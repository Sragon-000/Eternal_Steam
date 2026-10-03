using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using EternalSteam;
using EternalSteam.OpenWorld;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Passive supplemental observation. Legacy combat increments HordeTower.shot directly,
// rather than raising BuildingInstance.Shot. Never changes game rules or actor state.
public static class CombatCoverageProbe
{
 static MonoBehaviour Existing()=>Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(b=>b!=null&&b.GetType().Name=="CombatCoverageObserver"&&b.gameObject.scene.IsValid());
 public static string Start()
 {
  string save=Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT");
  if(!Application.isPlaying||Existing()!=null||save==null||!save.Contains("/Docs/Measurements/Balance/"))throw new Exception("Fresh isolated recorded Play required");
  var world=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
  if(world==null||world.Content.InfiniteResources||Time.timeScale!=1)throw new Exception("Normal simulation required");
  var recorder=Resources.FindObjectsOfTypeAll<MonoBehaviour>().Single(b=>b!=null&&b.GetType().Name=="BalanceRunObserver"&&b.gameObject.scene.IsValid());
  string root=Path.GetDirectoryName(save);
  if(File.Exists(root+"/combat-coverage.jsonl"))throw new Exception("Never overwrite coverage evidence");
  var observer=new GameObject("Passive combat coverage observer").AddComponent<CombatCoverageObserver>();
  observer.Begin(world,root);
  recorder.GetType().GetMethod("Mark").Invoke(recorder,new object[]{"coverage-probe-start","Passive supplemental legacy shot counters and live range opportunities. Legacy uses HordeTower.shot, not BuildingInstance.Shot; prior legacy event zeros are not evidence of inactivity. Initial counters are cumulative since construction, exact prior shot times unavailable.",true});
  return observer.Status();
 }
 public static string Status(){var p=Existing();return p==null?"No coverage probe":(string)p.GetType().GetMethod("Status").Invoke(p,null);}
 public static string Stop(){var p=Existing();if(p==null)throw new Exception("No coverage probe");p.GetType().GetMethod("Finish").Invoke(p,new object[]{"controlled-stop"});return (string)p.GetType().GetMethod("Status").Invoke(p,null);}
}
public sealed class CombatCoverageObserver:MonoBehaviour
{
 OpenWorldSandbox s;string root,error;StreamWriter writer;long sequence;double next;bool stopped;
 double Seconds=>(s.Clock.Day-1)*(s.DayDurationSeconds+s.NightDurationSeconds)+(s.Clock.Phase==DayPhase.Day?s.DayDurationSeconds-s.Clock.RemainingSeconds:s.DayDurationSeconds+s.NightDurationSeconds-s.Clock.RemainingSeconds);
 public void Begin(OpenWorldSandbox world,string directory)
 {
  s=world;root=directory;writer=new StreamWriter(new FileStream(root+"/combat-coverage.jsonl",FileMode.CreateNew,FileAccess.Write,FileShare.Read));
  Sample("begin");next=Time.realtimeSinceStartupAsDouble+1;
 }
 static JObject Vector(Vector3 p)=>new JObject{{"x",p.x},{"y",p.y},{"z",p.z}};
 JObject Actor(BuildingInstance b,float range,TargetKind kinds,int? legacyShots=null)
 {
  int eligible=0,ground=0,air=0;float nearest=float.PositiveInfinity;
  foreach(int id in s.Enemies.ActiveIndices){ref readonly var e=ref s.Enemies.GetEnemy(id);var d=e.position-b.Position;d.y=0;float distance=d.magnitude;nearest=Mathf.Min(nearest,distance);
   if(distance<=range){if(e.air)air++;else ground++;if((kinds&(e.air?TargetKind.Air:TargetKind.Ground))!=0)eligible++;}
  }
  var power=b.Module<PowerModule>();
  return new JObject{{"building",b.PersistentId},{"definition",b.DefinitionId},{"position",Vector(b.Position)},{"range",range},{"kinds",kinds.ToString()},
   {"operational",b.Operational},{"block",b.OperationBlock.ToString()},{"combatPermitted",CombatPermission.Allows(b)},
   {"supplied",power==null?null:JToken.FromObject(power.Supplied)},{"legacyCumulativeShots",legacyShots.HasValue?JToken.FromObject(legacyShots.Value):null},
   {"nearestAliveDistance",float.IsFinite(nearest)?JToken.FromObject(nearest):null},{"groundInRange",ground},{"airInRange",air},{"kindEligibleInRange",eligible}};
 }
 void Sample(string kind)
 {
  var actors=new JArray();
  foreach(var b in s.Content.Bases.Buildings){var weapon=b.Module<WeaponRuntime>();if(weapon!=null)actors.Add(Actor(b,weapon.Range,weapon.Kinds));}
  foreach(var tower in s.Towers)actors.Add(Actor(tower.building,tower.range,TargetKind.All,tower.shot));
  var row=new JObject{{"sequence",++sequence},{"utc",DateTime.UtcNow.ToString("O")},{"kind",kind},{"gameSeconds",Seconds},{"day",s.Clock.Day},{"phase",s.Clock.Phase.ToString()},{"alive",s.Enemies.Alive},{"actors",actors},
   {"limitation","One-second range opportunities omit turn alignment, slow-status filtering and sub-sample movement; legacy counters are cumulative, not hit or kill counts."}};
  writer.WriteLine(row.ToString(Formatting.None));writer.Flush();
 }
 void LateUpdate()
 {
  if(stopped||error!=null||Time.realtimeSinceStartupAsDouble<next)return;next=Time.realtimeSinceStartupAsDouble+1;
  try{if(File.Exists(root+"/status.json")){Finish("main-recorder-stopped");return;}Sample("sample");}
  catch(Exception e){error=e.ToString();EditorApplication.isPaused=true;File.WriteAllText(root+"/combat-coverage-status.json",Status());}
 }
 public string Status()=>new JObject{{"sequence",sequence},{"stopped",stopped},{"error",error},{"day",s?.Clock.Day},{"phase",s?.Clock.Phase.ToString()},
  {"legacy",s==null?null:new JArray(s.Towers.Select(t=>new JObject{{"definition",t.building.DefinitionId},{"shots",t.shot},{"range",t.range},{"operational",t.building.Operational}}))}}.ToString();
 public void Finish(string reason){if(stopped)return;Sample("end:"+reason);stopped=true;writer.Dispose();writer=null;File.WriteAllText(root+"/combat-coverage-status.json",Status());}
 void OnDestroy(){if(!stopped&&writer!=null){try{Finish("destroyed-before-controlled-stop");}catch(Exception e){error=e.ToString();writer.Dispose();}}}
}
