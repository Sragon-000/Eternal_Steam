"""Offline additive railway authoring. Use only without a reachable Editor; preserves prior scene objects."""
from pathlib import Path
import ast,re,json,uuid,shutil
ROOT=Path(__file__).resolve().parents[1];ASSETS=ROOT/'Assets'
# Reuse the project's serialization helpers without executing its original migration/material writes.
tree=ast.parse((ROOT/'Tools/author_canvas_hierarchy.py').read_text())
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,(ast.FunctionDef,ast.ClassDef)) and getattr(n,'name','')!='author'],type_ignores=[]),'<existing-authoring-helpers>','exec'))
common='  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
for folder in [ASSETS/'EternalSteam/Code/Railway',ASSETS/'EternalSteam/Content/Buildings/Railway']:
 folder.mkdir(parents=True,exist_ok=True);meta(folder)
for folder in [ASSETS/'EternalSteam/Code/Railway',ASSETS/'EternalSteam/Code/Economy',ASSETS/'EternalSteam/Tests/OpenWorld/Runtime',ASSETS/'EternalSteam/Tests/EditMode']:
 for p in folder.iterdir():
  if p.suffix in ['.cs','.asmdef']:meta(p)
GUIDS={name:script(name) for name in ['Image','Button','TextMeshProUGUI','TMP_InputField','ScrollRect','Mask','ContentSizeFitter','RailwayHud','RailwaySceneView','RailTrackView']}
baseScene=ASSETS/'EternalSteam/Scene/Tests/StartRegionSandbox.unity';source=baseScene.read_text();lineTemplate=next(b for b in blockmap(source).values() if b.startswith('--- !u!120'))
fonts='8f586378b4e144a9851e7b34d9b748ee'
folder=ASSETS/'EternalSteam/Content/Buildings/Railway'
def asset(name,klass,body):
 p=folder/(name+'.asset');p.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'+common+'  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: '+ext(script(klass),11500000,3)+'\n  m_Name: '+name+'\n  m_EditorClassIdentifier: \n'+body);return meta(p)
# Fixed authored presentation, including train chassis, cargo and an empty installation slot.
material=field(next(b for b in blockmap(source).values() if '  EnemyMesh:' in b),'TowerMaterial')
def prefab(name,parts):
 p=folder/(name+'.prefab')
 if p.exists() and name!='Track':return guid(p),100001
 fake=folder/('_'+name+'.unity');fake.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!1660057539 &9223372036854775807\nSceneRoots:\n  m_ObjectHideFlags: 0\n  m_Roots: []\n')
 s=Scene(fake);s.nextid=100000;root=s.node(name)
 if name=='Track':
  left=s.line(root,'LeftRail',material);right=s.line(root,'RightRail',material)
  s.mono(root,'RailTrackView','  LeftRail: '+ref(left)+'\n  RightRail: '+ref(right)+'\n')
  s.components=[b.replace('m_UseWorldSpace: 1','m_UseWorldSpace: 0').replace('widthMultiplier: 0.02','widthMultiplier: 0.12') for b in s.components]
 for part,pos,scale in parts:
  n=s.node(part,root);s.mesh(n,material,True);n['pos']=pos;n['scale']=scale
 s.write();text=fake.read_text();blocks=blockmap(text)
 for n in s.nodes:
  if 'pos' not in n:continue
  b=blocks[n['tr']];b=b.replace('m_LocalPosition: {x: 0, y: 0, z: 0}', 'm_LocalPosition: {x: %s, y: %s, z: %s}'%tuple(n['pos']));b=b.replace('m_LocalScale: {x: 1, y: 1, z: 1}', 'm_LocalScale: {x: %s, y: %s, z: %s}'%tuple(n['scale']));blocks[n['tr']]=b
 p.write_text(text[:text.index('--- !u!')]+''.join(b for b in blocks.values() if 'SceneRoots:' not in b));fake.unlink();return meta(p),root['go']
trackPrefab,trackRoot=prefab('Track',[('Tie',(0,.03,0),(1.7,.06,.25))])
stationPrefab,stationRoot=prefab('Station',[('Platform',(0,.15,0),(3.8,.3,3.8)),('Office',(.8,.9,0),(1.6,1.5,2)),('PortWest',(-2,.3,-1),(.25,.2,.5)),('PortEast',(2,.3,1),(.25,.2,.5)),('PortSouth',(1,.3,-2),(.5,.2,.25)),('PortNorth',(-1,.3,2),(.5,.2,.25))])
trainPrefab,trainRoot=prefab('Train',[('Cab',(0,.3,.7),(.7,.6,.7)),('Cargo',(0,.1,-.2),(.8,.4,.8)),('Mount',(0,0,-1.1),(.8,.2,.8))])
railModule=asset('TrackModule','RailFacilityDefinition','  Kind: 0\n');stationModule=asset('StationModule','RailFacilityDefinition','  Kind: 1\n')
trackPlacement=asset('TrackPlacement','BuildingPlacementDefinition','  Surface: 1\n  RequiredNexusLevel: 1\n  VerificationSettings: 1\n  RequiresBuildArea: 0\n  RequiresOperationalArea: 0\n  RequiresOwnerBase: 0\n  SnapCells: 1\n')
stationPlacement=asset('StationPlacement','BuildingPlacementDefinition','  Surface: 1\n  RequiredNexusLevel: 1\n  VerificationSettings: 1\n  RequiresBuildArea: 0\n  RequiresOperationalArea: 1\n  RequiresOwnerBase: 1\n  SnapCells: 1\n')
def definition(name,label,size,placement,prefab,root,modules):
 return asset(name,'BuildingDefinition','  Id: '+name+'\n  DisplayName: '+json.dumps(label,ensure_ascii=False)+'\n  Category: 2\n  Footprint: {x: %s, y: %s}\n'%size+'  Recoverable: 1\n  Placement: '+ext(placement)+'\n  ViewPrefab: '+ext(prefab,root,3)+'\n  Modules:\n'+''.join('  - '+ext(m)+'\n' for m in modules))
track=definition('railway.track','선로 · 검증비 철 1',(1,1),trackPlacement,trackPrefab,trackRoot,[railModule])
station=definition('railway.station','기차역 · 검증비 철 10',(2,2),stationPlacement,stationPrefab,stationRoot,[stationModule])
coalRecipe=asset('CoalProduction','ProductionModuleDefinition','  InputId: \n  InputAmount: 0\n  OutputId: coal\n  OutputAmount: 1\n  Interval: 2\n')
iron=(ASSETS/'EternalSteam/Content/Buildings/DocumentContent/resource.iron.asset').read_text();iron=iron.replace('resource.iron','railway.coal');iron=re.sub(r'  DisplayName: .*','  DisplayName: "석탄 생성기 · 검증용"',iron);iron=iron.replace('guid: ae956334b5516403fa0644944bce7304','guid: '+coalRecipe)
coalPath=folder/'railway.coal.asset';coalPath.write_text(iron);coal=meta(coalPath)
# Verify the replaced recipe rather than relying on list order.
assert 'guid: '+coalRecipe in iron

def author(scene):
 s=Scene(scene);s.nextid=810000000000
 if 'm_Name: "RailwayInterface"' in s.original:print('Already authored',scene.name);return
 sandboxId=next(i for i,b in s.blocks.items() if '  EnemyMesh:' in b and '  ContentCatalog:' in b);sandbox=s.blocks[sandboxId]
 canvasGo=next(i for i,b in s.blocks.items() if b.startswith('--- !u!1 ') and field(b,'m_Name') in ['GameplayCanvas','"GameplayCanvas"']);canvasTr=next(i for i,b in s.blocks.items() if b.startswith('--- !u!224 ') and fid(b,'m_GameObject')==canvasGo)
 parent={'tr':canvasTr,'children':[]};hudRoot=s.node('RailwayInterface',parent,fill());hudRoot['layer']=5
 s.controller=s.alloc();hudRoot['components'].append(s.controller)
 bar=s.panel(hudRoot,'RailwayToolbar',box(280,-84,620,36))
 for i,(name,caption) in enumerate([('open','노선 관리'),('station','선택 역 노선'),('build:station','기차역'),('build:track','선로'),('build:coal','석탄 생성기'),('rotate','역 방향 90°')]):s.button(bar,'rail-'+str(i),caption,box(4+i*103,-2,98,32),name)
 panel=s.panel(hudRoot,'RailwayPanel',box(280,-128,620,570));panel['active']=0
 _,title=s.text(panel,'RailwayTitle','전체 노선',box(12,-8,420,28),20);s.button(panel,'rail-close','닫기 · 초안 취소',box(450,-8,158,30),'close')
 scroll=s.scroll(panel,'RailwaySummary',box(12,-46,596,222),400);summaryNode,summary=s.text(scroll,'RailwaySummaryText','',box(0,0,570,400),16)
 # Scroll content height is updated explicitly from text preferred height, without runtime object creation.
 listing=s.node('RailwayList',panel,box(12,-280,596,225));detail=s.node('RailwayDetail',panel,box(12,-280,596,225));draftPanel=s.node('RailwayDraft',panel,box(12,-280,596,225));detail['active']=0;draftPanel['active']=0
 rowTexts=[];rowButtons=[]
 for i in range(4):
  _,button=s.button(listing,'RouteRow'+str(i),'노선',box(0,-i*38,596,34),'row:'+str(i));rowButtons.append(button);rowTexts.append(s.texts[-1][1])
 for i,(cmd,label) in enumerate([('page-prev','이전 목록'),('page-next','다음 목록'),('new','새 노선')]):s.button(listing,'list-'+cmd,label,box(i*198,-160,190,34),cmd)
 for i,(cmd,label) in enumerate([('add','선택 역 추가'),('next-stop','다음 역 선택'),('remove','선택 역 제거'),('up','선택 역 위로'),('arrival','진입 포트 변경'),('departure','진출 포트 변경'),('validate','순환 검증'),('commit','노선 확정'),('cancel','초안 취소')]):s.button(draftPanel,'draft-'+cmd,label,box((i%3)*198,-(i//3)*42,190,36),cmd)
 s.text(draftPanel,'PortHelp','포트 1 서 / 2 동 / 3 남 / 4 북\n맵에서 역 선택 → 선택 역 추가 → 순환 검증 → 확정',box(0,-136,596,72),16)
 for i,(cmd,label) in enumerate([('start','운행 시작'),('stop','다음 역 중지'),('fuel','석탄 +50'),('next-stop','다음 역 설정'),('edit','노선 편집'),('list','목록으로')]):s.button(detail,'detail-'+cmd,label,box((i%3)*198,-(i//3)*40,190,34),cmd)
 inputs=[]
 for i,(label,text) in enumerate([('자원 ID','iron'),('방문 적재량','100'),('방문 하역량','0')]):
  s.text(detail,'input-label'+str(i),label,box(i*198,-84,190,24),15)
  node=s.panel(detail,'RailInput'+str(i),box(i*198,-110,190,34));graphic=node['components'][-1];tn,ti=s.text(node,'RailInputValue'+str(i),text,fill(),16)
  inputs.append(s.mono(node,'TMP_InputField','  m_Navigation:\n    m_Mode: 0\n  m_Transition: 0\n  m_Interactable: 1\n  m_TargetGraphic: '+ref(graphic)+'\n  m_TextViewport: '+ref(node['tr'])+'\n  m_TextComponent: '+ref(ti)+'\n  m_Text: '+json.dumps(text)+'\n  m_ContentType: 0\n  m_InputType: 0\n  m_CharacterValidation: '+('0' if i==0 else '2')+'\n  m_CharacterLimit: 32\n  m_LineType: 0\n  m_ReadOnly: 0\n  m_RichText: 0\n  m_CaretWidth: 1\n  m_CaretBlinkRate: 0.85\n'))
 s.button(detail,'configure','선택 역 화물 설정 저장',box(0,-154,390,34),'configure')
 _,cancelPending=s.button(detail,'cancel-pending','예약 변경 취소',box(396,-154,200,34),'cancel-pending')
 _,feedback=s.text(panel,'RailFeedback','검증용: 선로 철 1/역 철 10 · 석탄 기준 1 × 길이 배율 · 장착 후속',box(12,-508,596,52),15)
 viewRoot=s.node('RailwayPresentation');highlight=s.line(viewRoot,'RailRouteHighlight',field(sandbox,'LineMaterial'));view=s.mono(viewRoot,'RailwaySceneView','  TrainPrefab: '+ext(trainPrefab,trainRoot,3)+'\n  RouteHighlight: '+ref(highlight)+'\n')
 body='  CancelPendingButton: '+ref(cancelPending)+'\n  Sandbox: '+ref(sandboxId)+'\n  Panel: '+ref(panel['go'])+'\n  ListPanel: '+ref(listing['go'])+'\n  DetailPanel: '+ref(detail['go'])+'\n  DraftPanel: '+ref(draftPanel['go'])+'\n  Title: '+ref(title)+'\n  Summary: '+ref(summary)+'\n  Feedback: '+ref(feedback)+'\n  Rows:\n'+''.join('  - '+ref(i)+'\n' for i in rowTexts)+'  RowButtons:\n'+''.join('  - '+ref(i)+'\n' for i in rowButtons)+'  Resource: '+ref(inputs[0])+'\n  Load: '+ref(inputs[1])+'\n  Unload: '+ref(inputs[2])+'\n  Track: '+ext(track)+'\n  Station: '+ext(station)+'\n  CoalProducer: '+ext(coal)+'\n'
 s.components.append(f'--- !u!114 &{s.controller}\nMonoBehaviour:\n'+common+'  m_GameObject: '+ref(hudRoot['go'])+'\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: '+ext(GUIDS['RailwayHud'],11500000,3)+'\n  m_Name: \n  m_EditorClassIdentifier: \n'+body)
 s.components=[b.replace('EternalSteam.OpenWorld.CanvasWorldHud, EternalSteam.OpenWorldSandbox','EternalSteam.OpenWorld.RailwayHud, EternalSteam.OpenWorldSandbox') for b in s.components]
 b=s.blocks[canvasTr];b=b.replace('  m_Children:\n','  m_Children:\n  - '+ref(hudRoot['tr'])+'\n');s.blocks[canvasTr]=b
 s.blocks[sandboxId]+='  RailwayView: '+ref(view)+'\n  RailwayHud: '+ref(s.controller)+'\n'
 catalogGuid=re.search(r'guid: (\w+)',field(sandbox,'ContentCatalog'))[1]
 catalog=next(p.with_suffix('') for p in (ASSETS/'EternalSteam').rglob('*.meta') if 'guid: '+catalogGuid in p.read_text())
 c=catalog.read_text()
 for g in [track,station,coal]:
  if g not in c:c+='  - '+ext(g)+'\n'
 catalog.write_text(c)
 backup=Path('/tmp/eternal-railway-scenes');backup.mkdir(exist_ok=True);shutil.copy2(scene,backup/scene.name)
 s.write();print(scene.name,len(s.nodes),'new saved objects')
for name in ['StartRegionSandbox','OpenWorldSandbox']:author(ASSETS/'EternalSteam/Scene/Tests'/(name+'.unity'))
