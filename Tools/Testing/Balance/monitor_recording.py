"""Incremental read-only observation of a live JSONL recording; no Unity commands."""
import argparse, collections, datetime, json, time
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('run',type=Path);a=p.parse_args();root=a.run.resolve()
source=root/'events.jsonl';counts=collections.Counter();days={};last=None;weapons=None;buffer='';sequence=0;last_output=0;last_activity=time.monotonic();first_battery=None;first_debit=None;pilot_actions=collections.Counter()
with source.open() as stream:
 while True:
  chunk=stream.read()
  if chunk:
   buffer+=chunk;lines=buffer.split('\n');buffer=lines.pop();last_activity=time.monotonic()
   for line in lines:
    if not line:continue
    r=json.loads(line)
    if r['sequence']!=sequence+1:raise RuntimeError(f"Sequence gap {sequence} -> {r['sequence']}")
    sequence=r['sequence'];kind=r['kind'];counts[kind]+=1
    day=days.setdefault(str(r['day']),dict(spawned=0,killed=0,damage=0,removed=0))
    if kind=='sample':
     last=r
     if r['routes']:
      battery=r['routes'][0]['train']['armament']['battery']
      if first_battery is None:first_battery=dict(gameSeconds=r['gameSeconds'],battery=battery)
      if first_debit is None and battery<199.99:first_debit=dict(gameSeconds=r['gameSeconds'],battery=battery,alive=r['enemies']['alive'],phase=r['phase'])
    elif kind=='weapon-counters':weapons=r['actors']
    elif kind=='enemy-spawn':day['spawned']+=1
    elif kind=='enemy-killed':day['killed']+=1
    elif kind=='building-damage':day['damage']+=r['damage']
    elif kind=='building-removed':day['removed']+=1
    elif kind=='action' and r['action'].startswith('pilot-'):pilot_actions[r['action']]+=1
  terminal=(root/'status.json').exists()
  now=time.monotonic()
  if last is not None and (now-last_output>=30 or terminal):
   report=dict(utc=datetime.datetime.now(datetime.timezone.utc).isoformat(),sequence=sequence,terminal=terminal,secondsWithoutNewEvents=round(now-last_activity,2),gameSeconds=last['gameSeconds'],day=last['day'],phase=last['phase'],stage=last['stage'],days=days,counts=dict(counts),observerErrors=last['observationErrors'],bases=[dict(definition=b['definition'],id=b['baseId'],health=b['health'],level=b['level']) for b in last['buildings'] if b['baseId']],routes=[dict(id=r['route'],status=r['train']['status'],stop=r['train']['stop'],fuel=r['train']['fuel'],cargo=r['train']['cargo'],battery=r['train']['armament']['battery']) for r in last['routes']],firstBattery=first_battery,firstBatteryDebit=first_debit,weaponCounters=weapons,pilotActions=dict(pilot_actions))
   temp=root/'live-summary.json.tmp';temp.write_text(json.dumps(report,indent=2,ensure_ascii=False)+'\n');temp.replace(root/'live-summary.json')
   print(json.dumps({k:v for k,v in report.items() if k not in ['weaponCounters','counts','firstBattery','firstBatteryDebit','utc']},ensure_ascii=False),flush=True);last_output=now
  if terminal:
   if buffer:raise RuntimeError('Partial final event')
   break
  time.sleep(1)
