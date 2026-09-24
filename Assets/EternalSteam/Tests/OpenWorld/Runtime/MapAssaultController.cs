using System;
using EternalSteam.Demo;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // Composition adapter: energy, point selection and enemy simulation own separate state.
    public sealed class MapAssaultController:IDisposable
    {
        readonly OpenWorldSandbox sandbox;
        readonly MapAssaultSettings settings;
        readonly NexusDestinationQuery destinations;
        readonly EnemySpawnLayout layout;
        readonly int[] fixedPoints=new int[8];
        readonly int[] rewardGeneration;
        [Saved(0)] double extractionTime,spawnTime;
        [Saved(0)] int cursor;int bossId=-1,bossGeneration;
        [Saved] bool bossSpawned;
        readonly SpawnAreaValidator spawnAreas;
        public string BossSpawnFailure {get;private set;}
        public Vector3 BossPosition=>settings.BossPosition;
        [Saved] MapStage previousStage;
        public MapEnergyState Energy {get;}
        public MapCoverage Coverage {get;}
        public NightSpawnPlanner Planner {get;}
        public string EnergyName=>settings.EnergyName;
        public int BossId=>bossId;
        public int BossGeneration=>bossGeneration;
        public int BossHealth=>bossId>=0&&sandbox.Enemies.Generation(bossId)==bossGeneration&&sandbox.Enemies.GetEnemy(bossId).alive?sandbox.Enemies.GetEnemy(bossId).health:0;
        public MapAssaultController(OpenWorldSandbox sandbox,MapAssaultSettings definition)
        {
            definition.ValidateConfiguration();this.sandbox=sandbox;settings=UnityEngine.Object.Instantiate(definition);
            spawnAreas=new SpawnAreaValidator(sandbox.Ground,sandbox.Content);sandbox.Content.ReservedBossCenter=settings.BossPosition;sandbox.Content.ReservedBossFootprint=settings.BossSpawnSize;
            destinations=new NexusDestinationQuery(sandbox.Content.GroundWorld,true);
            Coverage=new MapCoverage(sandbox.Content,sandbox.Ground,destinations);
            Energy=new MapEnergyState(settings.MapId,settings.Target,Coverage.Centers.Length,settings.EnergyPerCell);
            Planner=new NightSpawnPlanner(Coverage.Centers.Length,(uint)settings.Seed);
            for(int i=0;i<8;i++)fixedPoints[i]=Coverage.NearestCell(settings.BossWavePoints[i]);
            layout=new EnemySpawnLayout(sandbox.Enemies.MaxCount);rewardGeneration=new int[sandbox.Enemies.MaxCount];Array.Fill(rewardGeneration,-1);
            sandbox.Enemies.KilledEnemy+=OnKilled;
        }
        public void Tick(double seconds)
        {
            if(sandbox.Content.Defeated){Energy.Fail();Planner.Stop();return;}
            if(!double.IsFinite(seconds)||seconds<=0)return;
            bool changed=Coverage.Refresh();bool night=sandbox.Clock.Phase==DayPhase.Night;
            if(sandbox.Content.MainBase==null)return;
            extractionTime+=seconds;
            if(extractionTime>=1){long periods=(long)Math.Min(extractionTime,3600);extractionTime-=periods;
                if(Energy.Stage==MapStage.Gathering)for(int i=0;i<Coverage.Centers.Length;i++)if(Coverage.Covered[i])Energy.Extract(i,periods*settings.ExtractionPerSecond);
            }
            Energy.Advance(night);
            bool active=Energy.Stage is MapStage.Gathering or MapStage.WaitingForNight or MapStage.BossBattle or MapStage.AwaitingCraft;
            if(!active){Planner.Stop();return;}
            bool newNight=night&&Planner.LastBudgetDay<sandbox.Clock.Day;
            if(newNight){long day=sandbox.Clock.Day-1;long extra=settings.AdditionalPerDay==0?0:day>(long.MaxValue-settings.FirstNightCount)/Math.Max(1,settings.AdditionalPerDay)?long.MaxValue-settings.FirstNightCount:day*settings.AdditionalPerDay;Planner.BeginNight(sandbox.Clock.Day,settings.FirstNightCount+extra);}
            if(newNight||changed||previousStage!=Energy.Stage)Planner.Reconcile(Coverage.Eligible,Energy.Stage==MapStage.BossBattle?8:Coverage.BaseCount+1,Energy.Stage==MapStage.BossBattle?fixedPoints:null);
            previousStage=Energy.Stage;
            if(Energy.Stage==MapStage.BossBattle&&!bossSpawned)TryBoss();
            // Verification policy: residual enemies survive dawn; pending normal budget waits for night.
            if(!night||Planner.Pending==0||Planner.Points.Count==0||sandbox.Enemies.FreeCount==0)return;
            spawnTime+=seconds;if(spawnTime<settings.SpawnInterval)return;spawnTime%=settings.SpawnInterval;
            destinations.Refresh();layout.Begin(sandbox.Enemies,sandbox.EnemySpawnSpacing);
            int count=Planner.Points.Count;
            for(int n=0;n<count&&Planner.Pending>0&&sandbox.Enemies.FreeCount>0;n++){
                int cell=Planner.Points[cursor%count];cursor=(cursor+1)%count;var p=Coverage.Centers[cell];
                if(!Coverage.Eligible[cell]||!spawnAreas.Check(p,1,out _)||!layout.IsFree(p)||!destinations.TryNearest(p,out var target))continue;
                if(sandbox.Enemies.TrySpawn(p,target,2.4f,sandbox.VerificationEnemyHealth)){layout.Add(p);Planner.Consume();}
            }
        }
        void TryBoss()
        {
            if(!spawnAreas.Check(settings.BossPosition,settings.BossSpawnSize,out var reason)){BossSpawnFailure=reason;return;}BossSpawnFailure=null;
            destinations.Refresh();if(!destinations.TryNearest(settings.BossPosition,out var target)||sandbox.Enemies.FreeCount==0)return;
            // Airborne verification boss ignores safety, but its complete spawn area must be clear.
            sandbox.Enemies.SpawnedEnemy+=CaptureBoss;
            bool spawned=sandbox.Enemies.TrySpawn(settings.BossPosition,target,1,settings.BossHealth,true);
            sandbox.Enemies.SpawnedEnemy-=CaptureBoss;bossSpawned=spawned;
        }
        void CaptureBoss(int id){bossId=id;bossGeneration=sandbox.Enemies.Generation(id);}
        void OnKilled(int id,int generation)
        {
            if(rewardGeneration[id]==generation)return;rewardGeneration[id]=generation;
            if(sandbox.Content.Defeated){Energy.Fail();return;}
            if(id==bossId&&generation==bossGeneration){Energy.DefeatBoss();bossId=-1;}
            else {Energy.RewardKill(settings.KillEnergy);Energy.Advance(sandbox.Clock.Phase==DayPhase.Night);}
        }
        public bool Craft(){if(sandbox.Content.Defeated)return false;bool result=Energy.Craft(settings.MapId);if(result){Planner.Stop();sandbox.SpawnStream.Reset();}return result;}
        public void Dispose(){sandbox.Enemies.KilledEnemy-=OnKilled;UnityEngine.Object.Destroy(settings);}
    }
}
