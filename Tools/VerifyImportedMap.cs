using System;
using System.Linq;
using UnityEngine;
using EternalSteam.OpenWorld;
public static class VerifyImportedMap
{
 public static string Main(){
 var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var input=s.GetComponent<OpenWorldInput>();
 if(!Application.isPlaying)throw new Exception("Play required");
 var bridge=s.Ground.GetComponent<TileWorldGround>();if(bridge.Width!=50||bridge.GridRoot.name!="Imported Map · MapCreater")throw new Exception("Wrong map");
 var focus=s.CameraRig.Focus;input.BeginEditing();input.SelectContent(s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.nexus"));
 try{
 if(!input.ClickWorld(focus)||!input.Confirm())throw new Exception("Nexus placement: "+s.Message);
 if(!s.Spawn("10"))throw new Exception("Spawn request: "+s.Message);
 int alive=s.Enemies.Alive;if(alive==0)throw new Exception("No valid spawn route");
 for(int i=0;i<s.Enemies.MaxCount;i++){var e=s.Enemies.GetEnemy(i);if(e.alive&&!bridge.IsPlayable(e.position))throw new Exception("Spawn outside playable cells");}
 return "PASS: imported prefab instance, nexus placement/confirmation on sampled flat patch, 10 enemy request, actual spawned="+alive+", playable spawn locations.";
 }finally{input.Cancel();s.ResetEnemies();foreach(var b in s.Content.GroundWorld.Buildings.ToArray())s.Content.GroundWorld.Remove(b.Id);}
 }
}
