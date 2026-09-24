using System;
using System.Linq;
using EternalSteam;
using UnityEngine;
using UnityEngine.UIElements;
using EternalSteam.OpenWorld;
public static class VerifyHudFolds
{
 public static string Main(){
  var hud=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>();var root=hud.GetComponent<UIDocument>().rootVisualElement;
  foreach(var key in new[]{"clock","resource","test"}){
   var button=root.Q<Button>(key+"-fold");typeof(Clickable).GetMethod("Invoke",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(button.clickable,new object[]{null});
   if(root.Q(key+"-body").style.display!=DisplayStyle.None)throw new Exception(key+" did not fold");
   typeof(Clickable).GetMethod("Invoke",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(button.clickable,new object[]{null});
   if(root.Q(key+"-body").style.display!=DisplayStyle.Flex)throw new Exception(key+" did not unfold");
  }
  hud.Input.BeginEditing();
  var sandbox=hud.Sandbox;var definition=sandbox.ContentCatalog.Buildings.Single(d=>d.Id=="resource.power_generator");bool reserved=false;
  for(int z=-50;z<60&&!reserved;z++)for(int x=-50;x<60&&!reserved;x++){
   var position=sandbox.Content.GroundWorld.Grid.Center(new Vector2Int(x,z),definition.Footprint);
   if(sandbox.Content.HasBuildArea(position,definition.Footprint))reserved=hud.Input.Edits.AddContent(definition,position,Vector3.forward,out _);
  }
  if(!reserved||hud.Input.Edits.Count!=1)throw new Exception("Nonempty placement reservation required");
  int before=hud.Input.Edits.Count;var pending=hud.Input.Edits.ContentPending[0];
  var fold=root.Q<Button>("inventory-fold");var invoke=typeof(Clickable).GetMethod("Invoke",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
  invoke.Invoke(fold.clickable,new object[]{null});if(hud.Inventory.Visible||!hud.Input.IsEditing||hud.Input.Edits.Count!=before)throw new Exception("Inventory fold changed editing");
  invoke.Invoke(fold.clickable,new object[]{null});if(!hud.Inventory.Visible||hud.Input.Edits.ContentPending[0]!=pending)throw new Exception("Inventory unfold lost pending object");hud.Input.Cancel();if(hud.Input.Edits.Count!=0||sandbox.Content.GroundPlacement.Pending.Count!=0)throw new Exception("Cancel leaked reservations");
  return "PASS: bound UI button callbacks folds/unfolds all three upper panel bodies; lower fold preserves an actual pending building, cancel releases reservations";
 }
}
