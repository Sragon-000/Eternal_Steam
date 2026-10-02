using System;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;

public static class VerifyQa05Map
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static OpenWorldSandbox World()
    {
        Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")=="/tmp/eternal-qa05-map","Isolated Play required");
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        Check(s!=null&&s.Assault!=null&&s.Persistence!=null&&!s.Persistence.Blocked,"Map assault unavailable");
        s.Persistence.Automatic=false;s.Clock.Paused=true;Application.runInBackground=true;return s;
    }
    public static string Baseline()
    {
        var s=World();var ground=s.Ground.GetComponent<TileWorldGround>();var coverage=s.Assault.Coverage;
        Check(ground!=null&&ground.Width==500&&ground.Height==500&&ground.CellSize==1&&ground.PlayableCells.Length==250000,"500x500 tile source changed");
        Check(ground.Width>=150&&ground.Height>=150,"Below documented minimum map size");
        int playable=ground.PlayableCells.Count(cell=>cell!=0);
        Check(playable>0&&playable<ground.PlayableCells.Length,"Playable map mask invalid");
        coverage.Refresh();int eligible=0,covered=0;
        var spawn=new SpawnAreaValidator(s.Ground,s.Content);
        for(int i=0;i<coverage.Centers.Length;i++){
            if(coverage.Covered[i])covered++;
            if(!coverage.Eligible[i])continue;
            eligible++;
            var p=coverage.Centers[i];
            Check(ground.IsPlayable(p)&&!coverage.Covered[i]&&!s.Content.ReservedSpawnOverlap(p,Vector2Int.one)&&spawn.Check(p,1,out _),$"Invalid eligible cell {i}: {p}");
        }
        Check(eligible>0&&coverage.BaseCount==1,"Main-only spawn candidates unavailable");
        var fixedCells=s.AssaultSettings.BossWavePoints.Select(point=>coverage.NearestCell(point)).ToArray();
        Check(fixedCells.Length==8&&fixedCells.Distinct().Count()==8,"Authored boss wave cells overlap");
        Check(fixedCells.All(i=>coverage.Eligible[i]),"All eight authored boss wave points should be eligible before any safe-area expansion");
        Check(spawn.Check(s.AssaultSettings.BossPosition,s.AssaultSettings.BossSpawnSize,out var reason),"Authored boss footprint invalid: "+reason);
        return $"PASS map: tiles=500x500, tile playable={playable}, logical={coverage.Centers.Length}, eligible={eligible}, covered={covered}, bossFixedEligible={fixedCells.Count(i=>coverage.Eligible[i])}/8";
    }
    public static string InspectBoss()
    {
        var s=World();var coverage=s.Assault.Coverage;var settings=s.AssaultSettings;
        var spawn=new SpawnAreaValidator(s.Ground,s.Content);
        var details=new System.Text.StringBuilder();
        details.Append("asset=").Append(UnityEditor.AssetDatabase.GetAssetPath(settings));
        details.Append(" boss=").Append(settings.BossPosition);
        details.Append(" bossValid=").Append(spawn.Check(settings.BossPosition,settings.BossSpawnSize,out var reason));
        details.Append(" reason=").Append(reason);
        for(int i=0;i<settings.BossWavePoints.Length;i++){
            int cell=coverage.NearestCell(settings.BossWavePoints[i]);
            details.Append(" wave").Append(i).Append('=').Append(settings.BossWavePoints[i]).Append(" cell=").Append(cell).Append(" eligible=").Append(coverage.Eligible[cell]);
        }
        var nearest=coverage.Centers.OrderBy(p=>(p-settings.BossPosition).sqrMagnitude).Take(15000);
        foreach(var p in nearest)if(spawn.Check(p,settings.BossSpawnSize,out _)){
            details.Append(" nearestValidBoss=").Append(p).Append(" distance=").Append(Vector3.Distance(p,settings.BossPosition).ToString("0.##"));break;
        }
        foreach(var p in coverage.Centers.OrderBy(p=>(p-settings.BossPosition).sqrMagnitude).Take(15000))if(spawn.Check(p,new Vector2Int(5,5),out _)){
            details.Append(" clear5x5Boss=").Append(p).Append(" distance=").Append(Vector3.Distance(p,settings.BossPosition).ToString("0.##"));break;
        }
        var chosenBoss=new Vector3(79.20f,.36f,-156.98f);
        foreach(var i in Enumerable.Range(0,coverage.Centers.Length).OrderBy(i=>(coverage.Centers[i]-settings.BossWavePoints[0]).sqrMagnitude).Take(15000)){
            var p=coverage.Centers[i];
            if(!coverage.Eligible[i]||Vector3.Distance(p,chosenBoss)<10||SpawnAreaValidator.Overlaps(p,1,chosenBoss,settings.BossSpawnSize))continue;
            details.Append(" nearestValidWave0=").Append(p).Append(" bossDistance=").Append(Vector3.Distance(p,chosenBoss).ToString("0.##"));break;
        }
        return details.ToString();
    }
    public static string InspectWaves()
    {
        var s=World();var coverage=s.Assault.Coverage;var settings=s.AssaultSettings;
        var ground=s.Ground.GetComponent<TileWorldGround>();var spawn=new SpawnAreaValidator(s.Ground,s.Content);
        var details=new System.Text.StringBuilder();
        for(int n=0;n<settings.BossWavePoints.Length;n++){
            var authored=settings.BossWavePoints[n];int cell=coverage.NearestCell(authored);var point=coverage.Centers[cell];
            bool spawnValid=spawn.Check(point,1,out var reason);
            details.Append("wave").Append(n).Append(" authored=").Append(authored)
                .Append(" nearest=").Append(point).Append(" eligible=").Append(coverage.Eligible[cell])
                .Append(" tile=").Append(ground.IsPlayable(point)).Append(" spawn=").Append(spawnValid)
                .Append(" bossOverlap=").Append(s.Content.ReservedSpawnOverlap(point,Vector2Int.one))
                .Append(" reason=").Append(reason);
            if(coverage.Eligible[cell])continue;
            int best=Enumerable.Range(0,coverage.Centers.Length).Where(i=>coverage.Eligible[i])
                .OrderBy(i=>(coverage.Centers[i]-authored).sqrMagnitude).First();
            details.Append(" nearestEligible=").Append(coverage.Centers[best])
                .Append(" move=").Append(Vector3.Distance(coverage.Centers[best],authored).ToString("0.##"));
        }
        return details.ToString();
    }
    public static string Flow()
    {
        var s=World();var assault=s.Assault;var coverage=assault.Coverage;var energy=assault.Energy;var planner=assault.Planner;
        Check(energy.Stage==MapStage.Gathering&&planner.Pending==0,"Fresh map required");
        s.Clock.SetPhase(DayPhase.Night);assault.Tick(.001);
        Check(planner.Points.Count==2&&planner.Points.Distinct().Count()==2&&planner.Points.All(i=>coverage.Eligible[i]),"Main-only night points invalid");
        long initialBudget=planner.Pending;Check(initialBudget==s.AssaultSettings.FirstNightCount,"First-night budget mismatch");
        int retained=planner.Points[0];
        var target=s.Content.MainBase.Position;
        Check(s.Enemies.TrySpawn(coverage.Centers[retained],target,1,1000),"Could not create residual night enemy");
        int residual=s.Enemies.Alive;
        s.Clock.SetPhase(DayPhase.Day);assault.Tick(.001);
        Check(s.Enemies.Alive==residual&&planner.Pending==initialBudget&&planner.Points.Contains(retained),"Dawn lost an enemy, pending budget or valid point");
        s.Clock.SetPhase(DayPhase.Night);assault.Tick(.001);
        Check(planner.Pending==initialBudget&&planner.Points.Contains(retained),"Same-day phase toggle duplicated the night budget");
        s.Clock.SetPhase(DayPhase.Day);
        long toLimit=energy.PreBossLimit-energy.Earned;
        Check(energy.RewardKill(toLimit-1)==toLimit-1,"90 percent boundary preparation failed");
        assault.Tick(.001);Check(energy.Stage==MapStage.Gathering,"Boss started below 90 percent");
        Check(energy.RewardKill(2)==1&&energy.Earned==energy.PreBossLimit,"90 percent cap failed");
        assault.Tick(.001);Check(energy.Stage==MapStage.WaitingForNight&&assault.BossId<0,"Day threshold did not wait for night");
        s.Clock.SetPhase(DayPhase.Night);assault.Tick(.001);
        Check(energy.Stage==MapStage.BossBattle&&assault.BossId>=0,"Night boss did not spawn");
        var fixedCells=s.AssaultSettings.BossWavePoints.Select(point=>coverage.NearestCell(point)).ToArray();
        Check(planner.Points.Count==8&&planner.Points.Distinct().Count()==8&&planner.Points.All(i=>coverage.Eligible[i]),"Boss wave needs eight unique eligible points");
        foreach(var i in fixedCells)if(coverage.Eligible[i])Check(planner.Points.Contains(i),"Eligible fixed boss-wave point was replaced");
        int boss=assault.BossId;s.Enemies.ApplyDamage(boss,int.MaxValue);
        Check(energy.Stage==MapStage.AwaitingCraft&&energy.Earned==energy.Target&&energy.BossReward==energy.Target/10,"Boss reward not exactly ten percent");
        Check(!energy.DefeatBoss()&&energy.RewardKill(100)==0,"Duplicate boss/kill reward accepted");
        Check(assault.Craft()&&!assault.Craft()&&energy.Stage==MapStage.Cleared,"Orb craft not one-time or map not cleared");
        assault.Tick(.001);Check(planner.Pending==0&&planner.Points.Count==0,"Cleared map retains normal spawn budget");
        return $"PASS flow: first-night points=2, budget={initialBudget}, dawn residual={residual}, day 90% waits, boss wave=8, reward 10%, cleared spawn=0";
    }
    public static string SubArea()
    {
        var s=World();var assault=s.Assault;var coverage=assault.Coverage;var planner=assault.Planner;
        Check(s.Content.Bases.Bases.Count==1&&assault.Energy.Stage==MapStage.Gathering,"Fresh main-only map required");
        s.Clock.SetPhase(DayPhase.Night);assault.Tick(.001);
        Check(planner.Points.Count==2,"Main-only map needs two points");
        long budget=planner.Pending;var original=planner.Points.ToArray();
        var subDefinition=s.ContentCatalog.Buildings.Single(definition=>definition.Id=="installation.nexus");
        BuildingInstance sub=null;
        for(int z=20;z>=12&&sub==null;z--)for(int x=-5;x<10&&sub==null;x++)
            if(s.Content.GroundPlacement.Add(subDefinition,new Vector2Int(x,z),out _).Success){
                var result=s.Content.GroundPlacement.Confirm();Check(result.Success,result.Message);sub=s.Content.GroundWorld.Buildings.Last();
            }
        Check(sub!=null,"Could not place QA sub-base");assault.Tick(.001);
        Check(coverage.BaseCount==2&&planner.Points.Count==3&&planner.Points.Distinct().Count()==3&&planner.Points.All(i=>coverage.Eligible[i]),"Two-base night needs three unique eligible points");
        foreach(var i in original)if(coverage.Eligible[i])Check(planner.Points.Contains(i),"Valid night point was replaced after sub-base installation");
        Check(planner.Pending==budget,"Sub-base installation changed today's enemy budget");
        sub.Module<HealthModule>().ApplyDamage(100000);assault.Tick(.001);
        Check(sub.Disposed&&coverage.BaseCount==1&&planner.Points.Count==2&&planner.Points.Distinct().Count()==2&&planner.Points.All(i=>coverage.Eligible[i]),"Sub-base loss did not restore two valid points");
        Check(planner.Pending==budget,"Sub-base loss reduced today's enemy budget");
        return $"PASS night area changes: main points=2, main+sub=3, after loss=2, remaining budget={budget}";
    }
    public static string NightThreshold()
    {
        var s=World();var assault=s.Assault;var energy=assault.Energy;
        Check(s.Clock.Phase==DayPhase.Night&&energy.Stage==MapStage.Gathering&&assault.BossId<0,"Active pre-boss night required");
        long remaining=energy.PreBossLimit-energy.Earned;
        Check(energy.RewardKill(remaining+1000)==remaining&&energy.Earned==energy.PreBossLimit,"Night 90 percent cap failed");
        assault.Tick(.001);
        Check(energy.Stage==MapStage.BossBattle&&assault.BossId>=0,"Night threshold should spawn boss immediately");
        int boss=assault.BossId;s.Enemies.ApplyDamage(boss,int.MaxValue);
        Check(energy.Stage==MapStage.AwaitingCraft&&energy.Earned==energy.Target&&energy.BossReward==energy.Target/10,"Night boss reward wrong");
        return "PASS night threshold: immediate boss, over-cap kill reward clamped to 90%, boss reward exactly 10%";
    }
    public static string PlannerDistribution()
    {
        var eligible=new[]{true,true,true,true,false};int[] counts=new int[4];
        for(uint seed=1;seed<=10000;seed++){
            var planner=new NightSpawnPlanner(eligible.Length,seed);planner.Reconcile(eligible,1);
            Check(planner.Points.Count==1&&planner.Points[0]<4,"Planner selected an ineligible cell");
            counts[planner.Points[0]]++;
        }
        Check(counts.All(count=>count>2000&&count<3000),"Candidate selection distribution is unexpectedly skewed: "+string.Join(",",counts));
        var fixedPlanner=new NightSpawnPlanner(eligible.Length,42);fixedPlanner.BeginNight(1,100);fixedPlanner.Reconcile(eligible,3);
        Check(fixedPlanner.Points.Count==3&&fixedPlanner.Points.Distinct().Count()==3,"Planner sampled duplicate candidates");
        int original=fixedPlanner.Points[0];eligible[original]=false;fixedPlanner.Reconcile(eligible,3);
        Check(fixedPlanner.Pending==100&&!fixedPlanner.Points.Contains(original)&&fixedPlanner.Points.Count==3,"Invalid point removal changed budget or count");
        var bossPlanner=new NightSpawnPlanner(5,73);var bossEligible=new[]{true,true,true,true,true};
        bossPlanner.BeginNight(1,50);bossPlanner.Reconcile(bossEligible,4,new[]{0,1,2,3});
        Check(bossPlanner.Points.OrderBy(i=>i).SequenceEqual(new[]{0,1,2,3}),"Fixed boss points not all selected");
        bossEligible[1]=false;bossPlanner.Reconcile(bossEligible,4,new[]{0,1,2,3});
        Check(bossPlanner.Points.Contains(4)&&!bossPlanner.Points.Contains(1)&&bossPlanner.Pending==50,"Unsafe fixed point not replaced without budget loss");
        bossEligible[1]=true;bossPlanner.Reconcile(bossEligible,4,new[]{0,1,2,3});
        Check(bossPlanner.Points.OrderBy(i=>i).SequenceEqual(new[]{0,1,2,3}),"Restored fixed point did not displace its substitute");
        return "PASS candidate distribution across 10,000 fixed seeds: "+string.Join(",",counts)+"; no duplicates, budget retained, fixed-point replacement/restoration";
    }
}
