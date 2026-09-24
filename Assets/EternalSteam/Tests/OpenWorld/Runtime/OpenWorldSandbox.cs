using System;
using System.Collections.Generic;
using EternalSteam.Demo;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    public sealed class OpenWorldSandbox : MonoBehaviour
    {
        public bool SceneAuthored;
        public SceneStartingBase StartingBase;
        public EnemyCombatSettings EnemyCombat;
        public EnemyBuildingCombat EnemyAttacks {get;private set;}
        public bool MeetingConstructionRules;
        public bool BasePlanningRules;
        public PowerModuleDefinition LegacyPower;
        public SingleMapPersistence Persistence {get;private set;}
        public CampaignProgression Campaign {get;private set;}
        public MapAssaultSettings AssaultSettings;
        public MapAssaultController Assault {get;private set;}
        public WorldGridView WorldGrid {get;private set;}
        public RegionDefinition[] Regions;
        public BuildingCatalog ContentCatalog;
        [Range(1,8)] public int VerificationNexusLevel=8;
        public OpenWorldContent Content {get;private set;}
        public HordeTargetAdapter TargetAdapter {get;private set;}
        public GameObject FoundationPrefab;
        public GameObject[] TowerPrefabs;
        public Terrain Ground;
        public FreeCameraRig CameraRig;
        public Mesh EnemyMesh;
        public Material EnemyMaterial, TowerMaterial, BarrelMaterial, LineMaterial, ValidMaterial, InvalidMaterial, FoundationMaterial;
        public FoundationPlacement Foundations { get; private set; }
        public HordeEnemyWorld Enemies { get; private set; }
        public readonly List<HordeTower> Towers=new();
        [Header("시간 검증 설정 (초)")]
        [Min(1)] public float DayDurationSeconds=300;
        [Min(1)] public float NightDurationSeconds=180;
        public GameClock Clock {get;private set;}
        public bool Running;
        public bool SpawnAir;
        [Min(.5f)] public float EnemySpawnSpacing=.75f;
        [Min(1)] public int VerificationEnemyHealth=10;
        public EnemySpawnStream SpawnStream { get; private set; }
        public string Message="수정 → 설치 → 기지를 먼저 배치하고 확정하세요.";
        OpenWorldInput input;
        HordeTowerCombat combat;
        HordeEnemyRenderer renderer;
        readonly Material[] types=new Material[4],effects=new Material[4];
        void Awake()
        {
            Clock=new GameClock(DayDurationSeconds,NightDurationSeconds);
            input=GetComponent<OpenWorldInput>();
            Enemies=new HordeEnemyWorld(p=>Ground.SampleHeight(p)+Ground.transform.position.y+.55f,false,EnemySpawnStream.ActiveLimit,new Rect(Ground.transform.position.x,Ground.transform.position.z,Ground.terrainData.size.x,Ground.terrainData.size.z));
            Enemies.ArrivalPolicy=EnemyArrivalPolicy.Remain;
            TargetAdapter=new HordeTargetAdapter(Enemies);
            Campaign=new CampaignProgression();
            Content=new OpenWorldContent(Ground,transform,LineMaterial,TargetAdapter,VerificationNexusLevel,MeetingConstructionRules,BasePlanningRules,Campaign);
            Content.LegacyPower=LegacyPower;
            if(ContentCatalog!=null){var errors=ContentCatalog.Validate();if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
                foreach(var definition in ContentCatalog.Buildings)foreach(var module in definition.Modules)if(module is ProductionModuleDefinition production)Content.Resources.AddCapacity(production.OutputId,1000000);
            }
            for(int i=0;i<4;i++) {
                types[i]=new Material(TowerMaterial);types[i].SetColor("_BaseColor",HordeTowerStats.Color((HordeTowerKind)i));
                effects[i]=new Material(LineMaterial);effects[i].SetColor("_BaseColor",HordeTowerStats.Color((HordeTowerKind)i));
            }
            Foundations=new FoundationPlacement(Ground,transform,Towers,FoundationMaterial,BarrelMaterial,LineMaterial,ValidMaterial,types,effects,FoundationPrefab,TowerPrefabs,Content);
            Content.Attach(Foundations);Foundations.AttachGround();
            if(MeetingConstructionRules)WorldGrid=WorldGridView.Create(transform,CameraRig,Ground,LineMaterial);
            if(SceneAuthored) {
                foreach(var platform in GetComponentsInChildren<SceneFoundation>())Foundations.Adopt(platform);
                foreach(var tower in GetComponentsInChildren<SceneTower>())Foundations.Adopt(tower);
                Message="수정 → 설치 → 기지 확정 → 범위 안 격자 클릭 → 확정 (자동 조준, 토대 선택 사항)";
            }
            if(MeetingConstructionRules)Message="외부 설치 가능 · 기지/전초 범위 밖 건물은 비작동 · 기지 클릭으로 소속 기지 선택";
            SpawnStream=new EnemySpawnStream(Enemies,new NexusDestinationQuery(Content.GroundWorld,MeetingConstructionRules),Ground,EnemySpawnSpacing,VerificationEnemyHealth,false,Content.BuildingTargets);
            EnemyAttacks=new EnemyBuildingCombat(Enemies,Content.BuildingTargets,TargetAdapter,EnemyCombat,Ground.GetComponent<TileWorldGround>());
            combat=new HordeTowerCombat(Towers,Enemies,new HordeAttackResolver(Enemies),true,true);
            if(LegacyPower!=null)Message="메인 기지 → 발전기 → 포탑 설치 · 전력이 부족하면 공격이 중단됩니다.";
            if(AssaultSettings!=null)Assault=new MapAssaultController(this,AssaultSettings);
            if(StartingBase!=null&&BasePlanningRules&&Assault!=null){Persistence=new SingleMapPersistence(this);Persistence.Initialize();Running=true;}
            else if(StartingBase!=null){var installed=Content.InstallStartingBase(StartingBase);if(!installed.Success)throw new InvalidOperationException("시작 기지 설치 실패: "+installed.Message);Content.Bases.Refresh();Running=true;Message="메인 기지 준비 완료 · 정면 구역에 발전기와 방어 건물을 배치하세요.";}
            var size=Ground.terrainData.size;
            renderer=new HordeEnemyRenderer(Enemies,EnemyMesh,EnemyMaterial,new Bounds(Ground.transform.position+size*.5f,size+Vector3.up*20));
        }
        public bool Spawn(string amount)
        {
            if(Persistence?.Blocked==true)return false;
            if(Content.Defeated){Message="메인 기지가 파괴되어 공략이 종료되었습니다.";return false;}
            if(GetComponent<OpenWorldInput>() is OpenWorldInput input && input.IsEditing){Message="편집을 확정하거나 취소한 뒤 소환하세요.";return false;}
            if(!int.TryParse(amount,out int count) || count<1 || count>EnemySpawnStream.MaximumRequest) {Message="소환 수량은 1~100,000,000 사이의 정수로 입력하세요.";return false;}
            if(!SpawnStream.HasDestination){Message="먼저 기지를 설치하고 확정하세요. 적은 설치된 기지로 이동합니다.";return false;}
            if(!SpawnStream.Request(count,SpawnAir)){Message="초기화 전 누적 소환 요청은 최대 1억 마리입니다. 적 초기화 후 다시 요청하세요.";return false;}
            SpawnStream.Pump();
            Running=true;Message=$"{count:N0}마리 소환 요청 — 동시 최대 10만, 생성 간격 {EnemySpawnSpacing:0.##}m, 가까운 기지로 이동합니다.";return true;
        }
        public void ResetEnemies(){if(Assault!=null){Message="맵 공략 중 적 초기화는 보상/보스 상태를 손상시킬 수 있어 제한됩니다. Play를 다시 시작하세요.";return;}SpawnStream.Reset();EnemyAttacks.Reset();TargetAdapter.Reset();Enemies.Reset();combat.Reset();Running=false;Message="적과 소환 대기를 초기화했습니다. 토대와 포탑은 유지됩니다.";}

        [Saved(0)] double pendingTime;
        public double PendingSimulationTime {get=>pendingTime;set=>pendingTime=value;}
        public bool SimulationPaused=>(input!=null&&input.IsEditing)||Clock.Paused;
        void Start(){if(Persistence!=null&&Persistence.LastSavedUtc==null&&!Persistence.Blocked)Persistence.RequestAutoSave();}
        void Update()
        {
            AdvanceSimulation(Time.deltaTime);
            Persistence?.Tick(Time.unscaledDeltaTime);
            renderer.Draw();
        }
        // One time source for extraction, generation, production, movement and damage.
        // Bounded catch-up retains outstanding time instead of silently dropping it.
        public void AdvanceSimulation(float seconds)
        {
            if(Persistence?.Blocked==true)return;
            if(Content.Defeated){FinishFailure();return;}
            if(SimulationPaused)return;
            if(!float.IsFinite(seconds)||seconds<=0)return;
            Persistence?.Changed();pendingTime+=seconds;
            for(int steps=0;pendingTime>0&&steps<20;steps++){
                float dt=(float)Math.Min(pendingTime,.1);pendingTime=Math.Max(0,pendingTime-dt);
                Clock.Tick(dt);Content.Bases.Refresh();Content.Power.Tick(dt);
                Assault?.Tick(dt);
                if(Assault!=null){Running=true;renderer.HighlightId=Assault.BossId;renderer.HighlightGeneration=Assault.BossGeneration;}
                if(Running){SpawnStream.Pump();TargetAdapter.Tick(dt);EnemyAttacks.Prepare(dt);Enemies.MoveAndIndex(dt);EnemyAttacks.Attack(dt);
                    if(Content.Defeated){FinishFailure();break;}
                    Content.Bases.Refresh();combat.Update(dt,true,int.MaxValue);Content.Tick(dt);
                }else Content.TickProduction(dt);
            }
        }
        void FinishFailure()
        {
            Persistence?.FailNow();Assault?.Tick(.001);if(input!=null&&input.IsEditing)input.Cancel();Running=false;Clock.Paused=true;pendingTime=0;Message="메인 기지 파괴 · 공략 실패";
        }
        void OnDestroy()
        {
            Assault?.Dispose();EnemyAttacks?.Dispose();Foundations?.Dispose();Content?.Dispose();Foundations?.GroundPlatform?.Factory?.Dispose();TargetAdapter?.Dispose();renderer?.Dispose();
            foreach(var m in types)if(m!=null)Destroy(m);foreach(var m in effects)if(m!=null)Destroy(m);
        }
    }
}
