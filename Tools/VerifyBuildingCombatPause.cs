using System;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class VerifyBuildingCombatPause
{
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 public static string Main(){var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var input=s.GetComponent<OpenWorldInput>();Check(s.Content.GroundWorld.Buildings.Count==0&&s.Foundations.Platforms.Count==0,"Empty validation scene required");input.Cancel();
 Vector3 Find(BuildingDefinition d){var o=s.Ground.transform.position;var size=s.Ground.terrainData.size;for(float z=o.z+8;z<o.z+size.z-8;z+=2)for(float x=o.x+8;x<o.x+size.x-8;x+=2){var p=new Vector3(x,0,z);if(s.Content.Resolve(d,p,out var session,out var world,out var cell,out _)&&world==s.Content.GroundWorld&&session.Validate(new PlacementRequest(-1,d,cell)).Success)return p;}throw new Exception("No valid terrain for "+d.Id);}
 try{var nexus=s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.nexus");input.BeginEditing();input.SelectContent(nexus);Check(input.ClickWorld(Find(nexus))&&input.Confirm(),"Nexus click workflow "+s.Message);

 var b=s.Content.GroundWorld.Buildings.Single();Check(s.Enemies.TrySpawn(b.Position+WorldGridGeometry.ToWorld(Vector3.right*5),b.Position,2,20,true),"Spawn beside nexus");
 s.Running=false;s.EnemyAttacks.Prepare(.9f);s.Enemies.MoveAndIndex(.9f);s.EnemyAttacks.Attack(.9f);Check(b.Module<HealthModule>().Current==1000,"Attack timer armed without early damage");
 var update=typeof(OpenWorldSandbox).GetMethod("Update",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);for(int i=0;i<20;i++)update.Invoke(s,null);Check(b.Module<HealthModule>().Current==1000&&s.Enemies.Alive==1,"Paused host cannot damage or despawn");
 s.EnemyAttacks.Attack(.1f);Check(b.Module<HealthModule>().Current==999,"Resume remaining attack interval");
 input.ClickWorld(b.Position);var hud=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>();hud.Refresh();var root=hud.GetComponent<UnityEngine.UIElements.UIDocument>().rootVisualElement;
 var label=UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Label>(root,"content-stats");Check(label.text.Contains("999 / 1000")&&label.text.Contains(b.DisplayName),"Selected current/max health and building name");
 return "PASS: real sandbox pause prevents damage, resume completes remaining interval, selected nexus shows name and 999 / 1000 health.";
 }finally{s.ResetEnemies();input.Cancel();foreach(var b in s.Content.GroundWorld.Buildings.ToArray())s.Content.GroundWorld.Remove(b.Id);}
 }
}
