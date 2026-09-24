using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class VerifyResourceStockUI
{
 public static string Main(){
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();if(!Application.isPlaying)throw new Exception("Play required");
  var input=s.GetComponent<OpenWorldInput>();var hud=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>();var root=hud.GetComponent<UIDocument>().rootVisualElement;input.Cancel();
  void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
  try{
   input.BeginEditing();input.SelectContent(s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.nexus"));Check(input.ClickWorld(s.CameraRig.Focus)&&input.Confirm(),"Nexus: "+s.Message);
   var definition=s.ContentCatalog.Buildings.Single(d=>d.Id=="resource.iron");var recipe=definition.Modules.OfType<ProductionModuleDefinition>().Single();
   bool found=false;input.BeginEditing();input.SelectContent(definition);
   for(int z=-40;z<40&&!found;z++)for(int x=-40;x<40&&!found;x++)found=input.ClickWorld(WorldGridGeometry.Center(new Vector2Int(x,z),2))&&input.Edits.ContentPending.Any(p=>p.Request.Definition==definition);
   Check(found,"Resource reservation");double before=s.Content.Resources.Amount(recipe.OutputId);s.Content.TickProduction(recipe.Interval*2);Check(s.Content.Resources.Amount(recipe.OutputId)==before,"Pending does not produce");Check(input.Confirm(),"Confirm producer");
   var producer=s.Content.GroundWorld.Buildings.Single(b=>b.DisplayName==definition.DisplayName);s.Content.Bases.Refresh();Check(producer.Operational,"Producer covered by base");
   s.Content.TickProduction(recipe.Interval);Check(s.Content.Resources.Amount(recipe.OutputId)==before+recipe.OutputAmount,"Actual building production");hud.Refresh();
   var label=root.Q<Label>("resources");Check(label.parent.name=="resource-panel"&&label.text.Contains((before+recipe.OutputAmount).ToString("N0")),"Top UI shows stock");
   input.BeginEditing();Check(input.ClickWorld(producer.Position),"Recovery select");s.Content.TickProduction(recipe.Interval);Check(s.Content.Resources.Amount(recipe.OutputId)==before+recipe.OutputAmount*2,"Recovery pending retains active module");Check(input.Confirm(),"Recovery commit");double saved=s.Content.Resources.Amount(recipe.OutputId);s.Content.TickProduction(recipe.Interval*2);Check(s.Content.Resources.Amount(recipe.OutputId)==saved,"Recovered producer stops");hud.Refresh();
   return "PASS: pending no production, confirmed covered producer adds recipe output, top UXML stock matches, deferred recovery lifecycle, recovered producer stops; manual deterministic production ticks.";
  }finally{input.Cancel();foreach(var b in s.Content.GroundWorld.Buildings.ToArray())s.Content.GroundWorld.Remove(b.Id);}
 }
}
