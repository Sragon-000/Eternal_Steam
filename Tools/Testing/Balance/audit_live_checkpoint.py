"""Audit a fixed prefix of an active recording without pausing or modifying Unity."""
import argparse
import collections
import datetime
import hashlib
import json
import math
from pathlib import Path


def audit(root):
    root = root.resolve()
    project = Path(__file__).resolve().parents[3]
    if not root.is_relative_to(project / 'Docs/Measurements/Balance'):
        raise ValueError('Expected project balance recording directory')
    source = root / 'events.jsonl'
    limit = source.stat().st_size
    digest = hashlib.sha256()
    errors, stocks, requests, resolved, captures = [], {}, set(), set(), []
    counts, operations = collections.Counter(), collections.defaultdict(float)
    days, enemies, lifetime, installed = {}, {}, [], set()
    weapons, last, previous, observed_bytes = [], None, 0, 0
    with source.open('rb') as stream:
        while stream.tell() < limit:
            raw = stream.readline(limit - stream.tell())
            if not raw.endswith(b'\n'):
                break  # In-flight last row is outside this fixed evidence prefix.
            row = json.loads(raw)
            digest.update(raw)
            observed_bytes += len(raw)
            seq, kind = row['sequence'], row['kind']
            if seq != previous + 1:
                errors.append(f'Event sequence discontinuity at {seq}')
            previous = seq
            counts[kind] += 1
            day = days.setdefault(str(row['day']), dict(groundSpawned=0, airSpawned=0,
                killed=0, damage=0, removed=0, maximumAlive=0))
            if kind == 'inventory-observed':
                for stock in row['stocks']:
                    stocks[(row['base'], stock['id'])] = stock['amount']
            elif kind == 'resource':
                key = row['base'], row['resource']
                if not all(math.isfinite(row[k]) for k in ('before', 'after', 'delta')):
                    errors.append(f'Non-finite resource value at {seq}')
                if abs(stocks.get(key, 0) - row['before']) > 1e-6:
                    errors.append(f'Resource ledger discontinuity at {seq}')
                if abs(row['after'] - row['before'] - row['delta']) > 1e-6:
                    errors.append(f'Resource delta mismatch at {seq}')
                stocks[key] = row['after']
                operations[(row['base'], row['resource'], row['operation'])] += row['delta']
            elif kind == 'sample':
                last = row
                if row['observationErrors']:
                    errors.append(f'Observer error at {seq}')
                for bank in row['inventories']:
                    for stock in bank['stocks']:
                        if abs(stocks.get((bank['baseId'], stock['id']), 0) - stock['amount']) > 1e-6:
                            errors.append(f'Inventory/sample mismatch at {seq}')
                day['maximumAlive'] = max(day['maximumAlive'], row['enemies']['alive'])
            elif kind == 'building-added':
                installed.add(row['definition'])
            elif kind == 'building-damage':
                day['damage'] += row['damage']
            elif kind == 'building-removed':
                day['removed'] += 1
            elif kind == 'enemy-spawn':
                key = row['id'], row['generation']
                if key in enemies:
                    errors.append(f'Duplicate enemy identity at {seq}')
                enemies[key] = row
                day['airSpawned' if row['air'] else 'groundSpawned'] += 1
            elif kind == 'enemy-killed':
                key = row['id'], row['generation']
                spawned = enemies.pop(key, None)
                if spawned is None:
                    errors.append(f'Kill without active spawn at {seq}')
                else:
                    lifetime.append(dict(air=spawned['air'], health=spawned['health'],
                        spawnDay=spawned['day'], killDay=row['day'],
                        seconds=row['gameSeconds'] - spawned['gameSeconds']))
                day['killed'] += 1
            elif kind == 'weapon-counters':
                weapons = row['actors']
            elif kind == 'screenshot-request':
                requests.add(seq)
            elif kind == 'screenshot-captured':
                for request in row['requests']:
                    key = request['sequence']
                    if key not in requests or key in resolved:
                        errors.append(f'Unknown/repeated screenshot request at {seq}: {key}')
                    resolved.add(key)
                path = (root / row['file']).resolve()
                if not path.is_relative_to(root) or not path.is_file():
                    errors.append(f'Missing screenshot at {seq}')
                    continue
                data = path.read_bytes()
                if data[:8] != b'\x89PNG\r\n\x1a\n' or len(data) < 24:
                    errors.append(f'Invalid PNG at {seq}')
                else:
                    width, height = int.from_bytes(data[16:20], 'big'), int.from_bytes(data[20:24], 'big')
                    if (width, height) != (row['width'], row['height']):
                        errors.append(f'PNG dimensions mismatch at {seq}')
                captures.append(dict(file=row['file'], sha256=hashlib.sha256(data).hexdigest(), sequence=seq))
    if last is None:
        raise ValueError('No sample in prefix')
    config = json.loads((root / 'configuration.json').read_text())
    required = {x['id'] for x in config['catalog']}
    required |= {'openworld.legacy.' + x['kind'] for x in config['hudCatalog'] if x['tool'] == 'Tower'}
    combat = collections.defaultdict(lambda: dict(instances=0, shotSignals=0, projectileSignals=0))
    for weapon in weapons:
        if not weapon['definition'].startswith(('defense.', 'openworld.legacy.')):
            continue
        entry = combat[weapon['definition']]
        entry['instances'] += 1
        entry['shotSignals'] += weapon['shotSignals']
        entry['projectileSignals'] += weapon['projectileSignals']
    report = dict(run=root.name, recordedUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        scope='Fixed complete-event prefix of an active run; not a terminal audit or survival ceiling',
        sourceBytesAtRead=limit, prefixBytes=observed_bytes, prefixSha256=digest.hexdigest(),
        lastSequence=previous, lastSampleSequence=last['sequence'], gameSeconds=last['gameSeconds'],
        day=last['day'], phase=last['phase'], passed=not errors, errors=errors, counts=dict(counts),
        pendingScreenshotRequests=sorted(requests-resolved), screenshots=captures, days=days,
        resourceNetByOperation=[dict(base=b, resource=r, operation=o, delta=v)
            for (b,r,o),v in sorted(operations.items())],
        lastInventories=last['inventories'], lastRoutes=last['routes'],
        missingInstalledDefinitions=sorted(required-installed), weaponSignals=dict(combat),
        noObservedWeaponSignals=sorted(k for k,v in combat.items() if v['shotSignals']==v['projectileSignals']==0),
        weaponSignalLimitation='Presentation signals: not damage, hit, kill, or projectile counts; absence is not proof of a defect.',
        aliveEnemyIdentities=len(enemies), enemyLifetimes=lifetime)
    output = root / 'checkpoints' / f'audit-prefix-{previous:09d}.json'
    output.parent.mkdir(exist_ok=True)
    with output.open('x') as stream:
        json.dump(report, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    print(json.dumps({k:report[k] for k in ('scope','lastSequence','gameSeconds','day','phase',
        'passed','errors','counts','pendingScreenshotRequests','days','missingInstalledDefinitions',
        'noObservedWeaponSignals')}, ensure_ascii=False, indent=2))
    print('Saved:', output)
    return not errors


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('run', type=Path)
    raise SystemExit(0 if audit(parser.parse_args().run) else 1)
