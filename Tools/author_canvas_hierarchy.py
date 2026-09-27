"""Offline migration of saved presentation hierarchy; only use without a reachable Editor.
Does not run in Play mode. Existing scene content is preserved; refuses duplicate migration.
"""
from pathlib import Path
import re,json,uuid,hashlib
ROOT=Path(__file__).resolve().parents[1]
ASSETS=ROOT/'Assets'
def guid(path):return re.search(r'guid: (\w+)',Path(str(path)+'.meta').read_text())[1]
def meta(path):
 p=Path(str(path)+'.meta')
 if not p.exists():p.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n')
 return guid(path)
def script(name):
 paths=list((ROOT/'Library/PackageCache').glob('com.unity.ugui*/**/'+name+'.cs'))+list((ASSETS/'EternalSteam').rglob(name+'.cs'))+list((ROOT/'Library/PackageCache').glob('com.unity.inputsystem*/**/'+name+'.cs'))
 assert len(paths)==1,(name,paths)
 return guid(paths[0])
def ref(i):return '{fileID: '+str(i)+'}'
def ext(g,i=11400000,t=2):return '{fileID: '+str(i)+', guid: '+g+', type: '+str(t)+'}'
def blockmap(s):return {int(m[2]):m[0] for m in re.finditer(r'--- !u!(\d+) &(-?\d+)[^\n]*\n.*?(?=--- !u!|\Z)',s,re.S)}
def field(s,name):
 m=re.search(r'^  '+re.escape(name)+r': (.*)$',s,re.M);return m[1] if m else None
def fid(s,name):return int(re.search(r'fileID: (-?\d+)',field(s,name))[1])
def value(s,key,default=''):
 v=field(s,key)
 if v is None:return default
 if v.startswith('"'):return json.loads(v)
 return v
common='  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n'
GUIDS={name:script(name) for name in ['Image','Button','GridLayoutGroup','ContentSizeFitter','TextMeshProUGUI','TMP_InputField','ScrollRect','Mask','CanvasScaler','GraphicRaycaster','EventSystem','InputSystemUIInputModule','CanvasWorldHud','CanvasClockDial','CanvasWorldMinimap','WorldGridView','BuildAreaHologramView']}
fonts='8f586378b4e144a9851e7b34d9b748ee'
# Persisted materials, not runtime clones.
matdir=ASSETS/'EternalSteam/Shared/Materials/Demo'
base=(matdir/'ValidPlacement.mat').read_text()
for name,color in [('InactivePlacement','{r: 1, g: 0.5, b: 0.1, a: 1}'),('DragSelection','{r: 0, g: 1, b: 1, a: 1}')]:
 p=matdir/(name+'.mat');s=base.replace('m_Name: ValidPlacement','m_Name: '+name);s=re.sub(r'(- _(?:BaseColor|Color): )\{[^\n]+\}',lambda m:m[1]+color,s)
 if name=='DragSelection':s=s.replace('m_CustomRenderQueue: -1','m_CustomRenderQueue: 4000').replace('    m_Floats:\n','    m_Floats:\n    - _ZTest: 8\n')
 p.write_text(s);meta(p)
p=matdir/'BuildAreaHologram.mat';shader=guid(ASSETS/'EternalSteam/Shared/Resources/EternalSteam/BuildAreaHologram.shader');p.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n'+common+'  serializedVersion: 8\n  m_Name: BuildAreaHologram\n  m_Shader: '+ext(shader,4800000,3)+'\n  m_CustomRenderQueue: 3000\n  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs: []\n    m_Ints: []\n    m_Floats: []\n    m_Colors: []\n');meta(p)
source=(ASSETS/'EternalSteam/Scene/Tests/StartRegionSandbox.unity').read_text();lineTemplate=next(b for b in blockmap(source).values() if b.startswith('--- !u!120'))
assetsByGuid={guid(p.with_suffix('')):p.with_suffix('') for p in ASSETS.rglob('*.meta') if re.search(r'guid: (\w+)',p.read_text(errors='ignore'))}
class Scene:
 def __init__(self,path):
  self.path=path;self.original=path.read_text();self.blocks=blockmap(self.original);self.nodes=[];self.components=[];self.nextid=600000000000;self.roots=[];self.texts=[];self.buttons=[];self.sections=[];self.catalog=[]
 def alloc(self):
  self.nextid+=1;assert self.nextid not in self.blocks;return self.nextid
 def node(self,name,parent=None,rect=None,active=1,layer=None):
  n={'go':self.alloc(),'tr':self.alloc(),'name':name,'parent':parent,'children':[],'components':[],'rect':rect,'active':active,'layer':(5 if rect else 0) if layer is None else layer};self.nodes.append(n)
  if parent:parent['children'].append(n)
  else:self.roots.append(n)
  return n
 def comp(self,n,kind,body):
  i=self.alloc();n['components'].append(i);self.components.append(f'--- !u!{kind} &{i}\n'+body);return i
 def mono(self,n,name,body):return self.comp(n,114,'MonoBehaviour:\n'+common+'  m_GameObject: '+ref(n['go'])+'\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: '+ext(GUIDS[name],11500000,3)+'\n  m_Name: \n  m_EditorClassIdentifier: \n'+body)
 def image(self,n,color=(.055,.075,.095,.96),ray=True,filled=False):
  self.comp(n,222,'CanvasRenderer:\n'+common+'  m_GameObject: '+ref(n['go'])+'\n  m_CullTransparentMesh: 1\n')
  return self.mono(n,'Image','  m_Material: {fileID: 0}\n  m_Color: {r: %s, g: %s, b: %s, a: %s}\n'%color+f'  m_RaycastTarget: {int(ray)}\n  m_Maskable: 1\n  m_Sprite: {{fileID: 0}}\n  m_Type: {3 if filled else 0}\n  m_FillMethod: 0\n  m_FillAmount: 1\n  m_FillOrigin: 0\n')
 def text(self,parent,key,text,rect,size=16):
  n=self.node(key,parent,rect);self.comp(n,222,'CanvasRenderer:\n'+common+'  m_GameObject: '+ref(n['go'])+'\n  m_CullTransparentMesh: 1\n');i=self.mono(n,'TextMeshProUGUI','  m_Material: {fileID: 0}\n  m_Color: {r: 0.9, g: 0.94, b: 0.96, a: 1}\n  m_RaycastTarget: 0\n  m_Maskable: 1\n  m_text: '+json.dumps(text,ensure_ascii=False)+'\n  m_isRightToLeft: 0\n  m_fontAsset: '+ext(fonts)+'\n  m_sharedMaterial: '+ext(fonts,2180264)+'\n  m_fontColor: {r: 0.9, g: 0.94, b: 0.96, a: 1}\n  m_fontColor32: {serializedVersion: 2, rgba: 4294309350}\n  m_fontSize: '+str(size)+'\n  m_fontSizeBase: '+str(size)+'\n  m_fontWeight: 400\n  m_enableAutoSizing: 0\n  m_HorizontalAlignment: 1\n  m_VerticalAlignment: 256\n  m_textAlignment: 257\n  m_TextWrappingMode: 1\n  m_enableWordWrapping: 1\n  m_overflowMode: 0\n  m_isRichText: 0\n  m_richText: 0\n  m_isOrthographic: 1\n  m_margin: {x: 5, y: 3, z: 5, w: 3}\n');self.texts.append((key,i));return n,i
 def button(self,parent,key,caption,rect,command=None):
  n=self.node(key,parent,rect);graphic=self.image(n,(.12,.19,.23,1));i=self.mono(n,'Button','  m_Navigation:\n    m_Mode: 0\n    m_WrapAround: 0\n    m_SelectOnUp: {fileID: 0}\n    m_SelectOnDown: {fileID: 0}\n    m_SelectOnLeft: {fileID: 0}\n    m_SelectOnRight: {fileID: 0}\n  m_Transition: 1\n  m_Colors:\n    m_NormalColor: {r: 1, g: 1, b: 1, a: 1}\n    m_HighlightedColor: {r: 1.25, g: 1.25, b: 1.25, a: 1}\n    m_PressedColor: {r: 0.6, g: 0.85, b: 1, a: 1}\n    m_SelectedColor: {r: 1, g: 1, b: 1, a: 1}\n    m_DisabledColor: {r: 0.55, g: 0.55, b: 0.55, a: 0.6}\n    m_ColorMultiplier: 1\n    m_FadeDuration: 0.1\n  m_Interactable: 1\n  m_TargetGraphic: '+ref(graphic)+'\n  m_OnClick:\n    m_PersistentCalls:\n      m_Calls:\n      - m_Target: '+ref(self.controller)+'\n        m_TargetAssemblyTypeName: EternalSteam.OpenWorld.CanvasWorldHud, EternalSteam.OpenWorldSandbox\n        m_MethodName: Execute\n        m_Mode: 5\n        m_Arguments:\n          m_ObjectArgument: {fileID: 0}\n          m_ObjectArgumentAssemblyTypeName: UnityEngine.Object, UnityEngine\n          m_IntArgument: 0\n          m_FloatArgument: 0\n          m_StringArgument: '+json.dumps(command or key)+'\n          m_BoolArgument: 0\n        m_CallState: 2\n');self.text(n,key+'-caption',caption,fill(),15);self.buttons.append((key,i));return n,i
 def section(self,key,node):self.sections.append((key,node['go']));return node
 def panel(self,parent,key,rect):n=self.node(key,parent,rect);self.image(n);return n
 def scroll(self,parent,name,rect,height):
  n=self.node(name,parent,rect);v=self.node('Viewport',n,fill());self.image(v,(1,1,1,.01));self.mono(v,'Mask','  m_ShowMaskGraphic: 0\n');c=self.node('Content',v,(0,1,1,1,0,1,0,0,0,height));self.mono(n,'ScrollRect','  m_Content: '+ref(c['tr'])+'\n  m_Horizontal: 0\n  m_Vertical: 1\n  m_MovementType: 2\n  m_Elasticity: 0.1\n  m_Inertia: 1\n  m_DecelerationRate: 0.135\n  m_ScrollSensitivity: 24\n  m_Viewport: '+ref(v['tr'])+'\n  m_HorizontalScrollbar: {fileID: 0}\n  m_VerticalScrollbar: {fileID: 0}\n');return c
 def line(self,parent,name,material):
  n=self.node(name,parent);s=lineTemplate[lineTemplate.index('LineRenderer:'):];s=re.sub(r'm_GameObject: \{fileID: \d+\}','m_GameObject: '+ref(n['go']),s);s=re.sub(r'(m_Materials:\n  - )\{[^\n]+\}',lambda m:m[1]+material,s);s=s.replace('m_CastShadows: 1','m_CastShadows: 0').replace('m_ReceiveShadows: 1','m_ReceiveShadows: 0').replace('value: 0.1','value: 1');s=re.sub(r'key[01]: \{[^\n]+\}',lambda m:m[0].split(':')[0]+': {r: 1, g: 1, b: 1, a: 1}',s);s=s.replace('widthMultiplier: 1','widthMultiplier: '+str(.08 if name=='InvalidFootprintPattern' else .12 if name=='SelectedWeaponRange' else .02));s=re.sub(r'm_Positions:\n.*?(?=  m_Parameters:)', 'm_Positions:\n'+('  - {x: 0, y: 0, z: 0}\n'*(4 if name=='InvalidFootprintPattern' else 5)),s,flags=re.S);return self.comp(n,120,s)
 def mesh(self,n,material,cube=False):
  f=self.comp(n,33,'MeshFilter:\n'+common+'  m_GameObject: '+ref(n['go'])+'\n  m_Mesh: '+('{fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}' if cube else '{fileID: 0}')+'\n')
  r=self.comp(n,23,'MeshRenderer:\n'+common+'  m_GameObject: '+ref(n['go'])+'\n  m_Enabled: '+('1' if cube else '0')+'\n  m_CastShadows: 0\n  m_ReceiveShadows: 0\n  m_LightProbeUsage: 0\n  m_ReflectionProbeUsage: 0\n  m_RenderingLayerMask: 1\n  m_Materials:\n  - '+material+'\n  m_SortingLayerID: 0\n  m_SortingOrder: 0\n');return f,r
 def write(self):
  output=[]
  for n in self.nodes:
   output.append(f'--- !u!1 &{n["go"]}\nGameObject:\n'+common+'  serializedVersion: 6\n  m_Component:\n'+''.join('  - component: '+ref(i)+'\n' for i in [n['tr']]+n['components'])+f'  m_Layer: {n["layer"]}\n  m_Name: '+json.dumps(n['name'])+'\n  m_TagString: Untagged\n  m_Icon: {fileID: 0}\n  m_NavMeshLayer: 0\n  m_StaticEditorFlags: 0\n  m_IsActive: '+str(n['active'])+'\n')
   rect=n['rect'];s=f'--- !u!{224 if rect else 4} &{n["tr"]}\n'+('RectTransform' if rect else 'Transform')+':\n'+common+'  m_GameObject: '+ref(n['go'])+'\n  serializedVersion: 2\n  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}\n  m_LocalPosition: {x: 0, y: 0, z: 0}\n  m_LocalScale: {x: 1, y: 1, z: 1}\n  m_ConstrainProportionsScale: 0\n  m_Children:'+ ('\n'+''.join('  - '+ref(c['tr'])+'\n' for c in n['children']) if n['children'] else ' []\n')+'  m_Father: '+ref(n['parent']['tr'] if n['parent'] else 0)+'\n  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}\n'
   if rect:
    ax,ay,bx,by,px,py,x,y,w,h=rect;s+=f'  m_AnchorMin: {{x: {ax}, y: {ay}}}\n  m_AnchorMax: {{x: {bx}, y: {by}}}\n  m_AnchoredPosition: {{x: {x}, y: {y}}}\n  m_SizeDelta: {{x: {w}, y: {h}}}\n  m_Pivot: {{x: {px}, y: {py}}}\n'
   output.append(s)
  roots=next(i for i,b in self.blocks.items() if 'SceneRoots:\n' in b);self.blocks[roots]+=''.join('  - '+ref(n['tr'])+'\n' for n in self.roots)
  s=self.original[:self.original.index('--- !u!')]+''.join(b for i,b in self.blocks.items() if i!=roots)+''.join('\n'.join(line.rstrip() for line in b.split('\n')) for b in output+self.components)+self.blocks[roots]
  backup=Path('/tmp/eternal-canvas-scene-backups');backup.mkdir(exist_ok=True);bp=backup/self.path.name
  if not bp.exists():bp.write_text(self.original)
  self.path.write_text(s)
def fill():return (0,0,1,1,.5,.5,0,0,0,0)
def box(x,y,w,h,anchor=(0,1)):return (*anchor,*anchor,*anchor,x,y,w,h)
def author(path):
 s=Scene(path)
 if 'm_Name: "ConstructionPresentation"' in s.original:raise RuntimeError('Already migrated: '+str(path))
 inputId=next(i for i,b in s.blocks.items() if 'guid: fa797ae9ad70d4a53b63f8ce570f3dbb,' in b);sandboxId=fid(s.blocks[inputId],'Sandbox');sandbox=s.blocks[sandboxId]
 root=s.node('ConstructionPresentation')
 refs={}
 for key,klass,mat in [('WorldGrid','WorldGridView',field(sandbox,'LineMaterial')),('BuildAreaHologram','BuildAreaHologramView',ext(guid(matdir/'BuildAreaHologram.mat'),2100000))]:
  parent=s.node(key,root);filters=[];renderers=[]
  for j in range(9):f,r=s.mesh(s.node('Chunk'+str(j),parent),mat);filters.append(f);renderers.append(r)
  refs[key]=s.mono(parent,klass,'  renderers:\n'+''.join('  - '+ref(i)+'\n' for i in renderers)+'  filters:\n'+''.join('  - '+ref(i)+'\n' for i in filters))
 s.blocks[sandboxId]+=''.join('  '+k+': '+ref(v)+'\n' for k,v in refs.items())
 preview=s.node('PlacementPreview',root,active=0);s.mesh(preview,field(sandbox,'ValidMaterial'),True)
 pattern=s.line(preview,'InvalidFootprintPattern',field(sandbox,'InvalidMaterial'));direction=s.line(root,'SelectedWeaponRange',field(sandbox,'ValidMaterial'));drag=s.line(root,'DragSelection',ext(guid(matdir/'DragSelection.mat'),2100000))
 s.blocks[inputId]+='  preview: '+ref(preview['go'])+'\n  inactivePreviewMaterial: '+ext(guid(matdir/'InactivePlacement.mat'),2100000)+'\n  invalidPattern: '+ref(pattern)+'\n  directionLine: '+ref(direction)+'\n  DragRectangle: '+ref(drag)+'\n'
 # Historical snapshots keep their original HUD; construction references are added for compatibility.
 if path.name not in ['StartRegionSandbox.unity','OpenWorldSandbox.unity']:
  s.write();return {'scene':path.name,'constructionObjects':len(s.nodes),'canvas':False}
 canvas=s.node('GameplayCanvas',rect=fill());s.comp(canvas,223,'Canvas:\n'+common+'  m_GameObject: '+ref(canvas['go'])+'\n  m_Enabled: 1\n  serializedVersion: 3\n  m_RenderMode: 0\n  m_Camera: {fileID: 0}\n  m_PlaneDistance: 100\n  m_PixelPerfect: 0\n  m_ReceivesEvents: 1\n  m_OverrideSorting: 0\n  m_AdditionalShaderChannelsFlag: 25\n  m_SortingLayerID: 0\n  m_SortingOrder: 10\n  m_TargetDisplay: 0\n')
 s.mono(canvas,'CanvasScaler','  m_UiScaleMode: 1\n  m_ReferencePixelsPerUnit: 100\n  m_ScaleFactor: 1\n  m_ReferenceResolution: {x: 1280, y: 800}\n  m_ScreenMatchMode: 0\n  m_MatchWidthOrHeight: 1\n  m_PhysicalUnit: 3\n  m_FallbackScreenDPI: 96\n  m_DefaultSpriteDPI: 96\n  m_DynamicPixelsPerUnit: 1\n')
 raycaster=s.mono(canvas,'GraphicRaycaster','  m_IgnoreReversedGraphics: 1\n  m_BlockingObjects: 0\n  m_BlockingMask:\n    serializedVersion: 2\n    m_Bits: 4294967295\n');s.controller=s.alloc();canvas['components'].append(s.controller)
 event=s.node('GameplayEventSystem');s.mono(event,'EventSystem','  m_FirstSelected: {fileID: 0}\n  m_sendNavigationEvents: 1\n  m_DragThreshold: 6\n');s.mono(event,'InputSystemUIInputModule','  m_SendPointerHoverToParent: 1\n  m_MoveRepeatDelay: 0.5\n  m_MoveRepeatRate: 0.1\n  m_XRTrackingOrigin: {fileID: 0}\n  m_ActionsAsset: {fileID: 0}\n  m_DeselectOnBackgroundClick: 1\n  m_PointerBehavior: 0\n  m_ScrollDeltaPerTick: 6\n')
 for i,b in list(s.blocks.items()):
  if 'guid: d3cdda27ee36d4c07ad56518ddd24b5f,' in b:
   go=fid(b,'m_GameObject');s.blocks[go]=s.blocks[go].replace('m_IsActive: 1','m_IsActive: 0');s.blocks[i]=b.replace('m_Enabled: 1','m_Enabled: 0')
 # Four anchored areas leave the center world unobstructed.
 top=s.panel(canvas,'TopBar',(0,1,1,1,.5,1,0,-8,-24,84))
 s.text(top,'date','1일차 · 낮',box(12,-5,170,30),21);s.text(top,'remaining','전환까지 05:00',box(12,-38,240,34),15)
 s.text(top,'mode','준비',box(270,-10,190,56));s.text(top,'power','기지 전력',box(-320,-8,230,62,(1,1)),16);s.button(top,'pause','일시정지',box(-12,-18,112,40,(1,1)))
 left=s.panel(canvas,'LeftStatus',box(12,-104,236,470));s.text(left,'main-title','메인 기지',box(8,-6,218,30),20);s.text(left,'main-health','체력',box(8,-38,218,30))
 health=s.node('HealthTrack',left,box(12,-74,212,12));s.image(health,(.15,.2,.23,1),False);fillNode=s.node('HealthFill',health,fill());healthFill=s.image(fillNode,(.15,.78,.68,1),False,True)
 s.button(left,'clock-fold','낮 / 밤',box(8,-98,220,28),'fold:clock');clockBody=s.section('clock',s.node('ClockBody',left,box(8,-132,220,100)));s.text(clockBody,'clock-face','낮        밤',box(0,0,216,25));dial=s.node('ClockDial',clockBody,box(70,-30,68,68));s.comp(dial,222,'CanvasRenderer:\n'+common+'  m_GameObject: '+ref(dial['go'])+'\n  m_CullTransparentMesh: 1\n');s.mono(dial,'CanvasClockDial','  m_Material: {fileID: 0}\n  m_Color: {r: 1, g: 1, b: 1, a: 1}\n  m_RaycastTarget: 0\n  m_Maskable: 1\n');hand=s.node('ClockHand',dial,(.5,.5,.5,.5,.5,0,0,0,3,29));s.image(hand,(1,.78,.24,1),False)
 s.button(left,'resource-fold','자원',box(8,-242,220,28),'fold:resources');resources=s.section('resources',s.node('ResourceBody',left,box(8,-276,220,186)));resContent=s.scroll(resources,'ResourceScroll',box(0,0,220,144),440);s.text(resContent,'resources','자원 현황',box(0,0,210,440));s.button(resources,'resources-all','전체 자원',box(0,-150,220,30))
 right=s.panel(canvas,'RightStatus',box(-12,-104,292,470,(1,1)));s.button(right,'minimap-fold','미니맵',box(8,-6,276,28),'fold:minimap');mapBody=s.section('minimap',s.node('MinimapBody',right,box(8,-40,276,210)));mapNode=s.node('WorldMap',mapBody,(.5,.5,.5,.5,.5,.5,0,0,208,208));s.comp(mapNode,222,'CanvasRenderer:\n'+common+'  m_GameObject: '+ref(mapNode['go'])+'\n  m_CullTransparentMesh: 1\n');mapId=s.mono(mapNode,'CanvasWorldMinimap','  m_Material: {fileID: 0}\n  m_Color: {r: 1, g: 1, b: 1, a: 1}\n  m_RaycastTarget: 1\n  m_Maskable: 1\n')
 selection=s.section('selection',s.node('SelectionPanel',right,box(8,-256,276,208),active=0));s.text(selection,'selection-title','선택 건물',box(0,0,232,30),18);s.button(selection,'selection-close','닫기',box(-2,0,44,28,(1,1)));selcontent=s.scroll(selection,'SelectionScroll',box(0,-34,276,168),500);s.text(selcontent,'selection-stats','건물 상세',box(0,0,264,320));up=s.section('upgrade',s.node('UpgradeControls',selcontent,box(0,-324,264,166)));s.button(up,'upgrade','강화',box(0,0,264,34));s.text(up,'upgrade-status','강화 상태',box(0,-38,264,110))
 bottom=s.panel(canvas,'ConstructionBar',(0,0,1,0,.5,0,0,10,-24,206));s.button(bottom,'edit','수정',box(8,-8,100,34));actions=s.section('edit-actions',s.node('EditActions',bottom,box(116,-8,190,34),active=0));s.button(actions,'confirm','확정',box(0,0,90,34));s.button(actions,'cancel','취소',box(98,0,90,34));s.button(bottom,'inventory-fold','건물 목록',box(320,-8,130,34),'fold:inventory');s.button(bottom,'power-fold','전력 상세',box(-240,-8,110,34,(1,1)),'fold:power');s.button(bottom,'menu-fold','진행 · 저장',box(-122,-8,112,34,(1,1)),'fold:menu')
 inventory=s.section('inventory',s.node('InventoryBody',bottom,(0,0,1,1,.5,.5,0,-18,-16,-92)))
 for j,(name,cat) in enumerate([('전체',-1),('방어',0),('자원',1),('설치',2),('기타',3)]):s.button(inventory,'category-'+str(cat),name,box(j*88,0,82,27),'category:'+str(cat))
 entries=[];catalogRef=field(sandbox,'ContentCatalog');cg=re.search(r'guid: (\w+)',catalogRef or '')
 if cg:
  catalogText=assetsByGuid[cg[1]].read_text();defs=re.findall(r'  - \{fileID: 11400000, guid: (\w+)',catalogText)
  for dg in defs:
   d=assetsByGuid[dg].read_text();idv=value(d,'Id');
   if field(sandbox,'StartingBase') and fid(sandbox,'StartingBase'):
    if idv=='installation.main_base':continue
   name=value(d,'DisplayName',idv);cat=int(value(d,'Category','0'));entries.append((name,dg,6,0,cat))
 towerSection=re.search(r'  TowerPrefabs:\n(.*?)(?=^  \w)',sandbox,re.M|re.S)
 if towerSection:
  for j,g in enumerate(re.findall(r'guid: (\w+)',towerSection[1])):
   t=assetsByGuid[g].read_text();kind=int(value(t,'Kind',str(j)));entries.append((['기관총','화염','미사일','레이저'][kind] if kind<4 else '포탑','',2,kind,0))
 if fid(sandbox,'FoundationPrefab'):entries.append(('토대 8×8m','',1,0,2))
 content=s.scroll(inventory,'CatalogScroll',(0,0,1,1,.5,.5,0,-18,0,-36),max(68,((len(entries)+5)//6)*66))
 grid=s.mono(content,'GridLayoutGroup','  m_Padding: {m_Left: 0, m_Right: 0, m_Top: 0, m_Bottom: 0}\n  m_ChildAlignment: 0\n  m_StartCorner: 0\n  m_StartAxis: 0\n  m_CellSize: {x: 200, y: 60}\n  m_Spacing: {x: 6, y: 6}\n  m_Constraint: 1\n  m_ConstraintCount: 6\n')
 s.mono(content,'ContentSizeFitter','  m_HorizontalFit: 0\n  m_VerticalFit: 2\n')
 for j,(name,dg,tool,kind,cat) in enumerate(entries):
  n,i=s.button(content,'catalog-'+str(j),name,(j%6/6,1,(j%6+1)/6,1,0,1,3,-(j//6)*66,-6,60),'build:'+str(j));s.catalog.append((dg,tool,kind,cat,i))
 s.text(bottom,'pending','수정: 설치와 회수',box(10,-172,900,26),14)
 notice=s.node('Notifications',canvas,(0,0,1,0,.5,0,0,222,-24,66));s.text(notice,'message','WASD 이동 · 수정으로 설치',box(0,0,1200,32),15);hint,_=s.text(notice,'placement-hint','설치 위치 안내',box(0,-32,1200,32),15);s.section('placement-hint',hint)
 # Menus overlay central world, never overlap the permanent side columns.
 menu=s.section('menu',s.panel(canvas,'ProgressAndSave',(.5,.5,.5,.5,.5,.5,0,10,580,420)));menu['active']=0;s.button(menu,'menu-close','닫기',box(-8,-8,60,28,(1,1)),'fold:menu');mc=s.scroll(menu,'MenuScroll',box(8,-46,564,364),730)
 save=s.section('save-controls',s.node('SaveControls',mc,box(0,0,550,246)));s.text(save,'save-status','저장 상태',box(0,0,546,120));s.button(save,'save','저장',box(0,-128,168,34));s.button(save,'load','이어하기',box(180,-128,168,34));s.button(save,'new-game','새 게임',box(360,-128,168,34));confirm=s.section('new-game-confirmation',s.node('NewGameConfirmation',save,box(0,-172,540,66),active=0));s.text(confirm,'new-game-warning','현재 진행을 초기화하고 새 게임을 시작합니다.',box(0,0,540,28),14);s.button(confirm,'new-game-confirm','새 게임 시작',box(0,-30,254,34));s.button(confirm,'new-game-cancel','돌아가기',box(266,-30,254,34));mp=s.section('map-progress',s.node('MapProgress',mc,box(0,-254,548,338)));s.text(mp,'energy','맵 에너지',box(0,0,540,120));s.text(mp,'stage','맵 진행',box(0,-124,540,104));s.button(mp,'orb-craft','완벽한 에너지 오브 제작',box(0,-234,530,38));s.button(mc,'developer-fold','개발자',box(0,-606,540,34),'fold:developer-body')
 power=s.section('power',s.panel(canvas,'PowerDetails',(.5,.5,.5,.5,.5,.5,0,100,470,260)));power['active']=0;s.button(power,'power-close','전력 상세 닫기',box(8,-8,454,32),'fold:power');pc=s.scroll(power,'PowerScroll',box(8,-46,454,204),330);s.text(pc,'power-details','기지 전력 상세',box(0,0,442,330))
 dev=s.section('developer',s.node('Developer',canvas,fill()));body=s.section('developer-body',s.panel(dev,'DeveloperPanel',(.5,.5,.5,.5,.5,.5,0,5,590,430)));body['active']=0;s.button(body,'developer-close','개발자 닫기',box(8,-8,574,32),'fold:developer-body');s.text(body,'counts','검증 통계',box(8,-46,574,98));s.button(body,'spawn','소환',box(220,-158,170,36));s.button(body,'spawn-air','공중 적: 끔',box(398,-158,184,36));s.button(body,'reset','적 초기화',box(8,-204,188,36));run,_=s.button(body,'run','전투 실행',box(204,-204,184,36));s.section('run',run);s.button(body,'day','낮으로',box(8,-250,280,36));s.button(body,'night','밤으로',box(298,-250,284,36))
 amount=s.panel(body,'SpawnAmount',box(8,-158,204,36));textNode,textId=s.text(amount,'amount-value','100',fill());inputImage=amount['components'][-1]
 amountId=s.mono(amount,'TMP_InputField','  m_Navigation:\n    m_Mode: 0\n  m_Transition: 0\n  m_Interactable: 1\n  m_TargetGraphic: '+ref(inputImage)+'\n  m_TextViewport: '+ref(amount['tr'])+'\n  m_TextComponent: '+ref(textId)+'\n  m_Text: "100"\n  m_ContentType: 9\n  m_InputType: 0\n  m_CharacterValidation: 1\n  m_CharacterLimit: 9\n  m_LineType: 0\n  m_RichText: 0\n  m_ReadOnly: 0\n  m_CaretBlinkRate: 0.85\n  m_CaretWidth: 1\n  m_CustomCaretColor: 1\n  m_CaretColor: {r: 1, g: 1, b: 1, a: 1}\n  m_SelectionColor: {r: 0.3, g: 0.6, b: 0.9, a: 0.7}\n  m_ResetOnDeActivation: 1\n  m_RestoreOriginalTextOnEscape: 1\n')
 bodyText='  Sandbox: '+ref(sandboxId)+'\n  Input: '+ref(inputId)+'\n  Texts:\n'+''.join('  - Id: '+json.dumps(k)+'\n    View: '+ref(i)+'\n' for k,i in s.texts)+'  Buttons:\n'+''.join('  - Id: '+json.dumps(k)+'\n    View: '+ref(i)+'\n' for k,i in s.buttons)+'  Sections:\n'+''.join('  - Id: '+json.dumps(k)+'\n    Body: '+ref(i)+'\n' for k,i in s.sections)+'  Catalog:\n'+''.join('  - Definition: '+(ext(dg) if dg else ref(0))+'\n    Tool: '+str(tool)+'\n    Kind: '+str(kind)+'\n    Category: '+str(cat)+'\n    View: '+ref(i)+'\n' for dg,tool,kind,cat,i in s.catalog)+f'  CatalogLayout: {ref(grid)}\n  Amount: {ref(amountId)}\n  Minimap: {ref(mapId)}\n  HealthFill: {ref(healthFill)}\n  ClockHand: {ref(hand["tr"])}\n  Raycaster: {ref(raycaster)}\n  ResourcePriority: []\n'
 s.components.append(f'--- !u!114 &{s.controller}\nMonoBehaviour:\n'+common+'  m_GameObject: '+ref(canvas['go'])+'\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: '+ext(GUIDS['CanvasWorldHud'],11500000,3)+'\n  m_Name: \n  m_EditorClassIdentifier: \n'+bodyText)
 s.write();return {'scene':path.name,'objects':len(s.nodes),'buttons':len(s.buttons),'catalog':len(s.catalog),'canvas':True}
if __name__=='__main__':
 scenes=[p for p in (ASSETS/'EternalSteam/Scene/Tests').glob('*.unity') if 'guid: fa797ae9ad70d4a53b63f8ce570f3dbb,' in p.read_text()]
 print(json.dumps([author(p) for p in scenes],ensure_ascii=False,indent=2))
