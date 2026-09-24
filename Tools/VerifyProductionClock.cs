using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class VerifyProductionClock
{
 public static async Task<string> Main()
 {
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var input=s.GetComponent<OpenWorldInput>();input.Cancel();s.ResetEnemies();var definition=s.ContentCatalog.Buildings.First(d=>d.Id=="resource.iron");
  Vector3 point=default;bool found=false;for(int z=-40;z<=40&&!found;z++)for(int x=-40;x<=40&&!found;x++){var cell=new Vector2Int(x,z);if(s.Content.CheckGround(cell,Vector2Int.one,out _,out _)){point=s.Content.GroundWorld.Grid.Center(cell,Vector2Int.one);found=true;}}
  if(!found)throw new Exception("No ground");input.BeginEditing();input.SelectContent(definition);if(!input.ClickWorld(point)||!input.Confirm())throw new Exception(s.Message);var building=s.Content.GroundWorld.Buildings.Single();
  try {double before=s.Content.Resources.Amount("iron");await Task.Delay(2300);if(s.Running||s.Content.Resources.Amount("iron")<=before)throw new Exception("Preparation must auto produce without battle");input.BeginEditing();before=s.Content.Resources.Amount("iron");await Task.Delay(2300);if(s.Content.Resources.Amount("iron")!=before)throw new Exception("Editing must freeze production");return "PASS: confirmed resource auto-produces during preparation without starting combat; editing freezes production.";}
  finally {input.Cancel();s.Content.GroundWorld.Remove(building.Id);}
 }
}
