using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Demo;
public static class VerifyBuildingInventoryUI
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static async Task<string> Main()
 {
  var hud=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>();var s=hud.Sandbox;var input=hud.Input;var doc=hud.GetComponent<UIDocument>();var root=doc.rootVisualElement;
  async Task Click(string name){await Task.Delay(40);var b=root.Q<Button>(name);Check(b!=null&&b.enabledInHierarchy,"Clickable "+name);using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}await Task.Delay(40);}
  input.Cancel();s.ResetEnemies();hud.Inventory.SetVisible(true);hud.Refresh();
  Check(hud.Inventory.Category==BuildingCategory.Defense,"Default defense");Check(root.Q("inventory-items").childCount==s.TowerPrefabs.Length+(s.ContentCatalog?.Buildings.Count(d=>d.Category==BuildingCategory.Defense)??0),"Registered defense prefabs");
  Check(!root.Q<Button>("confirm").enabledSelf&&root.Q<Button>("edit").enabledSelf,"Initial edit only");
  var names=new[]{"category-defense","category-resource","category-other","category-installation"};for(int i=1;i<4;i++)Check(root.Q(names[i]).worldBound.x>root.Q(names[i-1]).worldBound.x,"Document tab order");
  Check(root.Q("edit").worldBound.x<root.Q("confirm").worldBound.x,"Right edit then confirm");
  await Click("category-other");Check(root.Q("inventory-items").childCount==0&&root.Q("inventory-empty").resolvedStyle.display!=DisplayStyle.None,"Empty category");
  await Click("inventory-toggle");await Click("inventory-toggle");Check(hud.Inventory.Category==BuildingCategory.Defense,"Reopen resets defense");
  await Click("edit");Check(input.IsEditing&&root.Q<Button>("edit").enabledSelf&&root.Q<Button>("confirm").enabledSelf,"Editing keeps toggle and confirm enabled");
  await Click("edit");Check(!input.IsEditing&&!root.Q<Button>("confirm").enabledSelf,"Second edit cancels empty session");await Click("edit");
  await Click("confirm");Check(!input.IsEditing&&!root.Q<Button>("confirm").enabledSelf,"Empty edit confirm exits");
  await Click("category-installation");await Click("building-foundation");Check(!input.IsEditing,"Selection alone does not edit");await Click("edit");Check(input.Tool==WorldTool.Edit,"Edit always starts neutral for recovery");await Click("building-foundation");Check(input.Tool==WorldTool.Foundation,"Select foundation after edit");
  Vector3 point=default;bool found=false;
  for(int z=-4;z<=4&&!found;z++)for(int x=-4;x<=4&&!found;x++){var p=WorldGridGeometry.Center(new Vector2Int(x,z),8);if(s.Foundations.CheckFoundation(p,out _,out _)){point=p;found=true;}}
  Check(found,"Available terrain");int count=s.Foundations.Platforms.Count;Check(input.ClickWorld(point),"Pending foundation");Check(s.Foundations.Platforms.Count==count,"Pending not installed");var foundationGhost=input.Edits.FoundationAt(point).View;
  await Click("edit");Check(!input.IsEditing&&input.Edits.Count==0&&foundationGhost==null&&s.Foundations.Platforms.Count==count,"Edit toggle discards foundation ghost without installation");
  await Click("edit");await Click("building-foundation");Check(input.ClickWorld(point),"Foundation can be queued again");await Click("confirm");Check(s.Foundations.Platforms.Count==count+1&&!input.IsEditing,"Foundation UI commit");
  s.Foundations.FindCell(point,out var platform,out _,out _);var cell=platform.World.Grid.Center(Vector2Int.zero,Vector2Int.one)+Vector3.up*.01f;
  await Click("category-defense");await Click("edit");await Click("building-MachineGun");Check(input.ClickWorld(cell)&&input.ClickWorld(cell+Vector3.forward*10),"Pending tower");var towerGhost=input.Edits.Towers[0].View;Check(platform.World.Grid.ReservationCount>0,"Pending tower reserves cells");
  await Click("edit");Check(!input.IsEditing&&input.Edits.Count==0&&towerGhost==null&&platform.World.Grid.ReservationCount==0&&platform.World.Buildings.Count==0&&s.Foundations.Platforms.Contains(platform),"Edit toggle removes tower ghost and reservation, preserves foundation");
  await Click("edit");await Click("building-MachineGun");Check(input.ClickWorld(cell)&&input.ClickWorld(cell+Vector3.forward*10),"Released cell reusable");await Click("confirm");Check(platform.World.Buildings.Count==1,"Tower UI commit");
  Check(root.Q("edit-tools")==null&&root.Q<Button>("recover")==null&&root.Q<Button>("cancel")==null,"Auxiliary buttons removed");
  input.ClickWorld(cell);Check(platform.World.Buildings.Count==1,"Explore click preserves tower");
  await Click("edit");Check(input.ClickWorld(cell),"Existing tower auto recovery selection");
  Check(platform.World.Buildings.Count==1&&platform.World.Grid.OccupiedCount==1&&input.Edits.RecoveryCount==1,"Selection preserves installed tower and occupancy");
  Check(input.ClickWorld(cell)&&input.Edits.Count==0,"Second click deselects recovery");
  Check(input.ClickWorld(cell),"Select recovery again");await Click("edit");Check(platform.World.Buildings.Count==1&&input.Edits.Count==0,"Edit toggle cancels recovery");
  await Click("edit");Check(input.ClickWorld(cell),"Recovery selected");await Click("confirm");Check(platform.World.Buildings.Count==0&&platform.World.Grid.OccupiedCount==0,"Confirm recovers tower only");
  await Click("edit");await Click("building-MachineGun");Check(input.ClickWorld(cell)&&input.ClickWorld(cell+Vector3.forward*10),"Place again after recovery");await Click("confirm");
  var emptyCell=platform.World.Grid.Center(new Vector2Int(3,3),Vector2Int.one);
  await Click("edit");Check(input.ClickWorld(emptyCell),"Foundation selects itself and its tower");
  Check(s.Foundations.Platforms.Contains(platform)&&platform.World.Buildings.Count==1&&input.Edits.RecoveryCount==2,"Foundation recovery deferred");
  Check(input.ClickWorld(emptyCell)&&input.Edits.Count==0,"Foundation deselect clears child selections");
  Check(input.ClickWorld(emptyCell),"Foundation reselect");await Click("edit");Check(s.Foundations.Platforms.Contains(platform)&&platform.World.Buildings.Count==1,"Cancel preserves foundation and tower");
  await Click("edit");Check(input.ClickWorld(emptyCell),"Foundation final selection");await Click("confirm");
  Check(!s.Foundations.Platforms.Contains(platform)&&platform.World.Buildings.Count==0&&platform.World.Grid.OccupiedCount==0,"Confirm recovers foundation and towers together");
  // Empty foundations must also confirm without an empty RecoverySession failure.
  await Click("edit");await Click("category-installation");await Click("building-foundation");Check(input.ClickWorld(point),"Empty foundation pending");await Click("confirm");
  s.Foundations.FindCell(point,out var emptyPlatform,out _,out _);await Click("edit");Check(input.ClickWorld(point),"Empty foundation recovery selection");await Click("confirm");Check(!s.Foundations.Platforms.Contains(emptyPlatform),"Empty foundation recovery commit");
  // Render a separate inventory populated with nine entries; do not invent game content.
  var test=doc.visualTreeAsset.CloneTree();test.style.position=Position.Absolute;test.style.left=0;test.style.top=0;test.style.width=1000;test.style.height=600;root.Add(test);
  try {
   var entries=Enumerable.Range(0,9).Select(i=>new InventoryBuilding{Id=i.ToString(),Name="검증 "+i,Category=BuildingCategory.Defense});
   var view=new BuildingInventoryView(test,entries,_=>{});await Task.Delay(100);var items=test.Q("inventory-items");
   Check(items.childCount==9,"Nine entries generated");Check(Mathf.Abs(items[0].worldBound.y-items[7].worldBound.y)<1,"First eight on same row");Check(items[8].worldBound.y>items[0].worldBound.y+20,"Ninth wraps to next row");
  } finally {test.RemoveFromHierarchy();}
  input.Cancel();hud.Inventory.SetVisible(true);hud.Refresh();
  return "PASS: category order/default/filter/empty/reopen, edit toggle cancels foundation/tower ghosts and reservations, edit-confirm enabled states, empty confirm, tower/foundation deferred click recovery, deselection/cancel, foundation with towers and empty foundation commit, removed auxiliary buttons, nine items wrap after eight, cleanup.";
 }
}
