"""Stream a completed recorder run without retaining large sample objects in memory."""
import argparse
import gzip
import collections
import hashlib
import json
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('run', type=Path)
parser.add_argument('--output', default='audit.json', help='New relative report path inside this run')
args = parser.parse_args()
root = args.run.resolve()
project = Path(__file__).resolve().parents[3]
if not root.is_relative_to(project / 'Docs/Measurements/Balance'):
    raise SystemExit('Expected a project balance recording directory')
output = (root / args.output).resolve()
if not output.is_relative_to(root) or output.exists():
    raise SystemExit('Use a new report path inside the run; never overwrite an audit')
status = json.loads((root / 'status.json').read_text())
if not status['stopped']:
    raise SystemExit('Controlled recorder stop required before final audit')
configuration = json.loads((root / 'configuration.json').read_text())
event_path = root / 'events.jsonl'
if not event_path.exists():
    event_path = root / 'events.jsonl.gz'
errors, stocks, files, seen_files = [], {}, [], set()
counts, requests, resolved, resource_output = (collections.Counter() for _ in range(4))
sequence, last = 0, None
with (gzip.open(event_path, 'rt') if event_path.suffix == '.gz' else event_path.open()) as stream:
    for line in stream:
        row = json.loads(line)
        if row['sequence'] != sequence + 1:
            errors.append('Missing or repeated sequence at ' + str(row['sequence']))
        sequence = row['sequence']
        last = row
        kind = row['kind']
        counts[kind] += 1
        if kind == 'screenshot-request':
            requests[sequence] += 1
        elif kind == 'screenshot-captured':
            resolved.update(q['sequence'] for q in row['requests'])
            path = (root / row['file']).resolve()
            if not path.is_relative_to(root) or not path.is_file():
                errors.append('Missing screenshot: ' + row['file'])
                continue
            if row['file'] in seen_files:
                errors.append('Repeated screenshot file: ' + row['file'])
            seen_files.add(row['file'])
            digest = hashlib.sha256()
            with path.open('rb') as image:
                header = image.read(24)
                digest.update(header)
                if len(header) != 24 or header[:8] != b'\x89PNG\r\n\x1a\n':
                    errors.append('Invalid PNG: ' + row['file'])
                elif (int.from_bytes(header[16:20], 'big'), int.from_bytes(header[20:24], 'big')) != (row['width'], row['height']):
                    errors.append('PNG dimensions mismatch: ' + row['file'])
                for chunk in iter(lambda: image.read(1024*1024), b''):
                    digest.update(chunk)
            files.append(dict(file=row['file'], sha256=digest.hexdigest(), width=row['width'], height=row['height']))
        elif kind == 'inventory-observed':
            for stock in row['stocks']:
                stocks[(row['base'], stock['id'])] = stock['amount']
        elif kind == 'resource':
            key = row['base'], row['resource']
            if abs(stocks.get(key, 0) - row['before']) > 1e-6:
                errors.append('Resource ledger discontinuity at ' + str(sequence))
            stocks[key] = row['after']
            resource_output.setdefault(row['resource'], 0)
            if row['operation'] == 'production-output':
                resource_output[row['resource']] += row['delta']
        elif kind == 'sample':
            if row['observationErrors']:
                errors.append('Observer errors at ' + str(sequence))
            for bank in row['inventories']:
                for stock in bank['stocks']:
                    if abs(stocks.get((bank['baseId'], stock['id']), 0) - stock['amount']) > 1e-6:
                        errors.append('Inventory/sample mismatch at ' + str(sequence))
if requests != resolved:
    errors.append('Screenshot requests not captured exactly once')
if not last or status['error'] or status['screenshotsPending'] or last['kind'] != 'run-end' or sequence != status['sequence']:
    errors.append('Unclean recording termination or status sequence mismatch')
if last and last.get('screenshotsPending', 0):
    errors.append('Final event has pending screenshots')
report = dict(run=root.name, purpose=configuration.get('purpose'), normalPlay=configuration['normalPlay'],
    passed=not errors, errors=errors, counts=dict(counts), gameSeconds=last['gameSeconds'] if last else None,
    screenshots=files, resourceOutput=dict(resource_output), streaming=True,
    auditToolSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())
output.parent.mkdir(parents=True, exist_ok=True)
with output.open('x') as stream:
    json.dump(report, stream, indent=2)
    stream.write('\n')
print(json.dumps({k:v for k,v in report.items() if k != 'screenshots'}, indent=2))
if errors:
    raise SystemExit(1)
