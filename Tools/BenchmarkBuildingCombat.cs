using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Unity.Profiling;
using EternalSteam;
using EternalSteam.Demo;
using EternalSteam.OpenWorld;
public static class BenchmarkBuildingCombat
{
 public static string Run1()=>Measure(4000);
 public static string Run2()=>Measure(10000);
 public static string Run3()=>Measure(100000);
 public static string Main()=>Measure(4000);
 static string Measure(int population){
  var report=new StringBuilder("CPU only, flat terrain, no rendering/UI; 101 static buildings, mixed ground/air; 30 warmup + 60 frames, dt=1/60. GC.Alloc marker event count (NOT duration as bytes).\n");
  using(var probe=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC.Alloc",16,ProfilerRecorderOptions.StartImmediately|ProfilerRecorderOptions.CollectOnlyOnCurrentThread)){
   var allocation=new byte[16384];GC.KeepAlive(allocation);probe.Stop();if(!probe.Valid||probe.Count==0)throw new Exception("GC.Alloc positive control failed");report.AppendLine("GC.Alloc positive control: "+probe.Count+" event(s)");
  }
  for(int repeat=1;repeat<=3;repeat++){
   int count=population;
   var registry=new BuildingTargetRegistry();var assets=new List<UnityEngine.Object>();var buildings=new List<BuildingInstance>();
   var health=ScriptableObject.CreateInstance<HealthModuleDefinition>();health.Maximum=1e9f;assets.Add(health);
   for(int i=0;i<101;i++){
    var d=ScriptableObject.CreateInstance<BuildingDefinition>();var body=ScriptableObject.CreateInstance<BuildingCombatDefinition>();assets.Add(d);assets.Add(body);d.Footprint=Vector2Int.one;body.Role=i==100?BuildingCombatRole.Nexus:i%3==0?BuildingCombatRole.Wall:i%3==1?BuildingCombatRole.Defense:BuildingCombatRole.General;d.Modules.Add(health);d.Modules.Add(body);
    var b=new BuildingInstance(i,d,default,i==100?Vector3.zero:new Vector3((i%10)*16-72,0,(i/10)*16-72),new BuildingServices(null));b.Activate();registry.Register(b,2,Quaternion.Euler(0,45,0));buildings.Add(b);
   }
   var world=new HordeEnemyWorld(null,false,count,new Rect(-200,-200,400,400)){ArrivalPolicy=EnemyArrivalPolicy.Remain};
   using(var effects=new HordeTargetAdapter(world))using(var combat=new EnemyBuildingCombat(world,registry,effects,null)){
    int side=Mathf.CeilToInt(Mathf.Sqrt(count));for(int i=0;i<count;i++){var p=new Vector3((i%side-side*.5f)*.75f,0,(i/side-side*.5f)*.75f);world.TrySpawn(p,Vector3.zero,2.4f,1000,i%2==0);}
    const float dt=1f/60;for(int i=0;i<30;i++){effects.Tick(dt);combat.Prepare(dt);world.MoveAndIndex(dt);combat.Attack(dt);}
    var prepare=new double[60];var move=new double[60];var attack=new double[60];var total=new double[60];var timer=new Stopwatch();int gc=GC.CollectionCount(0);int allocEvents;
    using(var alloc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC.Alloc",65536,ProfilerRecorderOptions.StartImmediately|ProfilerRecorderOptions.CollectOnlyOnCurrentThread)){
     for(int i=0;i<60;i++){
      timer.Restart();effects.Tick(dt);combat.Prepare(dt);timer.Stop();prepare[i]=timer.Elapsed.TotalMilliseconds;
      timer.Restart();world.MoveAndIndex(dt);timer.Stop();move[i]=timer.Elapsed.TotalMilliseconds;
      timer.Restart();combat.Attack(dt);timer.Stop();attack[i]=timer.Elapsed.TotalMilliseconds;total[i]=prepare[i]+move[i]+attack[i];
     }
     alloc.Stop();allocEvents=alloc.Count;
    }
    Array.Sort(prepare);Array.Sort(move);Array.Sort(attack);Array.Sort(total);
    report.AppendLine($"run {repeat}, {count}: prepare {prepare[30]:F2}/{prepare[57]:F2}, move+sweep {move[30]:F2}/{move[57]:F2}, attack {attack[30]:F2}/{attack[57]:F2}, TOTAL {total[30]:F2}/{total[57]:F2} ms median/p95; GC.Alloc {allocEvents} events, GC0 {GC.CollectionCount(0)-gc}");
    if(allocEvents>0)report.AppendLine("FAIL: measured loop allocated; investigate before acceptance.");
   }
   foreach(var b in buildings){registry.Remove(b);b.Dispose();}foreach(var a in assets)UnityEngine.Object.DestroyImmediate(a);
  }
  System.IO.File.WriteAllText("/private/tmp/building-combat-"+population+".txt",report.ToString());
  return report.ToString();
 }
}
