"""Static scene/prefab checks. This is not an Editor import or Play test."""
from pathlib import Path
import re,json,hashlib
root=Path(__file__).resolve().parents[1]
assets=root/'Assets'
def blocks(text):return {int(m[2]):m[0] for m in re.finditer(r'--- !u!(\d+) &(-?\d+)[^\n]*\n.*?(?=--- !u!|\Z)',text,re.S)}
def guid(path):return re.search(r'guid: (\w+)',Path(str(path)+'.meta').read_text())[1]
def ref(block,key):return int(re.search(r'^  '+key+r': \{fileID: (\d+)\}',block,re.M)[1])
byguid={}
for p in assets.rglob('*.meta'):
 match=re.search(r'guid: (\w+)',p.read_text(errors='replace'))
 if match:byguid[match[1]]=p.with_suffix('')
reports=[]
for name in ['StartRegionSandbox','OpenWorldSandbox']:
 p=assets/'EternalSteam/Scene/Tests'/(name+'.unity');text=p.read_text();b=blocks(text)
 hud=next(v for v in b.values() if 'guid: '+guid(assets/'EternalSteam/Tests/OpenWorld/Runtime/RailwayHud.cs') in v)
 for key in ['Sandbox','Panel','ListPanel','DetailPanel','DraftPanel','Title','Summary','Feedback','Resource','Load','Unload','CancelPendingButton','ArmamentPanel','ArmamentChoice']:assert ref(hud,key) in b,(name,key)
 sandbox=b[ref(hud,'Sandbox')]
 assert ref(sandbox,'RailwayHud') in b and ref(sandbox,'RailwayView') in b
 view=b[ref(sandbox,'RailwayView')];assert ref(view,'RouteHighlight') in b
 added={i:v for i,v in b.items() if 810000000000<i<820000000000}
 for value in added.values():
  for match in re.finditer(r'\{fileID: (-?\d+)\}',value):assert int(match[1])==0 or int(match[1]) in b,(name,match[0])
 for value in [hud,view]:
  for match in re.finditer(r'\{fileID: (\d+), guid: (\w+), type: \d+\}',value):
   path=byguid.get(match[2]);assert path is not None,match[2]
   if path.suffix in ['.prefab','.asset']:assert int(match[1]) in blocks(path.read_text()),path
 callbacks=[v for v in b.values() if 'm_TargetAssemblyTypeName: EternalSteam.OpenWorld.RailwayHud,' in v]
 assert len(callbacks)==39,len(callbacks)
 assert any(re.search(r'm_StringArgument: [\"]?rotate[\"]?\s*$', c, re.M) for c in callbacks)
 catalogGuid=re.search(r'ContentCatalog: \{fileID: \d+, guid: (\w+)',sandbox)[1];catalog=byguid[catalogGuid].read_text()
 for name2 in ['railway.track','railway.station','railway.coal']:assert guid(assets/'EternalSteam/Content/Buildings/Railway'/(name2+'.asset')) in catalog
 reports.append({'scene':name,'newSerializedBlocks':len(added),'persistentRailwayCallbacks':len(callbacks),'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'staticReferences':'PASS'})
print(json.dumps(reports,ensure_ascii=False,indent=2))
