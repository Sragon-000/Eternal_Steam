using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    public sealed class OpenWorldContent : IDisposable,ILevelLimit
    {
        int verificationLevel=8;
        public bool BaseRules {get;}
        public CampaignProgression Campaign {get;}
        public BasePowerSimulation Power {get;}
        public PowerModuleDefinition LegacyPower;
        public Action<BuildingInstance> PrepareRestoredBuilding;
        public Vector3? ReservedBossCenter;
        public Vector2Int ReservedBossFootprint=new Vector2Int(3,3);
        public bool SpawnFoundationOverlap(Vector3 center,Vector2Int footprint){if(foundations==null)return false;foreach(var p in foundations.Platforms)if(SpawnAreaValidator.Overlaps(center,footprint,p.View.transform.position,new Vector2Int(4,4)))return true;return false;}
        public bool ReservedSpawnOverlap(Vector3 center,Vector2Int footprint)=>ReservedBossCenter.HasValue&&SpawnAreaValidator.Overlaps(ReservedBossCenter.Value,ReservedBossFootprint,center,footprint);
        public bool Defeated {get;private set;}
        public event Action Failed;
        public BuildingInstance MainBase {get;private set;}
        public bool FixedMainBase {get;private set;}
        GameObject authoredMainView;bool installingMain;
        public PlacementResult InstallStartingBase(SceneStartingBase start)
        {
            FixedMainBase=true;installingMain=true;authoredMainView=start.gameObject;
            try {var result=GroundPlacement.Add(start.Definition,start.Cell,out _);if(result.Success)result=GroundPlacement.Confirm();return result;}
            finally {installingMain=false;authoredMainView=null;GroundPlacement.Cancel();}
        }
        public int LevelCap {get=>BaseRules?(MainBase?.Module<IUpgradeControl>()?.Level??0):verificationLevel;set=>verificationLevel=value;}
        public int SubLimit=>Math.Min(LevelCap*5,10);
        public bool ExternalConstruction {get;}
        public readonly BaseRegistry Bases=new(2,WorldGridGeometry.Rotation);
        public bool IsExempt(BuildingInstance b)=>BaseRules&&b.Module<IBaseRole>()?.Role==BaseRole.Main;
        public readonly ResourceBank Resources=new();
        public readonly BuildingTargetRegistry BuildingTargets=new();
        public readonly BuildingWorld GroundWorld;
        public readonly PlacementSession GroundPlacement;
        public readonly Dictionary<BuildingInstance,GameObject> Views=new();
        readonly Terrain terrain;readonly Transform parent,staging;readonly Material line;
        readonly BuildingServices services;FoundationPlacement foundations;
        public IBuildingFactory GroundLegacyFactory;
        readonly HashSet<BuildingDefinition> legacyDefinitions=new();
        public void RegisterLegacy(BuildingDefinition d)=>legacyDefinitions.Add(d);
        public OpenWorldContent(Terrain terrain,Transform parent,Material line,ITargetQuery targets,int nexusLevel,bool externalConstruction=false,bool baseRules=false,CampaignProgression campaign=null)
        {
            Campaign=campaign??new CampaignProgression();Power=new BasePowerSimulation(Bases,2,WorldGridGeometry.Rotation);BaseRules=baseRules;Bases.AnyNormalBaseCoverage=baseRules;ExternalConstruction=externalConstruction;this.terrain=terrain;this.parent=parent;this.line=line;LevelCap=nexusLevel;
            var stagingRoot=new GameObject("Inactive content staging");stagingRoot.transform.SetParent(parent,false);stagingRoot.SetActive(false);staging=stagingRoot.transform;
            services=new BuildingServices(targets,resources:Resources,levelLimit:this,campaign:Campaign);
            GroundWorld=new BuildingWorld(new BuildGrid(new RectInt(-4096,-4096,8192,8192),2,default,45),new Factory(this,null));
            GroundPlacement=new PlacementSession(GroundWorld,new GroundRule(this),new UnlockRule(this),new BasePlacementRule(this));
        }
        public void Attach(FoundationPlacement value){foundations=value;}
        public IBuildingFactory Wrap(IBuildingFactory legacy,BuildGrid grid)=>new Factory(this,legacy,grid);
        public PlacementSession FoundationSession(BuildingWorld world)=>new PlacementSession(world,new CoverageRule(this),new UnlockRule(this),new BasePlacementRule(this));
        public void TickProduction(float dt)=>Tick(dt,false);
        public void Tick(float dt,bool combatEnabled=true){Bases.Refresh();foreach(var building in GroundWorld.Buildings)building.Tick(dt,combatEnabled);foreach(var p in foundations.Platforms)foreach(var b in p.World.Buildings)b.Tick(dt,combatEnabled);}
        public bool Resolve(BuildingDefinition definition,Vector3 point,out PlacementSession session,out BuildingWorld world,out Vector2Int cell,out string reason)
        {
            session=null;world=null;cell=default;reason=null;
            if(definition==null){reason="건물 정의가 없습니다.";return false;}
            if(!IsMainDefinition(definition)&&(definition.Placement?.RequiredNexusLevel??1)>LevelCap){reason=$"기지 Lv.{definition.Placement.RequiredNexusLevel} 필요";return false;}
            if(definition.Placement?.Surface==BuildingSurface.Ground || (definition.Placement?.Surface==BuildingSurface.GroundOrFoundation&&!foundations.FindCell(point,out _,out _,out _))){world=GroundWorld;session=GroundPlacement;cell=definition.Placement.Snap(world.Grid.WorldToCell(point));return true;}
            if(!foundations.FindCell(point,out var p,out cell,out _)){reason="확정된 토대 위에 설치하세요.";return false;}
            world=p.World;session=p.Placement;return true;
        }
        public bool GroundAt(Vector3 point,out BuildingInstance building)
        {building=null;var cell=GroundWorld.Grid.WorldToCell(point);return GroundWorld.Grid.OccupantAt(cell) is int id&&GroundWorld.TryGet(id,out building);}
        public bool CanBuildFoundation(Vector3 center,ICollection<int> excluded=null)=>(!ReservedSpawnOverlap(center,new Vector2Int(4,4)))&&(ExternalConstruction||HasBuildArea(center,new Vector2Int(4,4),excluded));
        public bool HasBuildArea(Vector3 center,Vector2Int footprint,ICollection<int> excluded=null)
        {
            foreach(var b in GroundWorld.Buildings)if((excluded==null||!excluded.Contains(b.Id))&&b.Module<IBuildArea>() is IBuildArea area&&area.Contains(center,new Vector2(footprint.x,footprint.y),WorldGridGeometry.Rotation))return true;
            return false;
        }
        public bool CanRecoverGround(ICollection<int> removed,ICollection<FoundationPlacement.Platform> removedPlatforms)
        {
            if(ExternalConstruction)return true;
            bool removesArea=false;foreach(var b in GroundWorld.Buildings)if(removed.Contains(b.Id)&&b.Module<IBuildArea>()!=null)removesArea=true;
            if(!removesArea)return true;
            foreach(var p in foundations.Platforms)if(!removedPlatforms.Contains(p)&&!CanBuildFoundation(p.View.transform.position,removed))return false;
            foreach(var b in GroundWorld.Buildings)if(b.RequiresBuildArea&&!removed.Contains(b.Id)&&!HasBuildArea(b.Position,b.Footprint,removed))return false;
            return true;
        }
        public void ShowBuildAreas(bool visible)
        {foreach(var v in Views.Values)if(v!=null)v.GetComponent<ContentBuildingView>().ShowBuildArea(visible);}
        public bool FoundationClear(Vector3 center)
        {
            foreach(var b in GroundWorld.Buildings)if(Overlap(center,b.Position,b.Footprint))return false;
            foreach(var p in GroundPlacement.Pending)if(Overlap(center,GroundWorld.Grid.Center(p.Cell,p.Footprint),p.Footprint))return false;
            return true;
        }
        static bool Overlap(Vector3 foundation,Vector3 position,Vector2Int size)
        {var d=WorldGridGeometry.ToLocal(foundation-position);return Mathf.Abs(d.x)<4+size.x-.001f&&Mathf.Abs(d.z)<4+size.y-.001f;}
        public bool CheckGround(Vector2Int cell,Vector2Int footprint,out float height,out string reason)
        {
            height=float.MinValue;reason=null;float low=float.MaxValue;
            var center=GroundWorld.Grid.Center(cell,footprint);if(ReservedSpawnOverlap(center,footprint)){reason="보스 스폰 3×3 구역에는 건설할 수 없습니다.";return false;}var origin=terrain.transform.position;var size=terrain.terrainData.size;
            terrain.TryGetComponent<TileWorldGround>(out var tiles);
            for(int z=0;z<=footprint.y*2;z++)for(int x=0;x<=footprint.x*2;x++) {
                var p=center+WorldGridGeometry.ToWorld(new Vector3(x-footprint.x,0,z-footprint.y));
                if(p.x<origin.x||p.z<origin.z||p.x>origin.x+size.x||p.z>origin.z+size.z){reason="지형 경계를 벗어납니다.";return false;}
                float h;
                if(tiles!=null){if(!tiles.IsPlayable(p)||!tiles.TrySurface(p,out h)){reason="평지에만 설치할 수 있습니다.";return false;}}
                else h=terrain.SampleHeight(p)+origin.y;
                low=Mathf.Min(low,h);height=Mathf.Max(height,h);
            }
            if(height-low>1.2f){reason="경사가 큽니다.";return false;}
            foreach(var p in foundations.Platforms)if(Overlap(p.View.transform.position,center,footprint)){reason="토대와 겹칩니다.";return false;}
            return true;
        }
        public void Dispose(){GroundPlacement.Dispose();GroundWorld.Dispose();foreach(var v in Views.Values)if(v!=null)UnityEngine.Object.Destroy(v);Views.Clear();if(staging!=null)UnityEngine.Object.Destroy(staging.gameObject);}
        sealed class GroundRule:IPlacementRule
        {readonly OpenWorldContent host;public GroundRule(OpenWorldContent host){this.host=host;}public PlacementResult Validate(PlacementRequest p,IReadOnlyList<PlacementRequest> all,BuildGrid g){
            if(p.Definition.Placement!=null&&!p.Definition.Placement.IsAligned(p.Cell))return new PlacementResult("alignment","건물 배치 격자에 맞춰 설치하세요.");
            if(p.Definition.Placement?.RequiresBuildArea==true&&!host.HasBuildArea(g.Center(p.Cell,p.Footprint),p.Footprint))return new PlacementResult("build-area","기지를 먼저 확정하고 건물 전체를 건설 범위 안에 배치하세요.");
            return host.CheckGround(p.Cell,p.Footprint,out _,out var reason)?PlacementResult.Ok:new PlacementResult("ground",reason);
        }}
        sealed class CoverageRule:IPlacementRule
        {
            readonly OpenWorldContent host;public CoverageRule(OpenWorldContent host){this.host=host;}
            public PlacementResult Validate(PlacementRequest p,IReadOnlyList<PlacementRequest> all,BuildGrid g)=>p.Definition.Placement?.RequiresBuildArea!=true||host.HasBuildArea(g.Center(p.Cell,p.Footprint),p.Footprint)?PlacementResult.Ok:new PlacementResult("build-area","확정된 기지 범위 안에 설치하세요.");
        }
        bool IsMainDefinition(BuildingDefinition d)=>BaseRules&&RoleOf(d)==BaseRole.Main;
        public static BaseRole RoleOf(BuildingDefinition d){foreach(var m in d.Modules)if(m is BaseModuleDefinition identity)return identity.Role;return BaseRole.Legacy;}
        sealed class BasePlacementRule:IPlacementRule
        {
            readonly OpenWorldContent host;public BasePlacementRule(OpenWorldContent host){this.host=host;}
            public PlacementResult Validate(PlacementRequest p,IReadOnlyList<PlacementRequest> all,BuildGrid grid){
                if(!host.BaseRules)return PlacementResult.Ok;
                if(host.Defeated)return new PlacementResult("defeated","메인 기지가 파괴되어 공략이 종료되었습니다.");
                var role=RoleOf(p.Definition);if(role==BaseRole.Main&&host.FixedMainBase&&!host.installingMain)return new PlacementResult("fixed-main","메인 기지는 시작 위치에 고정됩니다.");if(role==BaseRole.Legacy)return PlacementResult.Ok;
                if(grid!=host.GroundWorld.Grid)return new PlacementResult("base-surface","기지는 지면에 설치하세요.");
                int installed=0,reserved=0;foreach(var b in host.GroundWorld.Buildings)if(b.Module<IBaseRole>()?.Role==role)installed++;
                bool included=false;foreach(var r in all){if(RoleOf(r.Definition)==role)reserved++;if(ReferenceEquals(r,p))included=true;}if(!included)reserved++;
                int limit=role==BaseRole.Main?1:host.SubLimit;
                if(installed+reserved>limit)return new PlacementResult("base-limit",role==BaseRole.Main?"메인 기지는 맵당 1개입니다.":$"서브 기지 설치 한도 {limit}개입니다.");
                return PlacementResult.Ok;
            }
        }
        void OnMainDestroyed(BuildingInstance b){Defeated=true;Failed?.Invoke();}
        sealed class UnlockRule:IPlacementRule
        {readonly OpenWorldContent host;public UnlockRule(OpenWorldContent host){this.host=host;}public PlacementResult Validate(PlacementRequest p,IReadOnlyList<PlacementRequest> all,BuildGrid g)=>(host.IsMainDefinition(p.Definition)||(p.Definition.Placement?.RequiredNexusLevel??1)<=host.LevelCap)?PlacementResult.Ok:new PlacementResult("locked","기지 레벨이 부족합니다.");}
        sealed class Factory:IBuildingFactory
        {
            readonly OpenWorldContent host;readonly IBuildingFactory legacy;readonly HashSet<BuildingInstance> common=new();
            readonly float cellSize;readonly Quaternion rotation;
            public Factory(OpenWorldContent host,IBuildingFactory legacy,BuildGrid grid=null){this.host=host;this.legacy=legacy;cellSize=grid?.CellSize??2;rotation=grid?.Rotation??WorldGridGeometry.Rotation;}
            public BuildingInstance Stage(int id,PlacementRequest request,Vector3 position)
            {
                if(request.Definition.Placement==null&&legacy!=null)return legacy.Stage(id,request,position);
                if(!host.IsMainDefinition(request.Definition)&&request.Definition.Placement.RequiredNexusLevel>host.LevelCap)throw new InvalidOperationException("기지 레벨이 부족합니다.");
                if(request.Definition.Placement.RequiresBuildArea&&!host.HasBuildArea(position,request.Footprint))throw new InvalidOperationException("기지 건설 범위를 벗어납니다.");
                if(legacy==null){if(!host.CheckGround(request.Cell,request.Footprint,out float h,out var reason))throw new InvalidOperationException(reason);position.y=h+.05f;}
                if(host.legacyDefinitions.Contains(request.Definition)){var restored=(legacy??host.GroundLegacyFactory).Stage(id,request,position);try{host.PrepareRestoredBuilding?.Invoke(restored);return restored;}catch{(legacy??host.GroundLegacyFactory).Remove(restored);restored.Dispose();throw;}}
                BuildingInstance b=null;GameObject view=null;
                try{b=new BuildingInstance(id,request.Definition,request.Cell,position,host.services);host.PrepareRestoredBuilding?.Invoke(b);view=host.installingMain&&host.IsMainDefinition(request.Definition)?host.authoredMainView:UnityEngine.Object.Instantiate(request.Definition.ViewPrefab,position,rotation,host.staging);view.SetActive(false);view.transform.SetPositionAndRotation(position,rotation);view.name=request.Definition.DisplayName;view.AddComponent<ContentBuildingView>().Bind(b,host.line,host.terrain,!host.ExternalConstruction);host.Views.Add(b,view);common.Add(b);return b;}
                catch{b?.Dispose();if(view!=null)UnityEngine.Object.Destroy(view);throw;}
            }
            public void Activate(BuildingInstance b){if(host.BaseRules&&b.Module<IBaseRole>()?.Role==BaseRole.Main){host.MainBase=b;b.Destroying+=host.OnMainDestroyed;}host.Bases.Register(b);host.BuildingTargets.Register(b,cellSize,rotation);if(common.Contains(b)){host.Views[b].transform.SetParent(host.parent,true);host.Views[b].SetActive(true);BuildingHitView.Attach(host.Views[b],b,host.line,cellSize,rotation);if(host.ExternalConstruction)OperationStatusView.Attach(host.Views[b],b,host.line);}else {var adapter=legacy??host.GroundLegacyFactory;adapter.Activate(b);if(adapter is EternalSteam.Demo.HordeTowerFactory factory&&factory.ViewOf(b) is GameObject view){BuildingHitView.Attach(view,b,host.line,cellSize,rotation);if(host.ExternalConstruction)OperationStatusView.Attach(view,b,host.line);}}}
            public void Remove(BuildingInstance b){if(ReferenceEquals(host.MainBase,b)){b.Destroying-=host.OnMainDestroyed;host.MainBase=null;}host.BuildingTargets.Remove(b);host.Bases.Remove(b);if(common.Remove(b)){if(host.Views.Remove(b,out var view)&&view!=null){view.SetActive(false);UnityEngine.Object.Destroy(view);}}else (legacy??host.GroundLegacyFactory)?.Remove(b);}
        }
    }
    public sealed class ContentBuildingView:MonoBehaviour
    {
        Terrain areaGround;float shownRadius=-1;
        BuildingInstance building;LineRenderer shot,buildArea;Transform aimPivot;float until;
        public void ShowBuildArea(bool visible){if(buildArea==null)return;buildArea.enabled=visible;if(visible&&building.Module<IBuildArea>() is IBuildArea area&&area.Radius!=shownRadius){shownRadius=area.Radius;for(int i=0;i<=96;i++){var p=area.Boundary(i/96f);p.y=(areaGround!=null?areaGround.SampleHeight(p)+areaGround.transform.position.y:p.y)+.3f;buildArea.SetPosition(i,p);}}}
        public void Bind(BuildingInstance b,Material material,Terrain ground=null,bool localGrid=true){areaGround=ground;building=b;shot=BuildingView.MakeLine("Attack",transform,material,.08f);shot.positionCount=2;shot.enabled=false;b.Shot+=OnShot;b.Projectile+=OnProjectile;
            if(b.Module<WeaponRuntime>()!=null){var model=transform.Find("Simple model");if(model!=null){aimPivot=new GameObject("Automatic turret aim").transform;aimPivot.SetParent(model,false);for(int i=model.childCount-1;i>=0;i--){var child=model.GetChild(i);if(child==aimPivot||child.name=="Armored plinth"||child.name=="Deck")continue;if(child.name=="Forward marker"){child.gameObject.SetActive(false);continue;}child.SetParent(aimPivot,true);}b.Aim+=OnAim;}}

            if(b.Module<IBuildArea>() is IBuildArea area){buildArea=BuildingView.MakeLine("Nexus construction radius",transform,material,.14f);buildArea.positionCount=97;buildArea.startColor=buildArea.endColor=Color.cyan;
                for(int i=0;i<=96;i++){var point=area.Boundary(i/96f)+Vector3.up*.2f;if(ground!=null)point.y=ground.SampleHeight(point)+ground.transform.position.y+.3f;buildArea.SetPosition(i,point);}buildArea.enabled=false;if(localGrid)NexusGridView.Create(transform,area is IBuildAreaGeometry geometry?geometry.Center:b.Position,area.Radius,material,ground);}
        }
        void OnAim(Vector3 p){var d=p-building.Position;d.y=0;if(aimPivot!=null&&d.sqrMagnitude>.000001f)aimPivot.rotation=Quaternion.LookRotation(d);}
        void OnShot(Vector3 p){shot.SetPosition(0,building.Position+Vector3.up);shot.SetPosition(1,p);until=Time.unscaledTime+.08f;shot.enabled=true;}
        void OnProjectile(Vector3 p){shot.SetPosition(0,p);shot.SetPosition(1,p+Vector3.up*.3f);until=Time.unscaledTime+.08f;shot.enabled=true;}
        void Update(){if(shot!=null)shot.enabled=Time.unscaledTime<until;}
        void OnDestroy(){if(building!=null){building.Shot-=OnShot;building.Aim-=OnAim;building.Projectile-=OnProjectile;}}
    }
}
