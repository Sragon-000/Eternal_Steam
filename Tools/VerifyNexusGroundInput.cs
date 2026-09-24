using System;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class VerifyNexusGroundInput
{
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 public static string Main(){var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var input=s.GetComponent<OpenWorldInput>();Check(s.Content.GroundWorld.Buildings.Count==0&&s.Foundations.Platforms.Count==0,"Empty validation scene required");input.Cancel();
 Vector3 Find(BuildingDefinition d){var o=s.Ground.transform.position;var size=s.Ground.terrainData.size;for(float z=o.z+8;z<o.z+size.z-8;z+=2)for(float x=o.x+8;x<o.x+size.x-8;x+=2){var p=new Vector3(x,0,z);if(s.Content.Resolve(d,p,out var session,out var world,out var cell,out _)&&world==s.Content.GroundWorld&&session.Validate(new PlacementRequest(-1,d,cell)).Success)return p;}throw new Exception("No valid terrain for "+d.Id);}
 try{var nexus=s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.nexus");input.BeginEditing();input.SelectContent(nexus);Check(input.ClickWorld(Find(nexus))&&input.Confirm(),"Nexus click workflow "+s.Message);
 var defense=s.ContentCatalog.Buildings.First(d=>d.Category==BuildingCategory.Defense&&d.Footprint.x==3);input.BeginEditing();input.SelectContent(defense);var point=Find(defense);Check(input.ClickWorld(point)&&!input.ChoosingDirection,"Single click ground placement without direction");Check(input.Confirm(),"Ground single click confirm "+s.Message);var b=s.Content.GroundWorld.Buildings.Single(x=>x.Module<WeaponRuntime>()!=null);input.BeginEditing();Check(input.ClickWorld(b.Position)&&!b.Disposed,"Ground deferred click recovery");input.Cancel();Check(!b.Disposed,"Recovery cancellation");input.BeginEditing();Check(input.ClickWorld(b.Position)&&input.Confirm()&&b.Disposed,"Recovery commit");
 var old=s.Foundations.Definition(EternalSteam.Demo.HordeTowerKind.MachineGun);point=Find(old);input.BeginEditing();input.Select(WorldTool.Tower);Check(input.ClickWorld(point)&&!input.ChoosingDirection&&input.Confirm(),"Legacy ground single-click commit "+s.Message);Check(s.Towers.Count==1&&s.Foundations.Platforms.Count==0,"Legacy ground runtime");b=s.Towers[0].building;input.BeginEditing();Check(input.ClickWorld(b.Position)&&input.Confirm()&&s.Towers.Count==0,"Legacy click recovery");return "PASS: actual sandbox input, nexus click commit, 9-cell ground tower single-click/confirm, deferred recovery cancel/commit, legacy ground tower single-click placement and recovery, no foundations required.";
 }finally{input.Cancel();foreach(var b in s.Content.GroundWorld.Buildings.ToArray())s.Content.GroundWorld.Remove(b.Id);}
 }
}
