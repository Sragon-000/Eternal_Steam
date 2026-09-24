using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class ApplyStartLoop
{
 const string Folder="Assets/EternalSteam/Content/Buildings/StartLoop";
 public static object Main(){
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var scene=s.gameObject.scene;
  if(Application.isPlaying||scene.isDirty||scene.name!="StartRegionSandbox"||s.StartingBase!=null)throw new Exception("Clean original scene without fixed main required");
  string backup=AssetDatabase.GenerateUniqueAssetPath("Assets/EternalSteam/Scene/Tests/StartRegionSandbox_BeforeFixedMain.unity");if(!AssetDatabase.CopyAsset(scene.path,backup))throw new Exception("Backup failed");
  var tiles=s.Ground.GetComponent<TileWorldGround>();var root=tiles.GridRoot;var pivot=s.CameraRig.Focus;
  var delta=Quaternion.Euler(0,45-root.eulerAngles.y,0);var oldPosition=root.position;var oldRotation=root.rotation;
  root.SetPositionAndRotation(pivot+delta*(root.position-pivot),delta*root.rotation);Physics.SyncTransforms();
  // The fixed start must be usable after rotating the actual map, not only its presentation.
  var cell=new Vector2Int(0,8);var center=WorldGridGeometry.ToWorld(new Vector3(4,0,20));float high=float.MinValue,low=float.MaxValue;
  for(int z=-4;z<=4;z++)for(int x=-4;x<=4;x++){
   var point=center+WorldGridGeometry.ToWorld(new Vector3(x,0,z));
   if(!tiles.IsPlayable(point)||!tiles.TrySurface(point,out float y)){root.SetPositionAndRotation(oldPosition,oldRotation);throw new Exception("Original starting footprint invalid after rotation at "+point);}
   high=Mathf.Max(high,y);low=Mathf.Min(low,y);
  }
  if(high-low>1.2f){root.SetPositionAndRotation(oldPosition,oldRotation);throw new Exception("Starting footprint slope");}center.y=high+.05f;
  if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/EternalSteam/Content/Buildings","StartLoop");
  var renders=root.GetComponentsInChildren<Renderer>();var bounds=renders[0].bounds;foreach(var r in renders)bounds.Encapsulate(r.bounds);
  float top=bounds.max.y+10,bottom=bounds.min.y-2;bool Sample(Vector3 p,out RaycastHit hit)=>Physics.Raycast(new Vector3(p.x,top,p.z),Vector3.down,out hit,top-bottom+10,tiles.SurfaceMask,QueryTriggerInteraction.Ignore);
  var data=new TerrainData{name="Rotated map height cache",heightmapResolution=513};data.size=new Vector3(bounds.size.x,Mathf.Max(8,bounds.size.y+4),bounds.size.z);var origin=new Vector3(bounds.min.x,bottom,bounds.min.z);var heights=new float[513,513];
  for(int z=0;z<=512;z++)for(int x=0;x<=512;x++){var point=origin+new Vector3(x*data.size.x/512,0,z*data.size.z/512);heights[z,x]=Sample(point,out var hit)?Mathf.Clamp01((hit.point.y-bottom)/data.size.y):0;}
  data.SetHeights(0,0,heights);AssetDatabase.CreateAsset(data,Folder+"/MapHeightCache.asset");s.Ground.terrainData=data;s.Ground.transform.position=origin;var collider=s.Ground.GetComponent<TerrainCollider>();collider.terrainData=data;collider.enabled=false;
  var catalog=UnityEngine.Object.Instantiate(s.ContentCatalog);catalog.Buildings=catalog.Buildings.ToList();int index=catalog.Buildings.FindIndex(d=>OpenWorldContent.RoleOf(d)==BaseRole.Main);
  var main=UnityEngine.Object.Instantiate(catalog.Buildings[index]);main.Modules=main.Modules.ToList();int areaIndex=main.Modules.FindIndex(m=>m is BuildAreaModuleDefinition);var area=UnityEngine.Object.Instantiate((BuildAreaModuleDefinition)main.Modules[areaIndex]);area.Shape=BuildAreaShape.Square;area.Radius=11;area.RadiusPerLevel=0;area.Yaw=45;area.FromFrontEdge=true;area.AlignForwardCells=true;area.CellSize=2;AssetDatabase.CreateAsset(area,Folder+"/MainForwardArea.asset");main.Modules[areaIndex]=area;AssetDatabase.CreateAsset(main,Folder+"/MainBase.asset");catalog.Buildings[index]=main;AssetDatabase.CreateAsset(catalog,Folder+"/Catalog.asset");s.ContentCatalog=catalog;
  var model=(GameObject)PrefabUtility.InstantiatePrefab(main.ViewPrefab,scene);model.name="Main Base · Fixed Start";model.transform.SetParent(s.transform);model.transform.SetPositionAndRotation(center,WorldGridGeometry.Rotation);var start=model.AddComponent<SceneStartingBase>();start.Definition=main;start.Cell=cell;s.StartingBase=start;
  // Spawn settings are a scene-specific copy. Find complete flat squares near the rotated authored points.
  var assault=UnityEngine.Object.Instantiate(s.AssaultSettings);
  Vector3 Find(Vector3 requested,int half){Vector3 best=default;float distance=float.PositiveInfinity;
   for(int z=0;z<tiles.Height;z++)for(int x=0;x<tiles.Width;x++){
    var p=WorldGridGeometry.Center(WorldGridGeometry.Cell(root.TransformPoint(new Vector3(x*tiles.CellSize,0,z*tiles.CellSize)),2),2);bool valid=true;
    for(int dz=-half;dz<=half&&valid;dz++)for(int dx=-half;dx<=half;dx++)if(!tiles.IsPlayable(p+WorldGridGeometry.ToWorld(new Vector3(dx,0,dz)))){valid=false;break;}
    if(!valid||SpawnAreaValidator.Overlaps(p,new Vector2Int(half,half),center,main.Footprint))continue;
    if((p-requested).sqrMagnitude<distance){distance=(p-requested).sqrMagnitude;best=p;}
   }if(float.IsPositiveInfinity(distance))throw new Exception("No spawn footprint");best.y=s.Ground.SampleHeight(best)+origin.y;return best;
  }
  assault.BossPosition=Find(pivot+delta*(assault.BossPosition-pivot),3);
  assault.BossWavePoints=assault.BossWavePoints.Select(p=>Find(pivot+delta*(p-pivot),1)).ToArray();AssetDatabase.CreateAsset(assault,Folder+"/MapAssault.asset");s.AssaultSettings=assault;
  s.CameraRig.Focus=center; s.CameraRig.View.transform.position=center-s.CameraRig.View.transform.forward*110;
  PrefabUtility.RecordPrefabInstancePropertyModifications(root);PrefabUtility.RecordPrefabInstancePropertyModifications(model.transform);
  EditorUtility.SetDirty(s);EditorUtility.SetDirty(tiles);EditorUtility.SetDirty(s.Ground);EditorUtility.SetDirty(s.CameraRig);EditorUtility.SetDirty(start);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  return new{rotation=root.eulerAngles.ToString(),main=center.ToString(),cell=cell.ToString(),backup,boss=assault.BossPosition.ToString(),front="11x11 cells from front edge, aligned to world lattice"};
 }
}
