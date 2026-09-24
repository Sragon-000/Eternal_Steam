using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Demo;
public static class VerifyResourceBuildArea
{
 static void Check(bool v,string msg){if(!v)throw new Exception(msg);}
 public static string Main(){Check(Application.isPlaying,"Play required");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var root=new GameObject("Nexus isolated regression");var data=new TerrainData{heightmapResolution=33,size=new Vector3(256,10,256)};var go=Terrain.CreateTerrainGameObject(data);go.transform.position=new Vector3(2000,0,2000);var terrain=go.GetComponent<Terrain>();var towers=new List<HordeTower>();var content=new OpenWorldContent(terrain,root.transform,s.LineMaterial,null,8);var placement=new FoundationPlacement(terrain,root.transform,towers,s.FoundationMaterial,s.BarrelMaterial,s.LineMaterial,s.ValidMaterial,new Material[4],new Material[4],s.FoundationPrefab,s.TowerPrefabs,content);content.Attach(placement);var edit=new WorldEditSession(placement,s.LineMaterial,content);
 try{
 var catalog=AssetDatabase.LoadAssetAtPath<BuildingCatalog>("Assets/EternalSteam/Content/Buildings/DocumentContent/DocumentBuildings.asset");var resources=catalog.Buildings.Where(d=>d.Category==BuildingCategory.Resource).ToArray();Check(resources.Length==7&&resources.All(d=>d.Placement.RequiresBuildArea),"Seven resource profiles require area");var def=catalog.Buildings.Single(d=>d.Id=="installation.nexus");var resource=resources[0];var point=new Vector3(2100,0,2100);
 Check(!edit.AddContent(resource,point,Vector3.forward,out _),"No nexus rejects resources");Check(edit.AddContent(def,point,Vector3.forward,out var reason),reason);var pending=edit.ContentPending.Single();var center=content.GroundWorld.Grid.Center(pending.Request.Cell,pending.Request.Footprint);var near=center+WorldGridGeometry.ToWorld(new Vector3(12,0,0));
 Check(!edit.AddContent(resource,near,Vector3.forward,out _),"Pending nexus grants no resource area");Check(edit.Confirm().Success,"Nexus commit");var nexus=content.GroundWorld.Buildings.Single();
 Check(!edit.AddContent(resource,center+Vector3.right*45,Vector3.forward,out _),"Out of range rejected");
 Vector2Int edge=default;bool found=false;var area=nexus.Module<IBuildArea>();for(int z=-22;z<=22&&!found;z++)for(int x=-22;x<=22&&!found;x++){var c=content.GroundWorld.Grid.WorldToCell(center)+new Vector2Int(x,z);var pos=content.GroundWorld.Grid.Center(c,resource.Footprint);if(area.Contains(pos,Vector2.zero,WorldGridGeometry.Rotation)&&!content.HasBuildArea(pos,resource.Footprint)){edge=c;found=true;}}
 Check(found,"Boundary fixture");Check(!content.GroundPlacement.Validate(new PlacementRequest(-1,resource,edge)).Success,"Center inside but resource corner outside rejected");
 Check(edit.AddContent(resource,near,Vector3.forward,out reason),reason);Check(edit.Confirm().Success,"Inside resource commits");var building=content.GroundWorld.Buildings.Single(b=>b.RequiresBuildArea);
 Check(edit.ToggleGroundRecovery(nexus,out reason),reason);Check(!edit.Confirm().Success&&!nexus.Disposed&&!building.Disposed,"Resource dependency blocks nexus-only recovery");edit.Cancel();
 var secondPoint=center+WorldGridGeometry.ToWorld(new Vector3(24,0,0));Check(edit.AddContent(def,secondPoint,Vector3.forward,out reason),reason);Check(edit.Confirm().Success,"Second coverage provider");Check(edit.ToggleGroundRecovery(nexus,out reason)&&edit.Confirm().Success,"Other nexus permits recovery");Check(!building.Disposed,"Resource preserved by alternate nexus");var second=content.GroundWorld.Buildings.Single(b=>b.Module<IBuildArea>()!=null);
 Check(edit.ToggleGroundRecovery(second,out reason)&&edit.ToggleGroundRecovery(building,out reason),reason);edit.Cancel();Check(!second.Disposed&&!building.Disposed,"Combined recovery cancel preserves both");Check(edit.ToggleGroundRecovery(second,out reason)&&edit.ToggleGroundRecovery(building,out reason)&&edit.Confirm().Success,"Combined resource+nexus recovery succeeds");
 Check(edit.AddContent(def,point,Vector3.forward,out reason)&&edit.Confirm().Success,"Reinstall");nexus=content.GroundWorld.Buildings.Single();Check(edit.AddContent(resource,near,Vector3.forward,out reason),reason);nexus.Destroy();Check(!edit.Confirm().Success&&edit.ContentPending.Count==1&&content.GroundWorld.Grid.ReservationCount==1,"Nexus lost before confirm preserves pending resource");edit.Cancel();Check(content.GroundWorld.Grid.ReservationCount==0,"Cancel releases reservation");
 return "PASS: 7 resource profiles, absent/pending nexus, outside/corner boundary, inside commit, dependency-safe recovery, alternate nexus coverage, grouped recovery/cancel, commit revalidation and reservation preservation.";
 }finally{edit.Dispose();placement.Dispose();content.Dispose();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(go);UnityEngine.Object.Destroy(data);}
 }
}
