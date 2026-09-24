using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GiantGrey.TileWorldCreator;
using EternalSteam.OpenWorld;
public static class ApplyImportedMap
{
 public static string Main(){
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  if(Application.isPlaying||scene.isDirty||scene.path!="Assets/EternalSteam/Scene/Tests/StartRegionSandbox.unity")throw new Exception("Clean original StartRegionSandbox edit scene required");
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
  if(s.GetComponentsInChildren<SceneFoundation>().Length>0||s.GetComponentsInChildren<SceneTower>().Length>0)throw new Exception("Relocate authored buildings before map replacement");
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab_Map/MapCreater .prefab");if(prefab==null)throw new Exception("Missing map prefab");
  int layer=LayerMask.NameToLayer("TWC Ground");if(layer<0)throw new Exception("Missing surface layer");
  string backup=AssetDatabase.GenerateUniqueAssetPath("Assets/EternalSteam/Scene/Tests/StartRegionSandbox_BeforeImportedMap.unity");if(!AssetDatabase.CopyAsset(scene.path,backup))throw new Exception("Backup failed");
  var old=s.Ground.GetComponent<TileWorldGround>().GridRoot.gameObject;
  var root=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);root.name="Imported Map · MapCreater";
  var manager=root.GetComponent<TileWorldCreatorManager>();int w=manager.configuration.width,h=manager.configuration.height;float cell=manager.configuration.cellSize;
  foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=layer;
  foreach(var f in root.GetComponentsInChildren<MeshFilter>(true)){if(f.sharedMesh==null||!AssetDatabase.Contains(f.sharedMesh))throw new Exception("Missing persistent mesh "+f.name);var collider=f.GetComponent<MeshCollider>();if(collider==null)collider=f.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=f.sharedMesh;}
  old.SetActive(false);Physics.SyncTransforms();
  var renders=root.GetComponentsInChildren<Renderer>();var bounds=renders[0].bounds;foreach(var r in renders)bounds.Encapsulate(r.bounds);
  float top=bounds.max.y+10,bottom=bounds.min.y-2,depth=top-bottom+10;
  bool Sample(Vector3 p,out RaycastHit hit)=>Physics.Raycast(new Vector3(p.x,top,p.z),Vector3.down,out hit,depth,1<<layer,QueryTriggerInteraction.Ignore);
  var playable=new byte[w*h];var heightsCell=new float[w*h];
  for(int z=0;z<h;z++)for(int x=0;x<w;x++){var p=root.transform.TransformPoint(new Vector3(x*cell,0,z*cell));if(Sample(p,out var hit)&&hit.normal.y>.95f){playable[z*w+x]=1;heightsCell[z*w+x]=hit.point.y;}}
  var initial=(byte[])playable.Clone();
  for(int z=0;z<h;z++)for(int x=0;x<w;x++)if(initial[z*w+x]!=0){foreach(var d in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down}){int xx=x+d.x,zz=z+d.y;if(xx<0||zz<0||xx>=w||zz>=h||initial[zz*w+xx]==0||Mathf.Abs(heightsCell[zz*w+xx]-heightsCell[z*w+x])>.6f){playable[z*w+x]=0;break;}}}
  string folder="Assets/EternalSteam/Content/Environments/ImportedMap";if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/EternalSteam/Content/Environments","ImportedMap");
  var terrain=s.Ground;var data=new TerrainData{name="Imported map height cache",heightmapResolution=513};data.size=new Vector3(bounds.size.x,Mathf.Max(8,bounds.size.y+4),bounds.size.z);
  var origin=new Vector3(bounds.min.x,bottom,bounds.min.z);var heights=new float[513,513];
  for(int z=0;z<=512;z++)for(int x=0;x<=512;x++){var p=origin+new Vector3(x*data.size.x/512,0,z*data.size.z/512);heights[z,x]=Sample(p,out var hit)?Mathf.Clamp01((hit.point.y-bottom)/data.size.y):0;}
  data.SetHeights(0,0,heights);AssetDatabase.CreateAsset(data,AssetDatabase.GenerateUniqueAssetPath(folder+"/HeightCache.asset"));terrain.terrainData=data;terrain.transform.position=origin;terrain.drawHeightmap=false;terrain.drawTreesAndFoliage=false;
  var tc=terrain.GetComponent<TerrainCollider>();tc.enabled=false;tc.terrainData=data;
  var bridge=terrain.GetComponent<TileWorldGround>();bridge.GridRoot=root.transform;bridge.Width=w;bridge.Height=h;bridge.CellSize=cell;bridge.SurfaceMask=1<<layer;bridge.PlayableCells=playable;
  Vector3 focus=bounds.center;bool found=false;
  for(int z=4;z<h-4&&!found;z++)for(int x=4;x<w-4&&!found;x++){var p=root.transform.TransformPoint(new Vector3(x*cell,0,z*cell));if(bridge.CheckFoundation(p,out var center,out _)){focus=center;found=true;}}
  if(!found)throw new Exception("Map has no valid foundation patch; backup retained");
  s.CameraRig.Focus=focus;s.CameraRig.Zoom=Mathf.Clamp(Mathf.Max(bounds.size.x,bounds.size.z)*.4f,10,70);s.CameraRig.View.orthographicSize=s.CameraRig.Zoom;s.CameraRig.View.transform.rotation=Quaternion.Euler(60,0,0);s.CameraRig.View.transform.position=focus-s.CameraRig.View.transform.forward*110;
  PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
  EditorUtility.SetDirty(terrain);EditorUtility.SetDirty(bridge);EditorUtility.SetDirty(s.CameraRig);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Selection.activeGameObject=root;SceneView.lastActiveSceneView?.Frame(bounds,false);
  return $"Applied prefab: {w}x{h}, {cell}m cells, {renders.Length} renderers, {playable.Count(v=>v!=0)} playable cells, foundation patch {focus}. Backup: {backup}. Old map disabled; prefab source unchanged.";
 }
}
