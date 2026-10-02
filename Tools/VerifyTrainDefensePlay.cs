using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;
public static class VerifyTrainDefensePlay
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static OpenWorldSandbox Sandbox(){Check(Application.isPlaying,"Play required");Check(Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")?.StartsWith("/tmp/eternal-railway-p6-")==true,"Isolated P6 root required");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();s.Clock.Paused=true;s.Persistence.Automatic=false;Check(!s.Persistence.Blocked,s.Persistence.Status);return s;}
 static void Click(RailwayHud h,string command){var button=h.GetComponentsInChildren<Button>(true).Single(b=>b.name==command);button.onClick.Invoke();}
 public static string Main(){
  var s=Sandbox();var c=s.Content;var n=c.Railway.Network;var r=n.Routes.Single();Check(r.train.status==TrainStatus.Stopped,"Stopped fixture required");
  while(c.LevelCap<2)Check(s.Persistence.Upgrades.TryUpgrade(c.MainBase,out var why),why);c.Bases.Refresh();
  var station=n.Station(r.stops[r.train.stop].stationId);var bank=c.Inventories.Available(station.OwnerBaseId);double before=bank.Amount("iron");int targets=c.BuildingTargets.Count,buildings=c.Bases.Buildings.Count;
  var h=s.RailwayHud;h.Execute("open");h.RowButtons[0].onClick.Invoke();Click(h,"defense");Check(h.ArmamentPanel.activeSelf,"Authored armament panel");
  Check(!c.Railway.Defense.Install(r,s.ContentCatalog.Buildings.Single(d=>d.Id=="defense.tesla"),out _),"Wrong footprint rejected");Check(bank.Amount("iron")==before,"Rejected installation no debit");
  Click(h,"weapon-install");Check(r.train.armament.definition=="defense.arc",h.Feedback.text);Check(bank.Amount("iron")==before-10,"Station owner charged install");
  Click(h,"weapon-upgrade");Check(c.Railway.Defense.Mounted.Single().Building.Module<IUpgradeControl>().Level==2,h.Feedback.text);Check(bank.Amount("iron")==before-15,"Upgrade costs committed once");
  var power=c.Bases.Bases[station.OwnerBaseId].Nexus.Module<PowerModule>();var state=SavedState.Capture(power);state.values.Single(v=>v.name=="Stored").text="100";state.Restore(power);double total=power.Stored+r.train.armament.battery;Click(h,"weapon-charge");Check(power.Stored==0&&r.train.armament.battery==total,"Power conservation with actual station owner");
  Check(c.BuildingTargets.Count==targets&&c.Bases.Buildings.Count==buildings,"No ground/target registration");
  Check(n.Start(r,out var error),error);c.Railway.Tick(11);var m=c.Railway.Defense.Mounted.Single();var origins=new List<Vector3>();m.Building.Shot+=_=>origins.Add(m.Building.Position);
  var point=m.Building.Position+Vector3.forward*2;Check(s.Enemies.TrySpawn(point,point,0,10000),"Enemy spawn");s.Enemies.MoveAndIndex(0);int hp=s.Enemies.GetEnemy(0).health;
  for(int i=0;i<30;i++)c.Railway.Tick(.1,true);
  Check(s.Enemies.GetEnemy(0).health<hp&&origins.Count>0,"Actual horde damaged by moving train");Check((m.Building.Position-s.RailwayView.Defense(r.trainId).Turret.position).sqrMagnitude<.001f,"Combat origin follows authored turret");
  Check(!c.Railway.Defense.Remove(r,out _)&&!c.Railway.Defense.Charge(r,out _),"Moving install/charge forbidden");
  s.Enemies.Reset();Check(c.Railway.Defense.HasPendingExecution,"Arc execution remains");Check(!s.Persistence.CanSave(out error)&&error.Contains("기차"),"Pending train execution save gate");
  for(int i=0;i<70;i++)c.Railway.Tick(.1,true);Check(!c.Railway.Defense.HasPendingExecution,"Pending completion");
  s.Clock.SetPhase(DayPhase.Day);Check(s.Persistence.Save(),s.Persistence.Status);File.WriteAllText("/tmp/eternal-railway-p6-before.json",JsonUtility.ToJson(s.Persistence.Capture()));
  return "PASS saved Canvas callbacks, exact-size rejection, station-owner install/upgrade, battery conservation, moving horde damage, turret origin, no target/grid registration, moving action rejection, pending execution safe-save gate, armed save; shots="+origins.Count;
 }
 public static string Reloaded(){
  var s=Sandbox();var before=JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText("/tmp/eternal-railway-p6-before.json"));var after=s.Persistence.Capture();before.savedUtc=after.savedUtc;Check(JsonUtility.ToJson(before)==JsonUtility.ToJson(after),"Exact armed scene reload");var r=s.Content.Railway.Network.Routes.Single();Check(s.Content.Railway.Defense.Mounted.Single().Building.Module<IUpgradeControl>().Level==2,"Mounted level restored");
  s.Content.Railway.Network.Stop(r);s.Content.Railway.Tick(40);Check(r.train.status==TrainStatus.Stopped,"Stop for removal");double energy=r.train.armament.battery;var station=s.Content.Railway.Network.Station(r.stops[r.train.stop].stationId);double iron=s.Content.Inventories.Available(station.OwnerBaseId).Amount("iron");Check(s.Content.Railway.Defense.Remove(r,out var error),error);Check(r.train.armament.battery==energy&&s.Content.Inventories.Available(station.OwnerBaseId).Amount("iron")==iron,"Removal preserves energy, no refund");
  var h=s.RailwayHud;h.Execute("open");h.RowButtons[0].onClick.Invoke();Click(h,"defense");Click(h,"weapon-install");s.Content.Railway.Tick(0);return "PASS exact armed reload (position, route, level, cooldown, battery), stationary removal/no refund/battery retention; armament UI ready";
 }
}
