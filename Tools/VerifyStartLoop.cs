using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class VerifyStartLoop
{
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 public static string Main(){
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(Application.isPlaying,"Play required");var input=s.GetComponent<OpenWorldInput>();var main=s.Content.MainBase;
  Check(main!=null&&main.Active&&s.Content.GroundWorld.Buildings.Count==1,"Exactly one automatic main");Check(s.Content.Views[main]==s.StartingBase.gameObject,"Authored model adopted without duplicate");
  Check(!s.Content.GroundPlacement.Add(s.StartingBase.Definition,new Vector2Int(0,8),out _).Success,"Manual main blocked");
  var area=main.Module<IBuildArea>();var geometry=main.Module<IBuildAreaGeometry>();Check(area!=null&&geometry!=null,"Common spatial capability");
  var front=main.Position+WorldGridGeometry.ToWorld(new Vector3(0,0,4));var near=geometry.Center-WorldGridGeometry.ToWorld(new Vector3(0,0,11));Check(Math.Abs(WorldGridGeometry.ToLocal(near-front).z)<.001,"Area starts at front edge");
  int cells=0;for(int z=-60;z<60;z++)for(int x=-60;x<60;x++){var p=s.Content.GroundWorld.Grid.Center(new Vector2Int(x,z),Vector2Int.one);if(area.Contains(p,Vector2.one,WorldGridGeometry.Rotation))cells++;}Check(cells==121,"Exactly 121 fully contained cells: "+cells);Check(!area.Contains(main.Position,Vector2.zero,WorldGridGeometry.Rotation),"No center-based rear coverage");
  BuildingInstance Install(BuildingDefinition def){for(int z=-50;z<60;z++)for(int x=-50;x<60;x++){var cell=new Vector2Int(x,z);if(!area.Contains(s.Content.GroundWorld.Grid.Center(cell,def.Footprint),(Vector2)def.Footprint,WorldGridGeometry.Rotation))continue;if(!s.Content.GroundPlacement.Add(def,cell,out _).Success)continue;var r=s.Content.GroundPlacement.Confirm();Check(r.Success,"Commit "+def.Id+": "+r.Message);return s.Content.GroundWorld.Buildings.Last();}throw new Exception("No placement: "+def.Id);}
  var generator=Install(s.ContentCatalog.Buildings.Single(d=>d.Id=="resource.power_generator"));
  var tower=Install(s.ContentCatalog.Buildings.First(d=>d.Modules.Any(m=>m is WeaponModuleDefinition)&&d.Placement.RequiredNexusLevel==1));
  var resourceDef=s.ContentCatalog.Buildings.First(d=>d.Modules.Any(m=>m is ProductionModuleDefinition)&&d.Placement.RequiredNexusLevel==1);var resource=Install(resourceDef);var recipe=resourceDef.Modules.OfType<ProductionModuleDefinition>().Single();var stock=s.Content.Resources.Amount(recipe.OutputId);
  Check(!CombatPermission.Allows(tower),"Starts without credit");
  input.BeginEditing();var elapsed=s.Clock.RemainingSeconds;var energy=s.Assault.Energy.Extracted;s.AdvanceSimulation(1);Check(s.Clock.RemainingSeconds==elapsed&&s.Assault.Energy.Extracted==energy,"Editing freezes whole loop");
  var hud=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>();var ui=hud.GetComponent<UIDocument>().rootVisualElement;
  Check(hud.Inventory!=null,"HUD started");hud.Inventory.SelectCategory(BuildingCategory.Installation);Check(ui.Q<Button>("building-installation.main_base")==null,"No manual main entry");
  hud.Inventory.SetVisible(false);Check(input.IsEditing,"Fold does not cancel edit");Check(ui.Q("inventory-scroll").style.display==DisplayStyle.None&&ui.Q<Button>("edit").hierarchy.parent!=null,"Header retained");hud.Inventory.SetVisible(true);Check(hud.Inventory.Category==BuildingCategory.Installation,"Fold preserves category");
  input.Cancel();s.Clock.Paused=false;s.AdvanceSimulation(1.01f);Check(CombatPermission.Allows(tower)&&generator.Operational&&resource.Operational,"Forward coverage powers production/defense");Check(s.Assault.Energy.Extracted>energy,"Extraction resumes after editing");
  for(float t=0;t<recipe.Interval+.1f;t+=.1f)s.AdvanceSimulation(.1f);Check(s.Content.Resources.Amount(recipe.OutputId)>stock,"Actual production credited to resource bank");
  s.Clock.Paused=true;elapsed=s.Clock.RemainingSeconds;double balance=main.Module<PowerModule>().Stored;s.AdvanceSimulation(1);Check(s.Clock.RemainingSeconds==elapsed&&main.Module<PowerModule>().Stored==balance,"Clock pause freezes all systems");
  s.Clock.Paused=false;s.Clock.SetPhase(DayPhase.Night);s.AdvanceSimulation(.4f);Check(s.Assault.Planner.Points.Count==2,"Two valid night spawn points, actual count "+s.Assault.Planner.Points.Count);Check(s.Enemies.Alive>0,"Night enemies enter from safe exterior");
  s.Assault.Energy.RewardKill(long.MaxValue);s.AdvanceSimulation(.1f);Check(s.Assault.BossId>=0,"Actual boss spawned: "+s.Assault.BossSpawnFailure);s.Enemies.ApplyDamage(s.Assault.BossId,int.MaxValue);Check(s.Assault.Craft()&&s.Assault.Energy.PerfectOrb,"Boss kill to orb clear");
  hud.Refresh();s.Clock.Paused=true;
  return $"PASS: fixed model adopted, exact 11x11 forward cells, generator + resource + defense, edit/pause freeze and resume, fold state preservation, night spawns ({s.Assault.Coverage.Eligible.Count(x=>x)} eligible cells), boss and orb clear. Deterministic production update path.";
 }
}
