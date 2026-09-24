using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Demo;
public static class VerifyNexusConstruction
{
 static void Check(bool v,string msg){if(!v)throw new Exception(msg);}
 public static string Main(){Check(Application.isPlaying,"Play required");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var root=new GameObject("Nexus isolated regression");var data=new TerrainData{heightmapResolution=33,size=new Vector3(256,10,256)};var go=Terrain.CreateTerrainGameObject(data);go.transform.position=new Vector3(2000,0,2000);var terrain=go.GetComponent<Terrain>();var towers=new List<HordeTower>();var content=new OpenWorldContent(terrain,root.transform,s.LineMaterial,null,8);var placement=new FoundationPlacement(terrain,root.transform,towers,s.FoundationMaterial,s.BarrelMaterial,s.LineMaterial,s.ValidMaterial,new Material[4],new Material[4],s.FoundationPrefab,s.TowerPrefabs,content);content.Attach(placement);var edit=new WorldEditSession(placement,s.LineMaterial,content);
 try{
 var def=AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/EternalSteam/Content/Buildings/DocumentContent/installation.nexus.asset");Check(def!=null&&def.Validate().Count==0,"Nexus catalog definition");var point=new Vector3(2100,0,2100);
 Check(!placement.CheckFoundation(point,out _,out _),"No nexus rejects foundation");
 Check(edit.AddContent(def,point,Vector3.forward,out var reason),reason);Check(!content.CanBuildFoundation(point),"Pending nexus grants no area");edit.Cancel();Check(content.GroundWorld.Buildings.Count==0&&content.GroundWorld.Grid.ReservationCount==0,"Nexus cancel releases reservations");
 Check(edit.AddContent(def,point,Vector3.forward,out reason),reason);Check(edit.Confirm().Success,"Commit nexus");var nexus=content.GroundWorld.Buildings.Single();var area=nexus.Module<IBuildArea>();Check(area!=null&&area.Radius==40,"Radius copied");
 Check(area.Contains(nexus.Position+Vector3.right*40,Vector2.zero,Quaternion.identity),"Inclusive circle edge");Check(!area.Contains(nexus.Position+Vector3.right*40.01f,Vector2.zero,Quaternion.identity),"Outside circle");Check(!area.Contains(nexus.Position+Vector3.right*38,new Vector2(4,4),WorldGridGeometry.Rotation),"Center inside but corners outside rejected");
 Vector3 inside=default,outside=default;bool found=false,foundOut=false;
 for(int z=-8;z<=8;z++)for(int x=-8;x<=8;x++){var p=nexus.Position+WorldGridGeometry.ToWorld(new Vector3(x*8,0,z*8));if(!WorldGridGeometry.TerrainPlacement(terrain,p,out var center,out _))continue;if(placement.CheckFoundation(p,out _,out _)){inside=p;found=true;}else if(!content.CanBuildFoundation(center)&&content.FoundationClear(center)){outside=p;foundOut=true;}}
 Check(found&&foundOut,"Inside/outside terrain available");Check(!edit.AddFoundation(outside,out _),"Outside ghost rejected");Check(edit.AddFoundation(inside,out reason),reason);Check(!edit.Move(edit.FoundationAt(inside),outside,out _),"Move outside fails");Check(edit.FoundationAt(inside)!=null,"Failed move keeps original");Check(edit.Confirm().Success,"Inside foundation commits");var platform=placement.Platforms.Single();
 Check(edit.ToggleGroundRecovery(nexus,out reason),reason);Check(!edit.Confirm().Success&&!nexus.Disposed&&placement.Platforms.Count==1,"Dependent foundation blocks nexus recovery atomically");edit.Cancel();
 Check(edit.ToggleGroundRecovery(nexus,out reason),reason);Check(edit.ToggleFoundationRecovery(platform,towers,out reason),reason);Check(edit.Confirm().Success,"Combined foundation+nexus recovery");Check(nexus.Disposed&&placement.Platforms.Count==0,"No orphaned foundation");Check(!content.CanBuildFoundation(inside),"Recovered area removed");
 Check(edit.AddContent(def,point,Vector3.forward,out reason)&&edit.Confirm().Success,"Reinstall nexus");nexus=content.GroundWorld.Buildings.Single();Check(edit.AddFoundation(inside,out reason),reason);nexus.Destroy();Check(!edit.Confirm().Success&&edit.Count==1&&placement.Platforms.Count==0,"Commit revalidates lost nexus and retains pending");edit.Cancel();
 return "PASS: nexus catalog, no/pending/confirmed area, cancellation, inclusive XZ edge and rotated corners, out-of-range add/move, successful foundation, dependent recovery blocked, grouped recovery, missing nexus commit rollback.";
 }finally{edit.Dispose();placement.Dispose();content.Dispose();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(go);UnityEngine.Object.Destroy(data);}
 }
}
