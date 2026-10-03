using System;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;

// Recorder QA only: invokes normal upgrade/edit commands, never injects stocks or time.
public static class VerifyRecorderProduction
{
 public static string Main()
 {
  var root=Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT");
  if(!Application.isPlaying||root==null||!root.EndsWith("20261003-recorder-production/save",StringComparison.Ordinal))throw new Exception("Dedicated recorder QA required");
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
  if(s.Content.InfiniteResources||s.Content.Defeated||Time.timeScale!=1)throw new Exception("Normal game rules required");
  var c=s.Content;var owner=c.MainBase.Module<IBaseIdentity>().BaseId;
  while(c.MainBase.Module<IUpgradeControl>().Level<3)
   if(!s.Persistence.Upgrades.TryUpgrade(c.MainBase,out var why))throw new Exception(why);
  c.Bases.Refresh();c.Bases.Select(owner);
  var input=s.GetComponent<OpenWorldInput>();var definition=s.ContentCatalog.Buildings.Single(d=>d.Id=="resource.iron");
  input.BeginEditing();bool added=false;
  for(int z=15;z<=35&&!added;z++)for(int x=-8;x<=12&&!added;x++)
  {
   var cell=new Vector2Int(x,z);var center=c.GroundWorld.Grid.Center(cell,definition.Footprint);
   if(!c.Bases.Covers(center,(Vector2)definition.Footprint,WorldGridGeometry.Rotation,owner))continue;
   var point=c.GroundWorld.Grid.Center(cell,Vector2Int.one);point.y=s.Ground.SampleHeight(point)+s.Ground.transform.position.y;
   added=input.Edits.AddContent(definition,point,WorldGridGeometry.Rotation*Vector3.forward,out _);
  }
  if(!added||!input.Confirm())throw new Exception("Normal placement failed: "+s.Message);
  var b=c.GroundWorld.Buildings.Last();
  if(!b.Operational)throw new Exception("Producer blocked: "+b.OperationBlock);
  return "Normal free-policy upgrade to Lv3 and covered iron production placement; actual production must be observed separately.";
 }
}
