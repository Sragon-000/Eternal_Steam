using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.Demo;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;
using Newtonsoft.Json.Linq;

// Reproducible player command driver. Uses purchase/edit/HUD commands at real timeScale=1.
// No simulation ticks, stocks, energy, health, enemy or clock-phase injection.
public static class NormalPlayActions
{
 static OpenWorldSandbox World()
 {
  string save=Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT");
  if(!Application.isPlaying||save==null||!save.Contains("/Docs/Measurements/Balance/"))throw new Exception("Isolated recorded Play required");
  var config=JObject.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(save),"configuration.json")));
  if((string)config["purpose"]!="normal-survival")throw new Exception("Normal survival recording required");
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
  if(s==null||s.Persistence.Blocked||s.Content.InfiniteResources||s.Content.Defeated||Time.timeScale!=1)throw new Exception("Normal world rules required");
  return s;
 }
 static void Mark(string action,string detail)
 {
  var r=Resources.FindObjectsOfTypeAll<MonoBehaviour>().Single(x=>x!=null&&x.GetType().Name=="BalanceRunObserver"&&x.gameObject.scene.IsValid());
  r.GetType().GetMethod("Mark").Invoke(r,new object[]{action,detail,true});
 }
 static string MainId(OpenWorldSandbox s)=>s.Content.MainBase.Module<IBaseIdentity>().BaseId;
 static BuildingInstance Sub(OpenWorldSandbox s)=>s.Content.Bases.Bases.Values.Select(v=>v.Nexus).First(v=>v.Module<IBaseRole>().Role==BaseRole.Sub);
 static void Upgrade(OpenWorldSandbox s,BuildingInstance b,int level)
 {
  while(b.Module<IUpgradeControl>().Level<level){int before=b.Module<IUpgradeControl>().Level;if(!s.Persistence.Upgrades.TryUpgrade(b,out var why))throw new Exception(why);Mark("upgrade",b.PersistentId+" "+b.DefinitionId+" "+before+" -> "+b.Module<IUpgradeControl>().Level);}
  s.Content.Bases.Refresh();
 }
 static BuildingInstance Install(OpenWorldSandbox s,string id,string owner,Vector2Int? exact=null,int minZ=15,int maxZ=38)
 {
  var c=s.Content;var input=s.GetComponent<OpenWorldInput>();var d=s.ContentCatalog.Buildings.Single(v=>v.Id==id);
  if(!c.Bases.Select(owner))throw new Exception("Owner unavailable");input.BeginEditing();bool added=false;string why=null;
  bool Add(Vector2Int cell){
   var center=c.GroundWorld.Grid.Center(cell,d.Footprint);
   if((d.Placement.RequiresOwnerBase||d.Placement.RequiresOperationalArea)&&!c.Bases.Covers(center,(Vector2)d.Footprint,WorldGridGeometry.Rotation,owner))return false;
   var p=c.GroundWorld.Grid.Center(cell,Vector2Int.one);p.y=s.Ground.SampleHeight(p)+s.Ground.transform.position.y;
   return input.Edits.AddContent(d,p,WorldGridGeometry.Rotation*Vector3.forward,out why);
  }
  try{
   if(exact.HasValue)added=Add(exact.Value);
   else { var cells=Enumerable.Range(minZ,maxZ-minZ+1).SelectMany(z=>Enumerable.Range(-14,35).Select(x=>new Vector2Int(x,z))); if(id=="installation.nexus"&&s.Foundations.Platforms.Any())cells=cells.OrderBy(cell=>(c.GroundWorld.Grid.Center(cell,d.Footprint)-s.Foundations.Platforms.First().View.transform.position).sqrMagnitude); foreach(var cell in cells){if(Add(cell)){added=true;break;}} }
   if(!added||!input.Confirm())throw new Exception("Placement failed "+id+": "+why+" "+s.Message);
   var b=c.GroundWorld.Buildings.Last();Mark("build",id+" id="+b.PersistentId+" owner="+owner+" pos="+b.Position+" operational="+b.Operational+" block="+b.OperationBlock);return b;
  }catch(Exception e){Mark("action-failed",e.Message);input.Cancel();throw;}
 }
 public static string Prepare()
 {
  var s=World();if(s.Content.GroundWorld.Buildings.Count!=1)throw new Exception("Fresh main-only run required");
  Mark("control-protocol","Command-driven normal play: real timeScale 1; normal free-cost upgrades/edits; no stock/time/enemy/health injection; construction naturally pauses simulation. Initial economy and Lv3 defense setup.");
  var main=MainId(s);Upgrade(s,s.Content.MainBase,3);
  Install(s,"resource.iron",main);Install(s,"resource.iron",main);Install(s,"railway.coal",main);
  Install(s,"resource.power_generator",main);Install(s,"resource.power_generator",main);
  Upgrade(s,Install(s,"defense.arc",main),3);Upgrade(s,Install(s,"defense.plasma_laser",main),3);
  return Status();
 }
 public static string Expand()
 {
  var s=World();var main=MainId(s);var b=Install(s,"installation.nexus",main,new Vector2Int(-5,20));var other=b.Module<IBaseIdentity>().BaseId;Upgrade(s,b,3);
  Install(s,"resource.iron",other,minZ:21);Install(s,"railway.coal",other,minZ:21);
  Install(s,"resource.power_generator",other,minZ:21);Install(s,"resource.power_generator",other,minZ:21);
  Upgrade(s,Install(s,"defense.arc",other,minZ:21),3);Upgrade(s,Install(s,"defense.plasma_laser",other,minZ:21),3);
  return Status();
 }
 public static string AllContent()
 {
  var s=World();var main=MainId(s);Upgrade(s,s.Content.MainBase,10);
  foreach(var d in s.ContentCatalog.Buildings.Where(d=>d.Id.StartsWith("resource.")||d.Id.StartsWith("defense.")||d.Id=="installation.outpost"||d.Id=="installation.wall"))
   if(!s.Content.GroundWorld.Buildings.Any(b=>b.DefinitionId==d.Id))Install(s,d.Id,main);
  for(int i=0;i<4;i++)Install(s,"resource.power_generator",main);
  return Status();
 }
 static void Hud(OpenWorldSandbox s,string command){if(!s.RailwayHud.CanExecute(command,out var why))throw new Exception(command+": "+why);s.RailwayHud.Execute(command);Mark("railway-hud",command);}
 public static string Railway()
 {
  var s=World();var c=s.Content;var main=MainId(s);var other=Sub(s).Module<IBaseIdentity>().BaseId;var origin=new Vector2Int(-3,12);
  if(c.Railway.Network.Routes.Count>0)throw new Exception("Route already exists");
  var a=Install(s,"railway.station",main,origin);var b=Install(s,"railway.station",other,origin+new Vector2Int(6,0));
  var cells=new List<Vector2Int>();for(int x=2;x<=5;x++)cells.Add(new Vector2Int(x,1));cells.Add(new Vector2Int(5,0));
  for(int y=-1;y>=-6;y--)cells.Add(new Vector2Int(7,y));for(int x=6;x>=1;x--)cells.Add(new Vector2Int(x,-6));for(int y=-5;y<=-1;y++)cells.Add(new Vector2Int(1,y));
  foreach(var cell in cells)Install(s,"railway.track",main,origin+cell);
  c.Railway.Refresh();if(!c.Railway.Network.Commit(Guid.NewGuid().ToString("N"),new[]{new RailStop{stationId=a.PersistentId,arrival=2,departure=1},new RailStop{stationId=b.PersistentId,arrival=0,departure=2}},out var route,out var why))throw new Exception(why);
  Mark("railway-route",route.id+" normal route validation/commit; all iron paid from naturally produced stock");
  Hud(s,"open");Hud(s,"row:0");
  for(int i=0;i<2;i++){Hud(s,"station-row:"+i);s.RailwayHud.Resource.SetTextWithoutNotify("iron");s.RailwayHud.Load.SetTextWithoutNotify(i==0?"30":"0");s.RailwayHud.Unload.SetTextWithoutNotify(i==0?"0":"30");Hud(s,"configure");}
  Hud(s,"station-row:0");Hud(s,"fuel");Hud(s,"defense");Hud(s,"weapon-install");Hud(s,"weapon-upgrade");Hud(s,"weapon-charge");Hud(s,"weapon-back");Hud(s,"start");return Status();
 }
 public static string LegacyFoundation()
 {
  var s=World();var input=s.GetComponent<OpenWorldInput>();s.Content.Bases.Select(MainId(s));input.BeginEditing();bool added=false;string why=null;
  var candidates=Enumerable.Range(-4,11).SelectMany(x=>Enumerable.Range(-2,12).Select(z=>WorldGridGeometry.Center(new Vector2Int(x,z),8))).OrderBy(p=>(p-s.Content.MainBase.Position).sqrMagnitude);
  foreach(var p in candidates){if(input.Edits.AddFoundation(p,out why)){added=true;break;}}
  if(!added||!input.Confirm()){input.Cancel();throw new Exception("Foundation: "+why);}
  var platform=s.Foundations.Platforms.Last();Mark("foundation-build",platform.PersistentId+" normal construction command");
  var kinds=new[]{HordeTowerKind.MachineGun,HordeTowerKind.Cannon,HordeTowerKind.Frost,HordeTowerKind.Arrow};
  input.BeginEditing();
  for(int i=0;i<kinds.Length;i++){
   var p=platform.World.Grid.Center(new Vector2Int(i%4,0),Vector2Int.one);
   if(!input.Edits.AddTower(p,WorldGridGeometry.Rotation*Vector3.forward,kinds[i],out why)){input.Cancel();throw new Exception("Legacy tower: "+why);}
  }
  if(!input.Confirm())throw new Exception(s.Message);
  Mark("legacy-towers","MachineGun, Cannon, Frost, Arrow installed on normal foundation");return Status();
 }
 public static string ServiceTrain(string command)
 {
  if(!new[]{"stop","fuel","defense","weapon-charge","weapon-upgrade","weapon-back","start"}.Contains(command))throw new Exception("Unknown service command");
  var s=World();Hud(s,"open");Hud(s,"row:0");if(command.StartsWith("weapon-"))Hud(s,"defense");Hud(s,command);return Status();
 }
 public static string RearBase()
 {
  var s=World();var b=Install(s,"installation.nexus",MainId(s),minZ:3,maxZ:18);Upgrade(s,b,10);var owner=b.Module<IBaseIdentity>().BaseId;
  for(int i=0;i<2;i++)Install(s,"resource.power_generator",owner,minZ:5,maxZ:18);
  Mark("rear-base","Normal extra base and generators to supply rear foundation; correct player placement coverage, no rule changes");return Status();
 }
 public static string CraftAndStop()
 {
  var s=World();if(s.Assault.Energy.Stage!=MapStage.AwaitingCraft)throw new Exception("Natural boss completion required");
  if(s.Enemies.Alive!=0||s.Assault.Planner.Pending!=0)throw new Exception("Complete the natural wave before recording campaign completion");
  // Craft normally resets the legacy shot counters. Preserve their final cumulative values first.
  var coverage=Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(x=>x!=null&&x.GetType().Name=="CombatCoverageObserver"&&x.gameObject.scene.IsValid());
  if(coverage!=null)coverage.GetType().GetMethod("Finish").Invoke(coverage,new object[]{"before-orb-craft"});
  var hud=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();hud.Execute("orb-craft");
  if(!s.Assault.Energy.PerfectOrb||s.Assault.Energy.Stage!=MapStage.Cleared)throw new Exception("Craft rejected");
  Mark("orb-crafted","Normal campaign complete on day "+s.Clock.Day+" "+s.Clock.Phase+" after "+s.Enemies.Spawned+" spawned / "+s.Enemies.Killed+" killed. Observed completion, not a theoretical survival ceiling. Subsequent docking/save time is excluded from survival.");
  foreach(var route in s.Content.Railway.Network.Routes)if(route.train.status is TrainStatus.Moving or TrainStatus.Dwelling)s.Content.Railway.Network.Stop(route);
  if(s.Clock.Paused&&s.Content.Railway.Network.Routes.Any(r=>r.train.status==TrainStatus.StopRequested)){var actions=new OpenWorldHudActions(s,s.GetComponent<OpenWorldInput>());if(!actions.ToggleProgress())throw new Exception(s.Message);Mark("resume-for-docking","Normal progress toggle after clear, to complete the requested next-station stop before save.");}
  return Status();
 }
 public static string SaveCompleted()
 {
  var s=World();if(s.Assault.Energy.Stage!=MapStage.Cleared||s.Enemies.Alive!=0)throw new Exception("Completed safe run required");
  if(s.Content.Railway.Network.Routes.Any(r=>r.train.status==TrainStatus.StopRequested))throw new Exception("Train still returning to station");
  s.Clock.Paused=true; // normal player pause before safe save and exact reload comparison
  if(!s.Persistence.Save())throw new Exception(s.Persistence.Status);
  string root=Path.GetDirectoryName(Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT"));
  File.WriteAllText(root+"/completed-snapshot.json",JsonUtility.ToJson(s.Persistence.Capture(),true));
  Mark("completed-save",s.Persistence.RunId+" normal safe save with clock paused; exact snapshot retained");return Status();
 }
 public static string Status()
 {
  var s=World();var c=s.Content;
  return new JObject{{"day",s.Clock.Day},{"phase",s.Clock.Phase.ToString()},{"remaining",s.Clock.RemainingSeconds},{"paused",s.SimulationPaused},{"stage",s.Assault.Energy.Stage.ToString()},{"energy",s.Assault.Energy.Earned},{"enemies",s.Enemies.Alive},{"spawned",s.Enemies.Spawned},{"killed",s.Enemies.Killed},{"boss",s.Assault.BossHealth},{"buildings",c.GroundWorld.Buildings.Count},{"bases",new JArray(c.Bases.Bases.Values.Select(v=>new JObject{{"id",v.Nexus.Module<IBaseIdentity>().BaseId},{"hp",v.Nexus.Module<HealthModule>().Current},{"level",v.Nexus.Module<IUpgradeControl>().Level},{"stocks",JArray.FromObject(c.PaymentBankFor(v.Nexus).Capture())}}))},{"routes",new JArray(c.Railway.Network.Routes.Select(r=>JObject.Parse(JsonUtility.ToJson(r.train))))}}.ToString();
 }
 public static string Describe()=>"Normal command-driven survival actions; production and progression require real time.";
}
