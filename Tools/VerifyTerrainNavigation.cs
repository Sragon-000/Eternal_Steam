using System;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.Demo;
using EternalSteam.OpenWorld;

// Regression injection reproduces the prior terrain-stuck crowd. Damage,
// power and elapsed time still come entirely from the normal simulation.
public static class VerifyTerrainNavigation
{
    sealed class MovementProbe:IEnemyMovementConstraint
    {
        public IEnemyMovementConstraint Inner;public TileWorldGround.TraversalSnapshot Grid;
        public int GroundSteps,IllegalSteps,TooFast;public float Distance;
        public Vector3 Constrain(int id,Vector3 from,Vector3 proposed)
        {
            var next=Inner.Constrain(id,from,proposed);GroundSteps++;
            if(!Grid.HasClearRoute(from,next))IllegalSteps++;
            float moved=Vector3.Distance(from,next);Distance+=moved;
            if(moved>Vector3.Distance(from,proposed)+.0001f)TooFast++;
            return next;
        }
    }
    public static string Regression()
    {
        if(!Application.isPlaying||Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")!="/tmp/eternal-first-loop-20260930")throw new Exception("Isolated Play required");
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        if(s.enabled||s.Content.InfiniteResources||s.Clock.Paused||s.Enemies.Alive!=0)throw new Exception("Prepared normal simulation required");
        var ground=s.Ground.GetComponent<TileWorldGround>();var point=new Vector3(76.7820969f,.911f,93.94279f);
        var target=s.Content.BuildingTargets.NearestNexus(point).Building.Position;
        if(!ground.Navigation.HasRoute(point,target))throw new Exception("Regression point must be terrain-connected");
        var tracked=new Dictionary<int,int>();int kills=0;
        Action<int> spawned=id=>tracked.Add(id,s.Enemies.Generation(id));
        Action<int,int> killed=(id,generation)=>{if(tracked.TryGetValue(id,out int expected)&&expected==generation)kills++;};
        var probe=new MovementProbe{Inner=s.Enemies.MovementConstraint,Grid=ground.CaptureTraversal()};
        int totalBefore=s.Enemies.Killed;float elapsed=0;long detours=0;int fields=0;
        s.Enemies.SpawnedEnemy+=spawned;s.Enemies.KilledEnemy+=killed;
        try{
            for(int i=0;i<33;i++)if(!s.Enemies.TrySpawn(point,target,2.4f,10))throw new Exception("Spawn failed");
            s.Enemies.SpawnedEnemy-=spawned;s.Enemies.MovementConstraint=probe;
            while(kills<33&&elapsed<200&&!s.Content.Defeated){s.AdvanceSimulation(.1f);elapsed+=.1f;}
            detours=s.EnemyAttacks.TerrainDetourSteps;fields=s.EnemyAttacks.TerrainFieldsBuilt;
            if(kills!=33||s.Enemies.Killed!=totalBefore+33||s.Enemies.Alive!=0||s.Content.Defeated)throw new Exception("Crowd still stuck: killed="+kills+" alive="+s.Enemies.Alive+" elapsed="+elapsed+" detours="+detours);
            if(probe.IllegalSteps!=0||probe.TooFast!=0||detours==0||fields>4)throw new Exception("Invalid movement or unshared fields");
            return JsonUtility.ToJson(new Result{passed=true,injectedGroundEnemies=33,naturalKills=kills,elapsedSeconds=elapsed,groundSteps=probe.GroundSteps,illegalTerrainSteps=probe.IllegalSteps,speedViolations=probe.TooFast,aggregateDistance=probe.Distance,sharedFields=fields,detourSteps=detours,mainHealth=s.Content.MainBase.Module<HealthModule>().Current},true);
        }finally{s.Enemies.SpawnedEnemy-=spawned;s.Enemies.KilledEnemy-=killed;s.Enemies.MovementConstraint=probe.Inner;}
    }
    [Serializable] sealed class Result {public bool passed;public int injectedGroundEnemies,naturalKills,groundSteps,illegalTerrainSteps,speedViolations,sharedFields;public float elapsedSeconds,aggregateDistance,mainHealth;public long detourSteps;}
}
