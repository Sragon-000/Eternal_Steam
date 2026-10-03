using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;
using Newtonsoft.Json.Linq;

// Explicitly recorded player-command automation. Never alters time, stocks, health or enemies.
public static class SurvivalServicePilot
{
 static MonoBehaviour Existing()=>Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(b=>b!=null&&b.GetType().Name=="SurvivalServiceDriver"&&b.gameObject.scene.IsValid());
 public static string Start(int throughDay=10)
 {
  if(throughDay<1||Existing()!=null||!Application.isPlaying)throw new Exception("Fresh playing pilot and positive observation day required");
  string save=Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT");
  if(save==null||!save.EndsWith("20261003-survival-improved-01/save",StringComparison.Ordinal))throw new Exception("Expected improved run");
  var world=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
  var recorder=Resources.FindObjectsOfTypeAll<MonoBehaviour>().Single(b=>b!=null&&b.GetType().Name=="BalanceRunObserver"&&b.gameObject.scene.IsValid());
  var driver=new GameObject("Recorded player service driver").AddComponent<SurvivalServiceDriver>();driver.Begin(world,recorder,Path.GetDirectoryName(save),throughDay);return driver.Status();
 }
 public static string Status(){var p=Existing();return p==null?"No service driver":(string)p.GetType().GetMethod("Status").Invoke(p,null);}
}
public sealed class SurvivalServiceDriver:MonoBehaviour
{
 OpenWorldSandbox s;MonoBehaviour recorder;string root,state="running",error;double next;int throughDay;long commands;readonly HashSet<string> servicing=new();
 public void Begin(OpenWorldSandbox world,MonoBehaviour observer,string directory,int day){s=world;recorder=observer;root=directory;throughDay=day;Mark("pilot-policy","Normal command automation: request next-station stop if coal<20 or mounted battery<=20; refuel up to 50 from station inventory, transfer actual stored power, then restart. No upgrades/spawns/time/resource injection. Observe through night "+day+" then pause at next safe dawn for review.");}
 void Mark(string action,string detail){recorder.GetType().GetMethod("Mark").Invoke(recorder,new object[]{action,detail,true});File.WriteAllText(root+"/pilot-status.json",Status());}
 public string Status()=>new JObject{{"state",state},{"error",error},{"throughDay",throughDay},{"commands",commands},{"day",s?.Clock.Day},{"phase",s?.Clock.Phase.ToString()},{"alive",s?.Enemies.Alive},{"defeated",s?.Content.Defeated},{"servicing",new JArray(servicing)}}.ToString();
 void Command(string action,RailRoute r,double beforeFuel,double beforeBattery,string detail="") {commands++;Mark("pilot-"+action,r.id+" fuel "+beforeFuel+" -> "+r.train.fuel+" battery "+beforeBattery+" -> "+r.train.armament.battery+" "+detail);}
 void Update()
 {
  if(state!="running"||Time.realtimeSinceStartupAsDouble<next)return;next=Time.realtimeSinceStartupAsDouble+1;
  try{
   if(s==null||recorder==null)throw new Exception("Recording world or observer disappeared");
   if(recorder.GetType().GetProperty("LastError").GetValue(recorder)!=null)throw new Exception("Recorder error; stop command automation");
   if(s.Content.InfiniteResources||Time.timeScale!=1)throw new Exception("Normal-play protocol violated");
   if(s.Content.Defeated){state="defeated";Mark("pilot-defeat","Natural defeat; no state restoration or retry");return;}
   if(s.Clock.Day>throughDay&&s.Clock.Phase==DayPhase.Day&&s.Enemies.Alive==0&&s.Assault.Planner.Pending==0){s.Clock.Paused=true;state="observation-checkpoint";Mark("pilot-checkpoint","Completed night "+throughDay+"; normal clock pause at safe dawn for review. Orb not crafted, no survival ceiling claimed.");return;}
   if(s.SimulationPaused||s.GetComponent<OpenWorldInput>().IsEditing)return;
   foreach(var r in s.Content.Railway.Network.Routes){var t=r.train;double fuel=t.fuel,battery=t.armament.battery;
    if(t.status==TrainStatus.RouteError)throw new Exception("Route error: "+r.error);
    if(t.fuel<20||(!string.IsNullOrEmpty(t.armament.definition)&&t.armament.battery<=20)||t.status==TrainStatus.FuelWait)servicing.Add(r.id);
    if(!servicing.Contains(r.id))continue;
    if(t.status is TrainStatus.Moving or TrainStatus.Dwelling){s.Content.Railway.Network.Stop(r);Command("stop-request",r,fuel,battery);continue;}
    if(t.status==TrainStatus.StopRequested||t.waitingForStation)continue;
    if(t.status is not (TrainStatus.Stopped or TrainStatus.FuelWait))continue;
    if(t.fuel<80&&s.Content.Railway.Network.Refuel(r,RailwayNetwork.RefuelBatch,out var why))Command("refuel",r,fuel,battery);
    fuel=t.fuel;battery=t.armament.battery;
    if(t.armament.battery<TrainArmament.Capacity&&s.Content.Railway.Defense.CanCharge(r,out _)&&s.Content.Railway.Defense.Charge(r,out _))Command("charge",r,fuel,battery);
    fuel=t.fuel;battery=t.armament.battery;
    if(s.Content.Railway.Network.Start(r,out var startReason)){servicing.Remove(r.id);Command("restart",r,fuel,battery);}
   }
  }catch(Exception e){error=e.ToString();state="error";EditorApplication.isPaused=true;File.WriteAllText(root+"/pilot-status.json",Status());}
 }
}
