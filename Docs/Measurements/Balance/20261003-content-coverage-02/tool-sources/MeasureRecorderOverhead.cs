using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Profiling;
using Newtonsoft.Json.Linq;
using EternalSteam.OpenWorld;

// Post-clear, paused, same-scene QA. Never changes simulation, inventories or camera.
public static class MeasureRecorderOverhead
{
 public static string Start()
 {
  var save=Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT");
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
  if(!Application.isPlaying||save==null||!save.Contains("/Docs/Measurements/Balance/")||s==null||!s.Clock.Paused||!s.Assault.Energy.PerfectOrb||s.Enemies.Alive!=0)throw new Exception("Completed paused isolated campaign required");
  var root=Path.GetDirectoryName(save);if(!File.Exists(root+"/completed-snapshot.json")||File.Exists(root+"/status.json"))throw new Exception("Saved completed campaign and still-active recorder required");
  var coverage=JObject.Parse(File.ReadAllText(root+"/combat-coverage-status.json"));if(!(bool)coverage["stopped"])throw new Exception("Preserve and stop combat counters first");
  var observer=Resources.FindObjectsOfTypeAll<MonoBehaviour>().Single(v=>v!=null&&v.GetType().Name=="BalanceRunObserver"&&v.gameObject.scene.IsValid());
  var path=root+"/recorder-overhead";if(Directory.Exists(path))throw new Exception("Never overwrite measurement");Directory.CreateDirectory(path);
  new GameObject("Post-clear recorder overhead probe").AddComponent<RecorderOverheadProbe>().Begin(s,observer,path);
  return "Running ABBA: recorder update off/on/on/off, 60 warmup frames and 12 real seconds each; one explicit PNG in each on phase. Poll recorder-overhead/status.json.";
 }
 public static string Describe()=>"Read-only game-state ABBA observation-cost QA after normal clear/save, before recorder stop; preserves and restores observer enabled state.";
}
public sealed class RecorderOverheadProbe:MonoBehaviour
{
 OpenWorldSandbox world;MonoBehaviour observer;string root;bool oldEnabled,finished;JObject before;JArray phases=new();
 public void Begin(OpenWorldSandbox s,MonoBehaviour o,string p){world=s;observer=o;root=p;oldEnabled=o.enabled;before=State();File.WriteAllText(root+"/before.json",before.ToString());File.WriteAllText(root+"/status.json","{\"running\":true}");StartCoroutine(Guarded());}
 JObject State(){var x=JObject.Parse(JsonUtility.ToJson(world.Persistence.Capture()));x.Remove("savedUtc");return x;}
 static JObject Stats(IEnumerable<double> data){var a=data.OrderBy(v=>v).ToArray();return new JObject{{"count",a.Length},{"mean",a.Average()},{"median",a[a.Length/2]},{"p95",a[Math.Min(a.Length-1,(int)(a.Length*.95))]},{"maximum",a.Last()}};}
 IEnumerator Guarded()
 {
  var routine=Measure();
  while(true){bool more=false;object current=null;Exception failure=null;
   try{more=routine.MoveNext();if(more)current=routine.Current;}catch(Exception e){failure=e;}
   if(failure!=null){(routine as IDisposable)?.Dispose();observer.enabled=oldEnabled;finished=true;File.WriteAllText(root+"/status.json",new JObject{{"running",false},{"passed",false},{"error",failure.ToString()}}.ToString());Destroy(gameObject);yield break;}
   if(!more)yield break;yield return current;
  }
 }
 IEnumerator Measure()
 {
  using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame"))
  using(var cpu=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread")){
   for(int phase=0;phase<4;phase++){
    bool enabled=phase==1||phase==2;observer.enabled=enabled;
    for(int warm=0;warm<60;warm++)yield return null;
    var frames=new List<double>();var allocations=new List<double>();var main=new List<double>();var raw=new JArray();double start=Time.realtimeSinceStartupAsDouble;bool capture=false;
    while(Time.realtimeSinceStartupAsDouble-start<12){
     yield return null;double elapsed=Time.realtimeSinceStartupAsDouble-start;
     frames.Add(Time.unscaledDeltaTime*1000);if(gc.Valid)allocations.Add(gc.LastValue);if(cpu.Valid)main.Add(cpu.LastValue*1e-6);
     raw.Add(new JArray(elapsed,Time.unscaledDeltaTime*1000,gc.Valid?(double)gc.LastValue:-1,cpu.Valid?cpu.LastValue*1e-6:-1));
     if(enabled&&!capture&&elapsed>=5){observer.GetType().GetMethod("Mark").Invoke(observer,new object[]{"post-clear-overhead-capture","Read-only paused QA phase "+phase,true});capture=true;}
    }
    phases.Add(new JObject{{"phase",phase},{"recorderUpdateEnabled",enabled},{"frameMs",Stats(frames)},{"gcBytes",gc.Valid?Stats(allocations):null},{"mainThreadMs",cpu.Valid?Stats(main):null},{"explicitScreenshot",capture},{"rawColumns",new JArray("elapsedSeconds","frameMs","gcBytes","mainThreadMs")},{"raw",raw}});
   }
  }
  observer.enabled=oldEnabled;var after=State();bool equal=JToken.DeepEquals(before,after);File.WriteAllText(root+"/after.json",after.ToString());
  File.WriteAllText(root+"/result.json",new JObject{{"passed",equal},{"scene",world.gameObject.scene.path},{"paused",world.Clock.Paused},{"gameStateEqualExcludingSavedUtc",equal},{"restoredRecorderEnabled",observer.enabled},{"phases",phases},{"limits","Paused completed layout only; Editor/rendering/frame pacing included. Supplemental combat observer is stopped. Disabled MonoBehaviour retains an idle capture coroutine. Explicit PNG in on phases measures recording workload, not combat performance. Raw frame GC includes Editor and probe allocations in all phases."}}.ToString());
  finished=true;File.WriteAllText(root+"/status.json",new JObject{{"running",false},{"passed",equal},{"error",equal?null:"Game state changed"}}.ToString());Destroy(gameObject);
 }
 void OnDestroy(){if(observer!=null)observer.enabled=oldEnabled;if(!finished&&root!=null)File.WriteAllText(root+"/status.json","{\"running\":false,\"passed\":false,\"error\":\"Probe destroyed before completion; recorder enabled state restored\"}");}
}
