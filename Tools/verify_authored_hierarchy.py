"""Validate persisted Unity references without claiming Editor import or Play success."""
from pathlib import Path
import re,json
ROOT=Path(__file__).resolve().parents[1]
def blocks(s):return {int(m[2]):m[0] for m in re.finditer(r'--- !u!(\d+) &(-?\d+)[^\n]*\n.*?(?=--- !u!|\Z)',s,re.S)}
def field(s,k):
 m=re.search(r'^  '+re.escape(k)+r': (.*)$',s,re.M);return m[1] if m else None
def ids(s):return [int(i) for i in re.findall(r'\{fileID: (-?\d+)\}',s)]
def guid(p):return re.search(r'^guid: (\w+)',p.read_text(errors='ignore'),re.M)[1]
scriptGuids={p.stem:guid(p) for p in (ROOT/'Assets/EternalSteam/Tests/OpenWorld/Runtime').glob('*.cs.meta')}
guidPaths={}
for top in [ROOT/'Assets',ROOT/'Library/PackageCache']:
 for p in top.rglob('*.meta'):
  match=re.search(r'^guid: (\w+)',p.read_text(errors='ignore'),re.M)
  if match:guidPaths.setdefault(match[1],[]).append(p)
report=[]
for path in (ROOT/'Assets/EternalSteam/Scene/Tests').glob('*.unity'):
 s=path.read_text();bs=blocks(s);created={i:b for i,b in bs.items() if 600000000000<i<600000100000}
 if not created:continue
 assert len(bs)==len(re.findall(r'^--- !u!',s,re.M)),path
 for i,b in created.items():
  for target in ids(b):assert target==0 or target in bs,(path.name,i,'missing local',target)
  for g in re.findall(r'guid: (\w+)',b):assert g.startswith('0000000000000000') or g in guidPaths,(path.name,i,'missing asset',g)
  if 'm_Father: ' in b:
   parent=ids(field(b,'m_Father'))[0]
   if parent and parent in created:assert '{fileID: '+str(i)+'}' in created[parent],(path.name,'parent-child mismatch',i)
 for name in ['WorldGridView.cs','BuildAreaHologramView.cs']:
  views=[b for b in created.values() if 'guid: '+scriptGuids[name]+',' in b];assert len(views)==1
  for key in ['renderers','filters']:
   seq=re.search(r'^  '+key+r':\n((?:  - .*\n)+)',views[0],re.M)[1];assert len(ids(seq))==9
 canvas=[b for b in created.values() if 'guid: '+scriptGuids['CanvasWorldHud.cs']+',' in b]
 if canvas:
  assert len(canvas)==1
  for k in ['Sandbox','Input','Amount','Minimap','HealthFill','ClockHand','Raycaster']:assert ids(field(canvas[0],k))[0] in bs,(path.name,k)
  for key in ['Texts','Buttons','Sections']:
   seq=re.search(r'^  '+key+r':\n(.*?)(?=^  \w|\Z)',canvas[0],re.M|re.S)[1];names=re.findall(r'^  - Id: (.+)',seq,re.M);assert len(names)==len(set(names)),(path.name,key)
  callbacks=[b for b in created.values() if 'm_MethodName: Execute' in b]
  assert all('m_CallState: 2' in b for b in callbacks)
  # Exactly one authored active EventSystem in migrated scenes.
  eventGuid=next(g for g,paths in guidPaths.items() if any(p.name=='EventSystem.cs.meta' for p in paths))
  assert sum('guid: '+eventGuid+',' in b for b in bs.values())==1
  for b in bs.values():
   if 'guid: d3cdda27ee36d4c07ad56518ddd24b5f,' in b:
    go=ids(field(b,'m_GameObject'))[0];assert 'm_IsActive: 0' in bs[go]
 report.append({'scene':path.name,'newGameObjects':sum('GameObject:\n' in b for b in created.values()),'canvas':bool(canvas),'persistedReferences':'PASS'})
print(json.dumps(report,ensure_ascii=False,indent=2))
