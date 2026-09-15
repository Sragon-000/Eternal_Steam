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
            public Vector2Int Key;
            public GameObject View;
            public BuildingWorld World;
            public PlacementSession Placement;
            public HordeTowerFactory Factory;
            public float Top;
        }
        readonly Terrain terrain;
        readonly GameObject foundationPrefab;
        readonly GameObject[] towerPrefabs;
        readonly Transform root;
        readonly List<HordeTower> towers;
        readonly Material surface, barrel, tracer, grid;
        readonly Material[] types, effects;
        readonly Dictionary<Vector2Int, Platform> platforms = new();
        readonly Dictionary<HordeTowerKind, BuildingDefinition> definitions = new();
        GameObject template;
        public IReadOnlyCollection<Platform> Platforms => platforms.Values;
        public FoundationPlacement(Terrain terrain, Transform root, List<HordeTower> towers, Material surface, Material barrel,
            Material tracer, Material grid, Material[] types, Material[] effects, GameObject foundationPrefab=null, GameObject[] towerPrefabs=null)
        {
            this.foundationPrefab=foundationPrefab;this.towerPrefabs=towerPrefabs;
            this.terrain=terrain;this.root=root;this.towers=towers;this.surface=surface;this.barrel=barrel;
            this.tracer=tracer;this.grid=grid;this.types=types;this.effects=effects;
            template=new GameObject("Test tower definition template");template.transform.SetParent(root,false);template.SetActive(false);
            foreach(HordeTowerKind kind in Enum.GetValues(typeof(HordeTowerKind))) {
                var definition=ScriptableObject.CreateInstance<BuildingDefinition>();
                definition.Id="openworld.legacy."+kind;definition.DisplayName=HordeTowerStats.Name(kind);
                definition.ViewPrefab=template;definitions.Add(kind,definition);
            }
        }
        public static Vector2Int Key(Vector3 point) => new(Mathf.FloorToInt(point.x/Size),Mathf.FloorToInt(point.z/Size));
        public bool CheckFoundation(Vector3 point,out Vector3 center,out string reason)
        {
            var key=Key(point);center=new Vector3((key.x+.5f)*Size,0,(key.y+.5f)*Size);
            if(platforms.TryGetValue(key,out var existing)){center.y=existing.Top;reason="이미 토대가 있는 자리입니다.";return false;}
            var origin=terrain.transform.position;var size=terrain.terrainData.size;
            if(center.x-4<origin.x || center.z-4<origin.z || center.x+4>origin.x+size.x || center.z+4>origin.z+size.z)
            {reason="지형 밖에는 토대를 놓을 수 없습니다.";return false;}
            float min=float.MaxValue,max=float.MinValue;
            for(int z=0;z<=4;z++)for(int x=0;x<=4;x++) {
                var p=center+new Vector3(x*2-4,0,z*2-4);float y=terrain.SampleHeight(p)+origin.y;
                min=Mathf.Min(min,y);max=Mathf.Max(max,y);
            }
            center.y=max+.35f;
            if(max-min>1.2f){reason="경사가 큽니다. 평탄한 곳을 선택하세요.";return false;}
            reason="클릭하여 8×8m 토대 설치";return true;
        }
        public bool AddFoundation(Vector3 point,out string reason)
        {
            if(!CheckFoundation(point,out var center,out reason))return false;
            if(foundationPrefab!=null) {
                var view=UnityEngine.Object.Instantiate(foundationPrefab,center-Vector3.up*.2f,Quaternion.identity,root);
                try {Adopt(view.GetComponent<SceneFoundation>());}
                catch {UnityEngine.Object.Destroy(view);throw;}
                reason="토대를 설치했습니다. 포탑을 선택하세요.";return true;
            }
            var key=Key(point);var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Foundation "+key;
            go.transform.SetParent(root,false);go.transform.position=center-Vector3.up*.2f;go.transform.localScale=new Vector3(8,.4f,8);
            go.GetComponent<Renderer>().sharedMaterial=surface;
            var factory=new HordeTowerFactory(root,towers,()=>HordeMapKind.Lane,barrel,tracer,grid,types,effects);
            var buildGrid=new BuildGrid(new RectInt(0,0,4,4),CellSize,new Vector3(key.x*8,center.y-.01f,key.y*8));
            var world=new BuildingWorld(buildGrid,factory);
            var platform=new Platform{Key=key,View=go,Top=center.y,Factory=factory,World=world,Placement=new PlacementSession(world)};
            platforms.Add(key,platform);
            for(int i=0;i<=4;i++) {
                var a=HordeVisualPrimitives.MakeLine("Grid X",go.transform,grid,.08f,2);
                a.SetPosition(0,new Vector3(key.x*8+i*2,center.y+.015f,key.y*8));a.SetPosition(1,new Vector3(key.x*8+i*2,center.y+.015f,key.y*8+8));
                var b=HordeVisualPrimitives.MakeLine("Grid Z",go.transform,grid,.08f,2);
                b.SetPosition(0,new Vector3(key.x*8,center.y+.015f,key.y*8+i*2));b.SetPosition(1,new Vector3(key.x*8+8,center.y+.015f,key.y*8+i*2));
            }
            reason="토대를 설치했습니다. 포탑을 선택하세요.";return true;
        }
        public void Adopt(SceneFoundation authored)
        {
            var key=authored.Key;
            if(platforms.ContainsKey(key))throw new InvalidOperationException("Duplicate authored foundation " + key);
            var factory=new HordeTowerFactory(root,towers,()=>HordeMapKind.Lane,barrel,tracer,grid,types,effects);
            var buildGrid=new BuildGrid(new RectInt(0,0,4,4),CellSize,new Vector3(key.x*8,authored.Top-.01f,key.y*8));
            var world=new BuildingWorld(buildGrid,factory);
            platforms.Add(key,new Platform {Key=key,View=authored.gameObject,Top=authored.Top,Factory=factory,World=world,Placement=new PlacementSession(world)});
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
            if(!platforms.TryGetValue(Key(point),out platform))return false;
            cell=platform.World.Grid.WorldToCell(point);center=platform.World.Grid.Center(cell,Vector2Int.one)+Vector3.up*.01f;
            return platform.World.Grid.Bounds.Contains(cell);
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
            platforms.Clear();foreach(var d in definitions.Values)UnityEngine.Object.Destroy(d);definitions.Clear();UnityEngine.Object.Destroy(template);
        }
    }
}
