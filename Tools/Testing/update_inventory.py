#!/usr/bin/env python3
"""Refresh the audited test inventory; reject new cross-scene test dependencies."""
import argparse
import hashlib
import json
import re
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--measurement',required=True)
args=parser.parse_args()
measurement=(ROOT / args.measurement).resolve()
assert measurement.is_relative_to((ROOT/'Docs/Measurements').resolve()), 'Evidence must remain inside the project'
items = []
for result in sorted(measurement.glob('all-*.xml')):
    for test in ET.parse(result).iter('test-case'):
        name = test.get('fullname')
        items.append({'name': name, 'result': test.get('result'), 'duration': test.get('duration'),
                      'evidence': str(result.relative_to(ROOT))})
assert items, 'Run current-scene checks before building the inventory'
for folder in ('EditMode', 'Legacy', 'PlayMode'):
    for source in (ROOT / 'Assets/EternalSteam/Tests' / folder).rglob('*.cs'):
        assert not re.search(r'\b(?:OpenScene|OpenPreviewScene|NewScene|LoadScene(?:Async|InPlayMode)?)\s*\(', source.read_text()), str(source)
archives = json.loads((ROOT / 'Docs/Testing/archived-scene-tools.json').read_text())
for item in archives:
    assert hashlib.sha256((ROOT / item['archive']).read_bytes()).hexdigest() == item['sha256'], item['archive']
before = [t.get('fullname') for t in ET.parse(ROOT / 'Docs/Measurements/2026-10-03-performance/product-tests.xml').iter('test-case')]
def normalized(name):
    return re.sub(r'\("(?:StartRegionSandbox|OpenWorldSandbox)"(?:,|\))',lambda m:'(' if m[0].endswith(',') else '',name)
previous=set(map(normalized,before))
current={i['name'] for i in items}
report = {'scene': json.loads((measurement / 'all-summary.json').read_text())['scene'],
          'oldEditModeCount': len(before), 'currentEditModeCount': len(items),
          'removedDuplicateSceneCases': len(before)-len(previous),
          'addedCaseNames': sorted(current-previous), 'missingOriginalCases': sorted(previous-current),
          'legacyPlayModeOptionalCount': 4, 'archivedCrossSceneTools': len(archives),
          'policyCheck': 'PASS', 'oldCaseNames': before, 'currentCases': items}
(ROOT / 'Docs/Testing/inventory.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n')
print(f"PASS: {len(items)} cases; {len(archives)} archive hashes; no cross-scene loading in tests")
