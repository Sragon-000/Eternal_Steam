using System;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using EternalSteam;
using EternalSteam.Demo;
using EternalSteam.OpenWorld;
public static class VerifyEnemySpawnStream
{
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 public static async Task<string> Main()
 {
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var input=s.GetComponent<OpenWorldInput>();input.Cancel();s.ResetEnemies();
  var destinations=new NexusDestinationQuery(s.Content.GroundWorld);destinations.Refresh();
  try {
   Check(new HordeEnemyWorld().MaxCount==4000,"Legacy capacity preserved");
   Check(destinations.HasTargets,"Install and confirm a nexus before this flat-terrain load scenario");
   var field=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>().GetComponent<UIDocument>().rootVisualElement.Q<TextField>("amount");
   field.value="100000000";Check(field.value=="100000000","Nine-digit UI input");
   Check(!s.Spawn("100000001")&&!s.Spawn("0")&&!s.Spawn("abc"),"Invalid counts rejected");
   Check(s.Spawn("100000000"),"100 million request accepted");s.Running=false;
   Check(s.Enemies.Spawned<=EnemySpawnStream.BatchSize,"Initial work bounded");
   var watch=Stopwatch.StartNew();int pumps=0;
   while(s.Enemies.Alive<100000 && pumps++<1000){s.SpawnStream.Pump();if(pumps%10==0)await Task.Delay(1);}
   double fillMs=watch.Elapsed.TotalMilliseconds;
   Check(s.Enemies.Alive==100000,"Actual 100,000 simultaneously alive: "+s.Enemies.Alive);
   Check(s.SpawnStream.Pending==99900000,"Remaining request counted exactly");
   Check(s.SpawnStream.Pump()==0&&!s.Spawn("1"),"Full capacity and total request cap");
   var layout=new EnemySpawnLayout(100000);layout.Begin(new HordeEnemyWorld(),s.EnemySpawnSpacing);
   for(int i=0;i<s.Enemies.MaxCount;i++) {
    var e=s.Enemies.GetEnemy(i);Check(layout.IsFree(e.position),"Spawn spacing at "+i);layout.Add(e.position);
    Check(destinations.TryNearest(e.position,out var target),"Target exists");
    var d=e.destination-target;d.y=0;Check(d.sqrMagnitude<.001f,"Nearest nexus target");
   }
   double[] ms=new double[90];
   for(int i=0;i<95;i++){watch.Restart();s.Enemies.MoveAndIndex(1f/60);watch.Stop();if(i>=5)ms[i-5]=watch.Elapsed.TotalMilliseconds;}
   Array.Sort(ms);
   var high=s.Enemies.GetEnemy(99999);new HordeAttackResolver(s.Enemies).ApplyArea(high.position,.01f);
   Check(!s.Enemies.GetEnemy(99999).alive||s.Enemies.GetEnemy(99999).health<high.health,"Combat reaches slot above 4,000");
   s.SpawnStream.Pump();Check(s.Enemies.Alive==100000,"Vacancies replenished");
   var before=s.Enemies.GetEnemy(0);s.Enemies.MoveAndIndex(.1f);var after=s.Enemies.GetEnemy(0);
   Check((after.position-after.destination).sqrMagnitude<(before.position-before.destination).sqrMagnitude,"Movement approaches nexus");
   s.ResetEnemies();Check(s.Enemies.Alive==0&&s.SpawnStream.Pending==0,"Reset clears active and pending");
   Check(s.Spawn("25"),"Small request accepted");s.Running=false;Check(s.Enemies.Spawned==25&&s.SpawnStream.Pending==0,"Small exact count");
   return $"PASS: actual 100,000 alive, 0.75m spacing against every live spawn, nearest nexus targets, 100M request bounds, bounded batches, refill/reset, high-slot combat, legacy default. Fill {fillMs:F1} ms over {pumps} pumps. 100K MoveAndIndex(dt=1/60), 90 samples after 5 warmups: median {ms[45]:F2} ms / p95 {ms[85]:F2} ms. This measures movement/index CPU only, not whole-frame/GPU.";
  } finally {s.ResetEnemies();}
 }
}
