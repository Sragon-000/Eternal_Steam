using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class VerifyDocumentContent
{
 static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
 public static async Task<string> Main()
 {
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var input=s.GetComponent<OpenWorldInput>();var hud=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>();input.Cancel();s.ResetEnemies();s.Content.LevelCap=8;
  Check(s.ContentCatalog!=null&&s.ContentCatalog.Buildings.Count==16&&s.ContentCatalog.Validate().Count==0,"Catalog 16 valid definitions");
  hud.Inventory.SelectCategory(BuildingCategory.Resource);Check(hud.GetComponent<UIDocument>().rootVisualElement.Q("inventory-items").childCount==7,"Seven generated resource entries");
  var iron=s.ContentCatalog.Buildings.First(d=>d.Id=="resource.iron");var copper=s.ContentCatalog.Buildings.First(d=>d.Id=="resource.copper");
  s.Content.LevelCap=1;Check(!s.Content.Resolve(copper,Vector3.zero,out _,out _,out _,out _),"Nexus unlock gate");s.Content.LevelCap=8;
  Vector3 FreeGround(BuildingDefinition d){for(int z=-45;z<=45;z++)for(int x=-45;x<=45;x++){var cell=new Vector2Int(x,z);if(s.Content.CheckGround(cell,d.Footprint,out _,out _)&&!s.Content.GroundWorld.Grid.IsOccupied(cell))return s.Content.GroundWorld.Grid.Center(cell,d.Footprint);}throw new Exception("No free ground");}
  foreach(var d in s.ContentCatalog.Buildings.Where(d=>d.Category==BuildingCategory.Resource)) {
   var p=FreeGround(d);var recipe=(ProductionModuleDefinition)d.Modules[0];double before=s.Content.Resources.Amount(recipe.OutputId);
   input.BeginEditing();input.SelectContent(d);Check(input.ClickWorld(p),"Resource ghost "+d.Id+": "+s.Message);Check(s.Content.GroundWorld.Buildings.Count==0,"Ghost has no runtime");s.Content.Tick(130);Check(s.Content.Resources.Amount(recipe.OutputId)==before,"No pending production");
   Check(input.Confirm(),"Resource confirm "+s.Message);var b=s.Content.GroundWorld.Buildings.Single();s.Content.Tick(recipe.Interval-.01f);Check(s.Content.Resources.Amount(recipe.OutputId)==before,"Before production boundary");s.Content.Tick(.02f);Check(s.Content.Resources.Amount(recipe.OutputId)==before+1,"Exact recipe gain");
   input.BeginEditing();Check(input.ClickWorld(b.Position),"Ground recovery selected");Check(!b.Disposed,"Recovery deferred");input.Cancel();Check(!b.Disposed,"Recovery cancelled");input.BeginEditing();Check(input.ClickWorld(b.Position)&&input.Confirm(),"Ground recovery commit");Check(b.Disposed&&s.Content.GroundWorld.Grid.OccupiedCount==0,"Production runtime removed");
  }
  Vector3 foundation=default;bool found=false;for(int z=-10;z<=10&&!found;z++)for(int x=-10;x<=10&&!found;x++){var p=WorldGridGeometry.Center(new Vector2Int(x,z),8);if(s.Foundations.AddFoundation(p,out _)){foundation=p;found=true;}}
  Check(found,"Foundation available");s.Foundations.FindCell(foundation,out var platform,out _,out _);
  try {
   foreach(var d in s.ContentCatalog.Buildings.Where(d=>d.Category==BuildingCategory.Defense)) {
    s.ResetEnemies();var anchor=platform.World.Grid.Center(Vector2Int.zero,Vector2Int.one);input.BeginEditing();input.SelectContent(d);
    Check(input.ClickWorld(anchor)&&input.ClickWorld(anchor+Vector3.forward*20),"Defense ghost "+d.Id+": "+s.Message);Check(platform.World.Grid.ReservationCount==d.Footprint.x*d.Footprint.y,"Multi-cell reservation");Check(input.Confirm(),"Defense commit "+s.Message);
    var building=platform.World.Buildings.Single();Check(platform.World.Grid.OccupiedCount==d.Footprint.x*d.Footprint.y,"Multi-cell occupied");
    var weapon=(WeaponModuleDefinition)d.Modules[0];bool air=weapon.Targets==TargetKind.Air;
    for(int i=0;i<4;i++)Check(s.Enemies.TrySpawn(building.Position+Vector3.forward*(5+i),building.Position+Vector3.forward*40,0,10000,air),"Enemy spawn");
    s.Enemies.MoveAndIndex(0);for(int tick=0;tick<80;tick++){s.TargetAdapter.Tick(.1f);building.Tick(.1f);if(tick==0&&weapon.Delivery==WeaponDelivery.Pulse)Check(s.Enemies.GetEnemy(0).movementPenalty==1,"EMP immediately stuns");}
    if(weapon.Delivery==WeaponDelivery.Pulse){bool stopped=false;for(int i=0;i<4;i++)stopped|=s.Enemies.GetEnemy(i).movementPenalty>0;building.Tick(3);s.TargetAdapter.Tick(.01f);building.Tick(.01f);Check(s.Enemies.GetEnemy(0).health==10000,"EMP has zero damage");}
    else Check(Enumerable.Range(0,4).Any(i=>s.Enemies.GetEnemy(i).health<10000),"Weapon actually damages "+d.Id);
    var upgrade=building.Module<IUpgradeControl>();Check(upgrade.TryUpgrade(out _),"Independent upgrade");while(upgrade.TryUpgrade(out _)){}Check(upgrade.Level==upgrade.MaximumLevel,"Maximum level");
    input.BeginEditing();Check(input.ClickWorld(anchor),"Generic tower recovery selection");Check(!building.Disposed,"Generic recovery deferred");Check(input.Confirm(),"Generic recovery commit");Check(building.Disposed&&platform.World.Buildings.Count==0,"Generic removal");
   }
   // A generic tower must participate in whole-foundation recovery.
   var large=s.ContentCatalog.Buildings.First(d=>d.Id=="defense.smart_missile");var largeAnchor=platform.World.Grid.Center(Vector2Int.zero,Vector2Int.one);input.BeginEditing();input.SelectContent(large);Check(input.ClickWorld(largeAnchor)&&input.ClickWorld(largeAnchor+Vector3.forward*20)&&input.Confirm(),"Large tower setup");
   input.BeginEditing();var empty=platform.World.Grid.Center(new Vector2Int(3,3),Vector2Int.one);Check(input.ClickWorld(empty)&&input.Confirm(),"Foundation with generic tower recovery");Check(!s.Foundations.Platforms.Contains(platform)&&platform.World.Buildings.Count==0,"Foundation clears generic runtimes");
  } finally {if(s.Foundations.Platforms.Contains(platform))s.Foundations.RemoveFoundation(platform);input.Cancel();s.ResetEnemies();hud.Inventory.SetVisible(true);}
  await Task.Delay(100);return "PASS: 16 catalog definitions, 7 resource entries, unlock gate, all 7 production intervals, pending inactivity, deferred recovery/cancel, all 9 weapon damage/control compositions, 1/4/9-cell reservations, upgrade limits, generic tower + foundation cleanup.";
 }
}
