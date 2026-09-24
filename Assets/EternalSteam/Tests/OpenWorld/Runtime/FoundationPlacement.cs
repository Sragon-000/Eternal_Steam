using System;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam.Demo;

namespace EternalSteam.OpenWorld
{
    // Each foundation owns its grid and building world; no legacy map build zones or saves.
    public sealed class FoundationPlacement : IDisposable
    {
        public const float Size = 8, CellSize = 2;
        public sealed class Platform
        {
            public string PersistentId=Guid.NewGuid().ToString("N");
            public Vector2Int Key;
            public GameObject View;
            public BuildingWorld World;
            public PlacementSession Placement;
            public HordeTowerFactory Factory;
            public float Top;
        }
        readonly Terrain terrain;
        readonly OpenWorldContent content;
        readonly GameObject foundationPrefab;
        readonly GameObject[] towerPrefabs;
        readonly Transform root;
        readonly List<HordeTower> towers;
        readonly Material surface, barrel, tracer, grid;
        readonly Material[] types, effects;
        readonly Dictionary<Vector2Int, Platform> platforms = new();
        readonly Dictionary<HordeTowerKind, BuildingDefinition> definitions = new();
        GameObject template;
        public Platform GroundPlatform {get;private set;}
        public Dictionary<Vector2Int,Platform>.ValueCollection Platforms => platforms.Values;
        public FoundationPlacement(Terrain terrain, Transform root, List<HordeTower> towers, Material surface, Material barrel,
            Material tracer, Material grid, Material[] types, Material[] effects, GameObject foundationPrefab=null, GameObject[] towerPrefabs=null,OpenWorldContent content=null)
        {
            this.content=content;this.foundationPrefab=foundationPrefab;this.towerPrefabs=towerPrefabs;
            this.terrain=terrain;this.root=root;this.towers=towers;this.surface=surface;this.barrel=barrel;
            this.tracer=tracer;this.grid=grid;this.types=types;this.effects=effects;
            template=new GameObject("Test tower definition template");template.transform.SetParent(root,false);template.SetActive(false);
            foreach(HordeTowerKind kind in Enum.GetValues(typeof(HordeTowerKind))) {
                var definition=ScriptableObject.CreateInstance<BuildingDefinition>();
                definition.Id="openworld.legacy."+kind;definition.DisplayName=HordeTowerStats.Name(kind);
                definition.ViewPrefab=template;
                if(content!=null){definition.Placement=ScriptableObject.CreateInstance<BuildingPlacementDefinition>();definition.Placement.Surface=BuildingSurface.GroundOrFoundation;definition.Placement.RequiresBuildArea=!content.ExternalConstruction;definition.Placement.RequiresOperationalArea=content.ExternalConstruction;content.RegisterLegacy(definition);}
                if(towerPrefabs!=null)foreach(var prefab in towerPrefabs)if(prefab!=null&&prefab.TryGetComponent<SceneTower>(out var authored)&&authored.Kind==kind){
                    if(authored.Health!=null)definition.Modules.Add(authored.Health);
                    if(authored.CombatBody!=null)definition.Modules.Add(authored.CombatBody);
                    if(authored.Rotation!=null)definition.Modules.Add(authored.Rotation);
                    if(authored.Upgrade!=null)definition.Modules.Add(authored.Upgrade);
                    break;
                }
                if(content?.LegacyPower!=null)definition.Modules.Add(content.LegacyPower);
                definitions.Add(kind,definition);
            }
        }
        public void AttachGround()
        {
            if(content==null||GroundPlatform!=null)return;
            var factory=new HordeTowerFactory(root,towers,()=>HordeMapKind.Lane,barrel,tracer,grid,types,effects);
            content.GroundLegacyFactory=factory;
            GroundPlatform=new Platform{World=content.GroundWorld,Placement=content.GroundPlacement,Factory=factory};
        }
        public bool FindTowerCell(Vector3 point,out Platform platform,out Vector2Int cell,out Vector3 center)
        {
            if(FindCell(point,out platform,out cell,out center))return true;
            platform=GroundPlatform;if(platform==null)return false;cell=platform.World.Grid.WorldToCell(point);center=platform.World.Grid.Center(cell,Vector2Int.one);
            if(content.CheckGround(cell,Vector2Int.one,out var h,out _))center.y=h+.05f;return true;
        }
        public static Vector2Int Key(Vector3 point) => WorldGridGeometry.Cell(point,Size);
        public bool CheckFoundation(Vector3 point,out Vector3 center,out string reason)
        {
            if(!WorldGridGeometry.TerrainPlacement(terrain,point,out center,out reason))return false;
            if(content!=null&&content.ReservedSpawnOverlap(center,new Vector2Int(4,4))){reason="보스 스폰 3×3 구역과 겹칩니다.";return false;}
            if(content!=null&&!content.CanBuildFoundation(center)){reason="기지를 먼저 확정하고 건설 범위 안에 토대 전체를 배치하세요.";return false;}
            if(content!=null&&!content.FoundationClear(center)){reason="지면 건물 또는 임시 예약과 겹칩니다.";return false;}
            foreach(var platform in platforms.Values)
                if(WorldGridGeometry.Overlaps(center,platform.View.transform)) {reason="이미 토대가 있는 자리입니다.";return false;}
            return true;
        }
        public bool AddFoundation(Vector3 point,out string reason)
        {
            if(!CheckFoundation(point,out var center,out reason))return false;
            if(foundationPrefab!=null) {
                var view=UnityEngine.Object.Instantiate(foundationPrefab,center-Vector3.up*.2f,WorldGridGeometry.Rotation,root);
                try {Adopt(view.GetComponent<SceneFoundation>());}
                catch {UnityEngine.Object.Destroy(view);throw;}
                reason="토대를 설치했습니다. 포탑을 선택하세요.";return true;
            }
            var key=Key(point);var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Foundation "+key;
            go.transform.SetParent(root,false);go.transform.position=center-Vector3.up*.2f;go.transform.localScale=new Vector3(8,.4f,8);
            go.GetComponent<Renderer>().sharedMaterial=surface;go.transform.rotation=WorldGridGeometry.Rotation;
            var factory=new HordeTowerFactory(root,towers,()=>HordeMapKind.Lane,barrel,tracer,grid,types,effects);
            var buildGrid=WorldGridGeometry.Grid(go.transform,center.y);
            var world=new BuildingWorld(buildGrid,content==null?factory:content.Wrap(factory,buildGrid));
            var platform=new Platform{Key=key,View=go,Top=center.y,Factory=factory,World=world,Placement=content==null?new PlacementSession(world):content.FoundationSession(world)};
            platforms.Add(key,platform);content?.Bases.Invalidate();
            for(int i=0;i<=4;i++) {
                var a=HordeVisualPrimitives.MakeLine("Grid X",go.transform,grid,.08f,2);
                a.SetPosition(0,WorldGridGeometry.ToWorld(new Vector3(key.x*8+i*2,center.y+.015f,key.y*8)));a.SetPosition(1,WorldGridGeometry.ToWorld(new Vector3(key.x*8+i*2,center.y+.015f,key.y*8+8)));
                var b=HordeVisualPrimitives.MakeLine("Grid Z",go.transform,grid,.08f,2);
                b.SetPosition(0,WorldGridGeometry.ToWorld(new Vector3(key.x*8,center.y+.015f,key.y*8+i*2)));b.SetPosition(1,WorldGridGeometry.ToWorld(new Vector3(key.x*8+8,center.y+.015f,key.y*8+i*2)));
            }
            reason="토대를 설치했습니다. 포탑을 선택하세요.";return true;
        }
        public void Adopt(SceneFoundation authored)
        {
            var key=authored.Key;
            if(platforms.ContainsKey(key))throw new InvalidOperationException("Duplicate authored foundation " + key);
            var factory=new HordeTowerFactory(root,towers,()=>HordeMapKind.Lane,barrel,tracer,grid,types,effects);
            var buildGrid=WorldGridGeometry.Grid(authored.transform,authored.Top);
            var world=new BuildingWorld(buildGrid,content==null?factory:content.Wrap(factory,buildGrid));
            platforms.Add(key,new Platform {Key=key,View=authored.gameObject,Top=authored.Top,Factory=factory,World=world,Placement=content==null?new PlacementSession(world):content.FoundationSession(world)});
        }
        public void Adopt(SceneTower authored)
        {
            if(!FindCell(authored.transform.position,out var p,out var cell,out _))throw new InvalidOperationException("Authored tower needs a foundation: "+authored.name);
            var result=p.Placement.Add(definitions[authored.Kind],cell,out var request);
            if(!result.Success)throw new InvalidOperationException(result.Message);
            try {
                var view=authored.RuntimeView();request.Direction=view.head.forward;
                p.Factory.ConfigureRequest(request.Id,view.range,view.kind);p.Factory.ConfigureExistingRequest(request.Id,view);
                result=p.Placement.Confirm();if(!result.Success)throw new InvalidOperationException(result.Message);
            } finally {p.Factory.ForgetRequest(request.Id);p.Placement.Cancel();}
        }
        public bool FindCell(Vector3 point,out Platform platform,out Vector2Int cell,out Vector3 center)
        {
            cell=default;center=point;
            platform=null;
            foreach(var candidate in platforms.Values) {
                var candidateCell=candidate.World.Grid.WorldToCell(point);
                if(!candidate.World.Grid.Bounds.Contains(candidateCell))continue;
                platform=candidate;cell=candidateCell;center=platform.World.Grid.Center(cell,Vector2Int.one)+Vector3.up*.01f;return true;
            }
            return false;
        }
        public BuildingDefinition Definition(HordeTowerKind kind) => definitions[kind];
        public GameObject CreateTowerPreview(HordeTowerKind kind,Vector3 position,Vector3 direction)
            => UnityEngine.Object.Instantiate(towerPrefabs[(int)kind],position,Quaternion.LookRotation(direction),root);
        public GameObject CreateFoundationPreview(Vector3 center)
            => UnityEngine.Object.Instantiate(foundationPrefab,center-Vector3.up*.2f,WorldGridGeometry.Rotation,root);
        public void RemoveFoundation(Platform platform)
        {
            platform.Placement.Dispose();platform.World.Dispose();platform.Factory.Dispose();
            platforms.Remove(platform.Key);content?.Bases.Invalidate();UnityEngine.Object.Destroy(platform.View);
        }
        public void PrepareRequest(Platform platform,PlacementRequest request,HordeTowerKind kind,GameObject view)
        {
            platform.Factory.ConfigureRequest(request.Id,22,kind);
            platform.Factory.ConfigureExistingRequest(request.Id,view.GetComponent<SceneTower>().RuntimeView());
        }
        public bool Install(Vector3 point,Vector3 direction,HordeTowerKind kind,out string reason)
        {
            direction.y=0;
            if(!FindCell(point,out var p,out var cell,out var center) ){reason="토대 위의 칸을 선택하세요.";return false;}
            if(direction.sqrMagnitude<.01f || !float.IsFinite(direction.sqrMagnitude)){reason="포탑에서 떨어진 지점을 눌러 방향을 정하세요.";return false;}
            if(!definitions.TryGetValue(kind,out var d)){reason="알 수 없는 포탑입니다.";return false;}
            var result=p.Placement.Add(d,cell,out var request);reason=result.Message;
            if(!result.Success)return false;
            GameObject prepared=null;
            try {
                request.Direction=direction.normalized;p.Factory.ConfigureRequest(request.Id,22,kind);
                if(towerPrefabs!=null && (int)kind<towerPrefabs.Length && towerPrefabs[(int)kind]!=null) {
                    prepared=UnityEngine.Object.Instantiate(towerPrefabs[(int)kind],center,Quaternion.LookRotation(direction.normalized),root);
                    prepared.SetActive(false);
                    p.Factory.ConfigureExistingRequest(request.Id,prepared.GetComponent<SceneTower>().RuntimeView());
                }
                result=p.Placement.Confirm();
                if(result.Success)prepared=null;
                reason=result.Success?"포탑 설치 완료 — 다음 칸을 선택하세요.":result.Message;return result.Success;
            } finally {
                if(prepared!=null)UnityEngine.Object.Destroy(prepared);
                p.Factory.ForgetRequest(request.Id);p.Placement.Cancel();
            }
        }
        public bool Recover(Vector3 point,out string reason)
        {
            if(FindCell(point,out var p,out var cell,out _) && p.World.Grid.OccupantAt(cell) is int id) {
                var recovery=new RecoverySession(p.World);recovery.Toggle(id);var result=recovery.Confirm();
                reason=result.Success?"포탑을 회수했습니다. 이 칸에 다시 설치할 수 있습니다.":result.Message;return result.Success;
            }
            reason="회수할 포탑이 있는 칸을 선택하세요.";return false;
        }
        public void Dispose()
        {
            foreach(var p in platforms.Values){p.Placement.Dispose();p.World.Dispose();p.Factory.Dispose();UnityEngine.Object.Destroy(p.View);}
            platforms.Clear();foreach(var d in definitions.Values){if(d.Placement!=null)UnityEngine.Object.Destroy(d.Placement);UnityEngine.Object.Destroy(d);}definitions.Clear();UnityEngine.Object.Destroy(template);
        }
    }
}
