"""Aggregate closed normal-play evidence without merging separate campaigns into one survival run."""
import collections,csv,gzip,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[3]
RUNS=['20261003-survival-baseline-01','20261003-survival-improved-01','20261003-content-coverage-01','20261003-content-coverage-02']
out=ROOT/'Docs/Measurements/Balance/20261003-suite-review'
if out.exists(): raise ValueError('Never overwrite a published review')
out.mkdir()
runs=[];installed=set();weapons={};catalog=set();legacy={};hashes={}
for name in RUNS:
 r=ROOT/'Docs/Measurements/Balance'/name
 config=json.loads((r/'configuration.json').read_text());audit=json.loads((r/'audit.json').read_text());status=json.loads((r/'status.json').read_text())
 assert audit['passed'] and status['stopped'] and not status['error'] and config['normalPlay']
 catalog.update(x['id'] for x in config['catalog']);actors={};damage=0;removed=collections.Counter();last=None;crafted=None
 with gzip.open(r/'events.jsonl.gz','rt') as stream:
  for line in stream:
   row=json.loads(line)
   if row['kind']=='building-added':installed.add(row['definition'])
   elif row['kind']=='building-damage':damage+=row['damage']
   elif row['kind']=='building-removed':removed[row['definition']]+=1
   elif row['kind']=='weapon-counters':
    for a in row['actors']:
     if not a['definition'].startswith('defense.'):continue
     key=a['building'];v=actors.setdefault(key,dict(definition=a['definition'],shots=0,projectiles=0))
     v['shots']=max(v['shots'],a['shotSignals']);v['projectiles']=max(v['projectiles'],a['projectileSignals'])
   elif row['kind']=='sample':last=row
   elif row['kind']=='action' and row['action']=='orb-crafted':crafted=row['gameSeconds']
 for a in actors.values():
  w=weapons.setdefault(a['definition'],dict(kind='modern signals, not per-weapon kill counts',shots=0,projectiles=0,runs=[]))
  w['shots']+=a['shots'];w['projectiles']+=a['projectiles']
  if a['shots'] or a['projectiles']:
   if name not in w['runs']:w['runs'].append(name)
 if (r/'combat-coverage-status.json').exists():
  cover=json.loads((r/'combat-coverage-status.json').read_text());assert cover['stopped'] and not cover['error']
  for a in cover['legacy']:
   w=legacy.setdefault(a['definition'],dict(kind='actual cumulative legacy shot counter before orb reset',shots=0,projectiles=0,runs=[]));w['shots']+=a['shots']
   if a['shots'] and name not in w['runs']:w['runs'].append(name)
 runs.append(dict(run=name,day=last['day'],phase=last['phase'],stage=status['stage'],defeated=status['defeated'],gameSeconds=last['gameSeconds'],orbCraftGameSeconds=crafted,spawned=last['enemies']['spawned'],killed=last['enemies']['killed'],alive=last['enemies']['alive'],damage=damage,removed=dict(removed),recordedScreenshots=len(audit['screenshots']),allPngFiles=len(list(r.rglob('*.png'))),events=sum(audit['counts'].values()),resourceChanges=audit['counts'].get('resource',0),samples=audit['counts']['sample'],observerErrors=last['observationErrors']))
 for p in [r/'configuration.json',r/'audit.json',r/'status.json',r/'events.jsonl.gz']:
  h=hashlib.sha256()
  with p.open('rb') as stream:
   for b in iter(lambda:stream.read(1048576),b''):h.update(b)
  hashes[str(p.relative_to(ROOT))]=h.hexdigest()
weapons.update(legacy)
assert len(catalog)==24 and not catalog-installed
assert len(weapons)==13 and all(w['shots']>0 or w['projectiles']>0 for w in weapons.values())
report=dict(runs=runs,catalogDefinitions=sorted(catalog),installedDefinitions=sorted(installed),missingInstalled=sorted(catalog-installed),weaponParticipation=weapons,missingWeaponParticipation=[k for k,w in weapons.items() if w['shots']==0 and w['projectiles']==0],maximumObservedDay=11,completedNaturalNights=10,observationLimit='Ten-night practical checkpoint, not a theoretical maximum; independent runs are never added together.',recordedScreenshots=sum(r['recordedScreenshots'] for r in runs),allPngFiles=sum(r['allPngFiles'] for r in runs),sourceHashes=hashes,limitations=['Modern action/hit presentation and projectile travel signals are not per-weapon kill or damage attribution.','Legacy pre-craft counts unavailable in baseline; use supplemental content runs.','Free construction/upgrade policy is the current game configuration, not evidence for a balanced paid economy.','Post-clear QA and paused intervals are not additional survival.','No academic research experiments were started.'])
(out/'summary.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n')
with (out/'weapon-participation.csv').open('w',newline='') as f:
 w=csv.writer(f,lineterminator='\n');w.writerow(['definition','evidenceKind','actionOrShotCount','projectileSignals','runs'])
 for k,v in sorted(weapons.items()):w.writerow([k,v['kind'],v['shots'],v['projectiles'],';'.join(v['runs'])])
with (out/'run-comparison.csv').open('w',newline='') as f:
 keys=['run','day','phase','stage','gameSeconds','spawned','killed','alive','damage','recordedScreenshots','events','resourceChanges','samples'];w=csv.DictWriter(f,fieldnames=keys,extrasaction='ignore',lineterminator='\n');w.writeheader();w.writerows(runs)
print(json.dumps({k:report[k] for k in ['missingInstalled','missingWeaponParticipation','recordedScreenshots','allPngFiles']},ensure_ascii=False))
