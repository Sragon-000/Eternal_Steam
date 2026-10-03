using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.Demo;
using EternalSteam.OpenWorld;
using Newtonsoft.Json.Linq;

// Normal player commands for a separate content-coverage campaign. Never run on the long-run save.
public static class FocusedContentActions
{
 static OpenWorldSandbox World()
 {
  string save=Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT");
  if(!Application.isPlaying||save==null||!save.Contains("/Docs/Measurements/Balance/")||!Path.GetFileName(Path.GetDirectoryName(save)).Contains("content-coverage"))throw new Exception("Separate content-coverage recording required; long survival run is protected");
  var config=JObject.Parse(File.ReadAllText(Path.Combine(Path.GetDirectoryName(save),"configuration.json")));
  if((string)config["purpose"]!="normal-survival")throw new Exception("Normal survival purpose required");
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
  if(s==null||s.Persistence.Blocked||s.Content.Defeated||s.Content.InfiniteResources||Time.timeScale!=1)throw new Exception("Normal playable world required");
  return s;
 }
 static void Mark(string action,string detail)
 {
  var recorder=Resources.FindObjectsOfTypeAll<MonoBehaviour>().Single(x=>x!=null&&x.GetType().Name=="BalanceRunObserver"&&x.gameObject.scene.IsValid());
  recorder.GetType().GetMethod("Mark").Invoke(recorder,new object[]{action,detail,true});
 }
 static string Id(BuildingInstance b)=>b.Module<IBaseIdentity>().BaseId;
 static Vector3 AreaCenter(BuildingInstance b){var area=b.Module<IBuildArea>();return (area.Boundary(0)+area.Boundary(.5f))*.5f;}
 static void Upgrade(OpenWorldSandbox s,BuildingInstance b,int level)
 {
  while(b.Module<IUpgradeControl>().Level<level){if(!s.Persistence.Upgrades.TryUpgrade(b,out var why))throw new Exception(why);Mark("coverage-upgrade",b.DefinitionId+" -> "+b.Module<IUpgradeControl>().Level);}
  s.Content.Bases.Refresh();
 }
 static IEnumerable<Vector2Int> Cells(BuildGrid grid,Vector3 point,int radius=14)
 {
  var center=grid.WorldToCell(point);
  return Enumerable.Range(-radius,radius*2+1).SelectMany(z=>Enumerable.Range(-radius,radius*2+1).Select(x=>center+new Vector2Int(x,z)))
   .OrderBy(cell=>(grid.Center(cell,Vector2Int.one)-point).sqrMagnitude);
 }
 static BuildingInstance Place(OpenWorldSandbox s,string definition,string owner,Vector3 target,BuildingInstance protectedBase=null,float maximumBaseDistance=16)
 {
  var c=s.Content;var d=s.ContentCatalog.Buildings.Single(x=>x.Id==definition);var input=s.GetComponent<OpenWorldInput>();
  if(!c.Bases.Select(owner))throw new Exception("Owner unavailable");input.BeginEditing();string why=null;
  try{
   foreach(var cell in Cells(c.GroundWorld.Grid,target)){
    Vector3 center=c.GroundWorld.Grid.Center(cell,d.Footprint);
    if(protectedBase!=null){var offset=center-protectedBase.Position;offset.y=0;if(offset.magnitude>maximumBaseDistance)continue;}
    if((d.Placement.RequiresOwnerBase||d.Placement.RequiresOperationalArea)&&!c.Bases.Covers(center,(Vector2)d.Footprint,WorldGridGeometry.Rotation,owner))continue;
    var p=c.GroundWorld.Grid.Center(cell,Vector2Int.one);p.y=s.Ground.SampleHeight(p)+s.Ground.transform.position.y;
    if(!input.Edits.AddContent(d,p,WorldGridGeometry.Rotation*Vector3.forward,out why))continue;
    if(!input.Confirm())throw new Exception(s.Message);
    var b=c.GroundWorld.Buildings.Last();Mark("coverage-build",b.PersistentId+" "+definition+" owner="+owner+" target="+target+" actual="+b.Position+" operational="+b.Operational);return b;
   }
   throw new Exception("No valid placement for "+definition+": "+why);
  }catch(Exception e){input.Cancel();Mark("coverage-action-failed",e.Message);throw;}
 }
 static void Legacy(OpenWorldSandbox s,HordeTowerKind kind,string owner,Vector3 target,BuildingInstance protectedBase=null)
 {
  var c=s.Content;var input=s.GetComponent<OpenWorldInput>();c.Bases.Select(owner);input.BeginEditing();string why=null;
  try{
   foreach(var cell in Cells(c.GroundWorld.Grid,target)){
    var p=c.GroundWorld.Grid.Center(cell,Vector2Int.one);
    if(!c.Bases.Covers(p,Vector2.one,WorldGridGeometry.Rotation,owner))continue;
    if(protectedBase!=null){var offset=p-protectedBase.Position;offset.y=0;if(offset.magnitude>16)continue;}
    p.y=s.Ground.SampleHeight(p)+s.Ground.transform.position.y;
    if(!input.Edits.AddTower(p,WorldGridGeometry.Rotation*Vector3.forward,kind,out why))continue;
    if(!input.Confirm())throw new Exception(s.Message);
    var tower=s.Towers.Last();Mark("coverage-legacy-build",kind+" "+tower.building.PersistentId+" "+tower.building.Position);return;
   }
   throw new Exception("No normal ground placement for "+kind+": "+why);
  }catch(Exception e){input.Cancel();Mark("coverage-action-failed",e.Message);throw;}
 }
 public static string PrepareCore()
 {
  var s=World();if(s.Content.GroundWorld.Buildings.Count!=1)throw new Exception("Fresh main-only coverage campaign required");
  Mark("coverage-policy","Second coverage strategy after first-night defeat: all four legacy towers near the actual main base before the first wave; four normally upgraded Tesla and one EMP; preserve staggered air-only boss approach. No enemy/resource/time injection.");
  var main=s.Content.MainBase;Upgrade(s,main,10);string owner=Id(main);
  Vector3 forward=WorldGridGeometry.Rotation*Vector3.forward,side=WorldGridGeometry.Rotation*Vector3.right;
  Vector3 mainArea=AreaCenter(main);
  Place(s,"resource.iron",owner,mainArea+side*6);Place(s,"railway.coal",owner,mainArea-side*6);
  for(int i=0;i<4;i++)Place(s,"resource.power_generator",owner,mainArea+forward*(i-2)*2);
  AddLegacyRing();
  for(int i=0;i<4;i++){
   var tower=Place(s,"defense.tesla",owner,main.Position+forward*(10+(i/2)*3)+side*((i%2)*2-1)*5,main);
   Upgrade(s,tower,5);
  }
  return FinishCore();
 }
 public static string FinishCore()
 {
  var s=World();var main=s.Content.MainBase;string owner=Id(main);
  if(s.Content.GroundWorld.Buildings.Any(b=>b.DefinitionId=="defense.emp"||b.DefinitionId=="installation.nexus"))throw new Exception("Core follow-up already applied; inspect before continuing");
  if(s.Towers.Count!=4)throw new Exception("All four normally placed legacy towers required");
  Vector3 forward=WorldGridGeometry.Rotation*Vector3.forward,side=WorldGridGeometry.Rotation*Vector3.right;
  var emp=Place(s,"defense.emp",owner,main.Position+forward*17,main,24);Upgrade(s,emp,3);
  var bossDirection=s.Assault.BossPosition-main.Position;bossDirection.y=0;bossDirection.Normalize();
  var front=Place(s,"installation.nexus",owner,main.Position+bossDirection*50);Upgrade(s,front,10);string frontOwner=Id(front);
  for(int i=0;i<4;i++)Place(s,"resource.power_generator",frontOwner,front.Position+side*(i-2)*3);
  Place(s,"defense.vulcan_aa",frontOwner,front.Position+bossDirection*15);
  Place(s,"defense.sky_plasma",frontOwner,front.Position-bossDirection*10);
  var frontTesla=Place(s,"defense.tesla",frontOwner,front.Position+side*8,front);Upgrade(s,frontTesla,3);Place(s,"defense.emp",frontOwner,front.Position-side*8,front);
  return Status();
 }
 public static string AddLegacyRing()
 {
  var s=World();if(s.Towers.Count!=0)throw new Exception("Legacy ring already installed");
  var main=s.Content.MainBase;var area=main.Position;var forward=WorldGridGeometry.Rotation*Vector3.forward;var side=WorldGridGeometry.Rotation*Vector3.right;
  var kinds=new[]{HordeTowerKind.MachineGun,HordeTowerKind.Cannon,HordeTowerKind.Frost,HordeTowerKind.Arrow};
  for(int i=0;i<4;i++)Legacy(s,kinds[i],Id(main),area+side*((i%2)*2-1)*6+forward*(6+(i/2)*3),main);
  Mark("coverage-legacy-ring","Normal ground placement of all four legacy types before first wave, within 16m of the actual main base; cumulative counters observed independently.");return Status();
 }
 public static string Status()
 {
  var s=World();return new JObject{{"day",s.Clock.Day},{"phase",s.Clock.Phase.ToString()},{"alive",s.Enemies.Alive},{"stage",s.Assault.Energy.Stage.ToString()},
   {"buildings",new JArray(s.Content.Bases.Buildings.Select(b=>new JObject{{"id",b.PersistentId},{"definition",b.DefinitionId},{"position",new JObject{{"x",b.Position.x},{"y",b.Position.y},{"z",b.Position.z}}},{"operational",b.Operational},{"block",b.OperationBlock.ToString()}}))}}.ToString();
 }
 public static string Describe()=>"Prepared normal content-coverage commands; requires a separate content-coverage recording. Describe does not access or change the live world.";
}
