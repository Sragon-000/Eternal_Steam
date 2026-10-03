"""Audit a completed recorder run without changing its raw evidence."""
import argparse
import gzip
import collections
import hashlib
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('run', type=Path)
args = parser.parse_args()
root = args.run.resolve()
project = Path(__file__).resolve().parents[3]
if not root.is_relative_to(project / 'Docs/Measurements/Balance'):
    raise SystemExit('Expected a project balance recording directory')
event_path = root / 'events.jsonl'
if not event_path.exists():
    event_path = root / 'events.jsonl.gz'
with (gzip.open(event_path, 'rt') if event_path.suffix == '.gz' else event_path.open()) as stream:
    rows = [json.loads(line) for line in stream]
status = json.loads((root / 'status.json').read_text())
configuration = json.loads((root / 'configuration.json').read_text())
errors = []
if [r['sequence'] for r in rows] != list(range(1, len(rows) + 1)):
    errors.append('Missing or repeated sequence')
requests = [r['sequence'] for r in rows if r['kind'] == 'screenshot-request']
captures = [r for r in rows if r['kind'] == 'screenshot-captured']
resolved = [q['sequence'] for r in captures for q in r['requests']]
if sorted(requests) != sorted(resolved):
    errors.append('Screenshot requests not captured exactly once')
for row in rows:
    if row['kind'] == 'sample' and row['observationErrors']:
        errors.append('Observer errors at ' + str(row['sequence']))
if not status['stopped'] or status['error'] or status['screenshotsPending'] or rows[-1]['kind'] != 'run-end':
    errors.append('Unclean recording termination')
stocks = {}
for row in rows:
    if row['kind'] == 'inventory-observed':
        for stock in row['stocks']:
            stocks[(row['base'], stock['id'])] = stock['amount']
    elif row['kind'] == 'resource':
        key = (row['base'], row['resource'])
        if abs(stocks.get(key, 0) - row['before']) > 1e-6:
            errors.append('Resource ledger discontinuity at ' + str(row['sequence']))
        stocks[key] = row['after']
    elif row['kind'] == 'sample':
        for bank in row['inventories']:
            for stock in bank['stocks']:
                if abs(stocks.get((bank['baseId'], stock['id']), 0) - stock['amount']) > 1e-6:
                    errors.append('Inventory/sample mismatch at ' + str(row['sequence']))
files = []
for capture in captures:
    path = root / capture['file']
    if not path.is_relative_to(root) or not path.is_file():
        errors.append('Missing screenshot: ' + capture['file'])
        continue
    files.append(dict(file=capture['file'], sha256=hashlib.sha256(path.read_bytes()).hexdigest(), width=capture['width'], height=capture['height']))
report = dict(run=root.name, purpose=configuration.get('purpose'), normalPlay=configuration['normalPlay'],
              passed=not errors, errors=errors, counts=dict(collections.Counter(r['kind'] for r in rows)),
              gameSeconds=rows[-1]['gameSeconds'], screenshots=files,
              resourceOutput=dict(collections.Counter({k:sum(r['delta'] for r in rows if r['kind']=='resource' and r['operation']=='production-output' and r['resource']==k) for k in {r['resource'] for r in rows if r['kind']=='resource'}})))
(root / 'audit.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps({k:v for k,v in report.items() if k!='screenshots'}, indent=2))
if errors:
    raise SystemExit(1)
