using System;
using System.Linq;
using System.Text;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Profiling;
using EternalSteam;
using EternalSteam.Demo;
using EternalSteam.OpenWorld;
public static class BenchmarkDocumentCombat
{
 public static string Run1()=>Measure(1);
 public static string Run2()=>Measure(2);
 public static string Run3()=>Measure(3);
 public static string Main()=>Measure(1);
 static string Measure(int repeat)
 {
  var scene=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var report=new StringBuilder("CPU-only fixed dt=1/60; warmup 30 + 720 samples (12s), 3 repetitions. No rendering/UI. Heap delta is retained managed heap change, NOT allocated bytes.\n");
  foreach(int count in new[]{4000,10000,100000}){
   var terrain=scene.Ground;var center=terrain.transform.position+terrain.terrainData.size*.5f;center.y=0;
   var world=new HordeEnemyWorld(p=>terrain.SampleHeight(p)+terrain.transform.position.y+.55f,false,count,new Rect(terrain.transform.position.x,terrain.transform.position.z,terrain.terrainData.size.x,terrain.terrainData.size.z));
   int side=Mathf.CeilToInt(Mathf.Sqrt(count));for(int i=0;i<count;i++){var p=center+new Vector3(((i%side)-side*.5f)*.75f,0,((i/side)-side*.5f)*.75f);world.TrySpawn(p,center+Vector3.forward*80,2.4f,1000000,i%2==0);}
   var adapter=new HordeTargetAdapter(world);world.MoveAndIndex(0);
   // Warm the fixed population's receiver leases outside the measured loop.
   for(int i=0;i<count;i++)adapter.TryGet(new TargetHandle(i,world.Generation(i)),out _);
   var weapons=scene.ContentCatalog.Buildings.Where(d=>d.Category==BuildingCategory.Defense).Select((d,i)=>new BuildingInstance(i,d,Vector2Int.zero,center+new Vector3(i*2-8,0,0),new BuildingServices(adapter))).ToArray();int hits=0;Action<Vector3> onHit=_=>hits++;foreach(var b in weapons){b.Activate();b.Shot+=onHit;}
   var timer=new Stopwatch();var movement=new double[720];var combat=new double[720];
   for(int i=0;i<30;i++){adapter.Tick(1f/60);world.MoveAndIndex(1f/60);foreach(var b in weapons)b.Tick(1f/60);}
   long heap=Profiler.GetMonoUsedSizeLong();int gc=GC.CollectionCount(0);
   for(int i=0;i<720;i++){timer.Restart();adapter.Tick(1f/60);world.MoveAndIndex(1f/60);timer.Stop();movement[i]=timer.Elapsed.TotalMilliseconds;timer.Restart();foreach(var b in weapons)b.Tick(1f/60);timer.Stop();combat[i]=timer.Elapsed.TotalMilliseconds;}
   long delta=Profiler.GetMonoUsedSizeLong()-heap;Array.Sort(movement);Array.Sort(combat);
   report.AppendLine($"{count} run {repeat}: move/status median {movement[360]:F2} p95 {movement[684]:F2} ms; 9 weapons median {combat[360]:F2} p95 {combat[684]:F2} ms max {combat[719]:F2}; hit events {hits}; heap delta {delta} B, GC0 collections {GC.CollectionCount(0)-gc}");
   foreach(var b in weapons)b.Dispose();adapter.Dispose();
  }
  return report.ToString();
 }
}
