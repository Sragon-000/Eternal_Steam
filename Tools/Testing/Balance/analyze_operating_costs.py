"""Export resource flows and sampled train operating costs at one fixed sample cutoff."""
import argparse
import collections
import csv
import gzip
import hashlib
import json
from pathlib import Path


def analyze(root):
    root = root.resolve()
    project = Path(__file__).resolve().parents[3]
    if not root.is_relative_to(project / 'Docs/Measurements/Balance'):
        raise ValueError('Expected project balance run')
    source = root / 'events.jsonl'
    compressed = not source.exists()
    if compressed:
        source = root / 'events.jsonl.gz'
    limit = None if compressed else source.stat().st_size
    active = not (root / 'status.json').exists()

    def rows():
        with (gzip.open(source, 'rb') if compressed else source.open('rb')) as stream:
            while limit is None or stream.tell() < limit:
                raw = stream.readline(-1 if limit is None else limit-stream.tell())
                if not raw:
                    return
                if not raw.endswith(b'\n'):
                    if active:
                        return
                    raise ValueError('Partial terminal event')
                yield raw, json.loads(raw)

    last = None
    for _, row in rows():
        if row['kind'] == 'sample':
            last = row
    if last is None:
        raise ValueError('No sample')
    cutoff = last['sequence']
    folder = root / 'checkpoints' / f'operating-costs-{cutoff:09d}'
    folder.mkdir(parents=True, exist_ok=False)
    flows = collections.defaultdict(lambda: dict(events=0, requested=0., delta=0.))
    ledger, names, previous, train_previous = {}, {}, None, {}
    status_seconds = collections.defaultdict(float)
    cargo_net = collections.defaultdict(lambda: dict(netLoad=0., netUnload=0.))
    train_metrics = {}
    errors, actions = [], collections.Counter()
    power_samples = collections.defaultdict(lambda: dict(samples=0, outsideAreaConsumerSamples=0, unsuppliedOperationalConsumerSamples=0))
    digest, sequence, prefix_bytes = hashlib.sha256(), 0, 0
    with (folder / 'train-samples.csv').open('w', newline='') as stream:
        csvout = csv.writer(stream, lineterminator='\n')
        csvout.writerow(['gameSeconds','day','phase','route','status','fuel','cargo','resource','battery','aliveEnemies'])
        for raw, row in rows():
            if row['sequence'] > cutoff:
                break
            if row['sequence'] != sequence+1:
                errors.append(f'Sequence gap at {row["sequence"]}')
            sequence = row['sequence']
            digest.update(raw)
            prefix_bytes += len(raw)
            kind = row['kind']
            if kind == 'inventory-observed':
                for stock in row['stocks']:
                    ledger[(row['base'],stock['id'])] = stock['amount']
            elif kind == 'resource':
                key = row['base'],row['resource']
                if abs(ledger.get(key,0)-row['before']) > 1e-6:
                    errors.append(f'Ledger mismatch {sequence}')
                ledger[key] = row['after']
                f = flows[(row['day'],row['base'],row['resource'],row['operation'])]
                f['events'] += 1
                f['requested'] += row['requested']
                f['delta'] += row['delta']
            elif kind == 'building-added' and row['baseId']:
                names[row['baseId']] = dict(definition=row['definition'],building=row['building'])
            elif kind == 'action':
                actions[row['action']] += 1
            elif kind == 'train-change':
                t = row['train']
                prior = train_previous.get(row['route'])
                before = prior['cargo'] if prior and prior['resource']==t['resource'] else 0
                delta = t['cargo']-before
                cargo_net[(row['route'],t['resource'])]['netLoad' if delta>=0 else 'netUnload'] += abs(delta)
                train_previous[row['route']] = t
            elif kind == 'sample':
                for bank in row['inventories']:
                    for stock in bank['stocks']:
                        if abs(ledger.get((bank['baseId'],stock['id']),0)-stock['amount']) > 1e-6:
                            errors.append(f'Sample mismatch {sequence}')
                day = power_samples[row['day']]
                day['samples'] += 1
                consumers = [b for b in row['buildings'] if b.get('power') and b['power']['role']=='Consumer']
                day['outsideAreaConsumerSamples'] += any(not b['operational'] for b in consumers)
                day['unsuppliedOperationalConsumerSamples'] += any(b['operational'] and not b['power']['supplied'] for b in consumers)
                if previous is not None:
                    dt = row['gameSeconds']-previous['gameSeconds']
                    if dt < -1e-6:
                        errors.append(f'Clock reversal {sequence}')
                    for route in previous['routes']:
                        status_seconds[(route['route'],route['train']['status'])] += max(0,dt)
                for route in row['routes']:
                    t, key = route['train'],route['route']
                    battery = t['armament']['battery']
                    csvout.writerow([row['gameSeconds'],row['day'],row['phase'],key,t['status'],t['fuel'],t['cargo'],t['resource'],battery,row['enemies']['alive']])
                    if key not in train_metrics:
                        train_metrics[key] = dict(firstSampleSeconds=row['gameSeconds'],initialSampleFuel=t['fuel'],initialSampleBattery=battery,
                            samples=0,minFuel=t['fuel'],minBattery=battery,maxBattery=battery,
                            sampledBatteryDecreases=0.,sampledBatteryIncreases=0.,
                            sampledFuelDecreases=0.,sampledFuelIncreases=0.,
                            previousBattery=battery,previousFuel=t['fuel'],zeroBatterySamples=0,zeroBatteryWithEnemiesSamples=0)
                    m = train_metrics[key]
                    m['samples'] += 1
                    m['minFuel'] = min(m['minFuel'],t['fuel'])
                    m['minBattery'] = min(m['minBattery'],battery)
                    m['maxBattery'] = max(m['maxBattery'],battery)
                    for value,old,label in [(battery,m['previousBattery'],'Battery'),(t['fuel'],m['previousFuel'],'Fuel')]:
                        m['sampled'+label+('Increases' if value>=old else 'Decreases')] += abs(value-old)
                    m['previousBattery'],m['previousFuel'] = battery,t['fuel']
                    m['zeroBatterySamples'] += battery<=1e-6
                    m['zeroBatteryWithEnemiesSamples'] += battery<=1e-6 and row['enemies']['alive']>0
                previous = row
    assert sequence == cutoff
    flow_rows = [dict(day=d,base=b,resource=r,operation=o,**v) for (d,b,r,o),v in sorted(flows.items())]
    with (folder / 'resource-flows.csv').open('w', newline='') as stream:
        writer = csv.DictWriter(stream,fieldnames=['day','base','resource','operation','events','requested','delta'],lineterminator='\n')
        writer.writeheader();writer.writerows(flow_rows)
    global_ops = collections.defaultdict(float)
    for (_,_,resource,operation),v in flows.items():
        global_ops[resource+':'+operation] += v['delta']
    stocks = collections.defaultdict(float)
    for bank in last['inventories']:
        for stock in bank['stocks']:
            stocks[stock['id']] += stock['amount']
    for metric in train_metrics.values():
        metric['finalFuel'] = metric.pop('previousFuel')
        metric['finalBattery'] = metric.pop('previousBattery')
    report = dict(run=root.name,active=active,lastSampleSequence=cutoff,prefixBytes=prefix_bytes,prefixSha256=digest.hexdigest(),
        gameSeconds=last['gameSeconds'],day=last['day'],phase=last['phase'],passed=not errors,errors=errors,
        bases=names,resourceNetByOperation=dict(global_ops),lastStocksByResource=dict(stocks),
        lastInventories=last['inventories'],lastRoutes=last['routes'],trainMetrics=train_metrics,
        trainStatusSecondsEstimate=[dict(route=r,status=s,seconds=t) for (r,s),t in sorted(status_seconds.items())],
        netCargoTransfers=[dict(route=r,resource=v,**x) for (r,v),x in cargo_net.items()],
        actions=dict(actions),powerSamplesByDay=dict(power_samples),
        limitations=['Resource flows are exact recorded changes through the fixed last sample, not a completed-run audit.',
            'Train fuel/battery sums are sampled net changes and can miss simultaneous consumption/replenishment; use as lower bounds.',
            'Train status durations hold the previous sampled state; boundaries have approximately one-sample uncertainty.',
            'Net cargo changes can hide simultaneous loading/unloading. This run uses separate load and unload stops; corroborate with bank ledger.',
            'Signal/event day differs from spawn-cohort day. Use ID/generation cohort audit for wave kills.',
            'Editor compile/pause/capture overhead is not a performance benchmark.'])
    (folder/'summary.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
    print(json.dumps({k:report[k] for k in ['run','active','day','phase','gameSeconds','passed','errors','resourceNetByOperation','lastStocksByResource','trainMetrics','netCargoTransfers','powerSamplesByDay']},ensure_ascii=False,indent=2))
    print('Saved:',folder)
    return not errors


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('run',type=Path)
    raise SystemExit(0 if analyze(parser.parse_args().run) else 1)
