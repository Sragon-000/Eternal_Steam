"""Summarize raw gameplay events; supports a still-running recording snapshot."""
import argparse, collections, gzip, json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('run',type=Path);a=p.parse_args();root=a.run.resolve()
config=json.loads((root/'configuration.json').read_text());days={};resources=collections.defaultdict(lambda:collections.defaultdict(float));built={};actions=[];last=None;shots=0;errors=0;stockfirst={};stocklast={};active=(root/'status.json').exists()==False
event_path=root/'events.jsonl'
stream=event_path.open() if event_path.exists() else gzip.open(root/'events.jsonl.gz','rt')
for line in stream:
 try:r=json.loads(line)
 except json.JSONDecodeError:
  if active:break
  raise
 day=days.setdefault(str(r['day']),dict(spawned=0,killed=0,damage=0,removed=0,samples=0,maxAlive=0,powerShortageSamples=0,outsideAreaConsumerSamples=0,maximumFrameMs=0))
 kind=r['kind']
 if kind=='sample':
  last=r;day['samples']+=1;day['maxAlive']=max(day['maxAlive'],r['enemies']['alive']);day['maximumFrameMs']=max(day['maximumFrameMs'],r['frameMs']);errors=max(errors,r['observationErrors'])
  if any(b.get('power') and b['power']['role']=='Consumer' and b['operational'] and not b['power']['supplied'] for b in r['buildings']):day['powerShortageSamples']+=1
  if any(b.get('power') and b['power']['role']=='Consumer' and not b['operational'] for b in r['buildings']):day['outsideAreaConsumerSamples']+=1
  for bank in r['inventories']:
   stocklast[bank['baseId']]={v['id']:v['amount'] for v in bank['stocks']}
   stockfirst.setdefault(bank['baseId'],stocklast[bank['baseId']])
 elif kind=='building-added':built.setdefault(r['definition'],[]).append(r['building'])
 elif kind=='building-removed':day['removed']+=1
 elif kind=='building-damage':day['damage']+=r['damage']
 elif kind=='enemy-spawn':day['spawned']+=1
 elif kind=='enemy-killed':day['killed']+=1
 elif kind=='resource':resources[r['base']][r['resource']+':'+r['operation']]+=r['delta']
 elif kind=='action':actions.append({k:r[k] for k in ['gameSeconds','day','phase','action','detail']})
 elif kind=='screenshot-captured':shots+=1
stream.close()
catalog={v['id'] for v in config['catalog']};required=catalog|{'openworld.legacy.'+v['kind'] for v in config['hudCatalog'] if v['tool']=='Tower'}
completion=next((x for x in actions if x['action']=='orb-crafted'),None)
result=dict(combatEndGameSeconds=completion['gameSeconds'] if completion else last['gameSeconds'],run=root.name,purpose=config.get('purpose'),active=active,gameSeconds=last['gameSeconds'],day=last['day'],phase=last['phase'],stage=last['stage'],days=days,resourceNetByOperation=resources,initialInventories=stockfirst,lastInventories=stocklast,catalogAndLegacyDefinitionsBuilt=sorted(built),missingDefinitions=sorted(required-set(built)),foundationUsed=any(x['action']=='foundation-build' for x in actions),screenshots=shots,observationErrors=errors,actions=actions,lastState=last)
(root/'summary.json').write_text(json.dumps(result,indent=2,ensure_ascii=False)+'\n')
print(json.dumps({k:result[k] for k in ['run','active','gameSeconds','day','phase','stage','days','missingDefinitions','foundationUsed','screenshots','observationErrors']},indent=2))
