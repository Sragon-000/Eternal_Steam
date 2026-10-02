using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam.OpenWorld;
using TMPro;
public static class AuthorRailRouteMarkers
{
 public static string Main(){
  if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Clean Edit scene required");string original=EditorSceneManager.GetActiveScene().path;
  const string path="Assets/EternalSteam/Content/Buildings/Railway/RouteMarker.prefab";var prefab=AssetDatabase.LoadAssetAtPath<RailRouteMarker>(path);
  if(prefab==null){var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var root=new GameObject("RouteMarker");try{
    var marker=root.AddComponent<RailRouteMarker>();var canvas=new GameObject("WorldNumber",typeof(RectTransform),typeof(Canvas));canvas.transform.SetParent(root.transform,false);canvas.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;((RectTransform)canvas.transform).sizeDelta=new Vector2(40,40);canvas.transform.localScale=Vector3.one*.075f;
    var number=UnityEngine.Object.Instantiate(s.RailwayHud.Summary,canvas.transform);number.name="StationNumber";number.rectTransform.anchorMin=Vector2.zero;number.rectTransform.anchorMax=Vector2.one;number.rectTransform.offsetMin=number.rectTransform.offsetMax=Vector2.zero;number.fontSize=36;number.alignment=TextAlignmentOptions.Center;number.color=Color.yellow;number.text="1";number.raycastTarget=false;marker.Number=number;
    var arrow=new GameObject("DirectionArrow",typeof(LineRenderer));arrow.transform.SetParent(root.transform,false);var line=arrow.GetComponent<LineRenderer>();line.sharedMaterial=s.LineMaterial;line.useWorldSpace=false;line.positionCount=5;line.startWidth=line.endWidth=.14f;line.startColor=line.endColor=Color.yellow;line.SetPositions(new[]{new Vector3(-.65f,0,-.65f),Vector3.zero,new Vector3(.65f,0,-.65f),Vector3.zero,new Vector3(0,0,-1.4f)});marker.Arrow=arrow.transform;
    prefab=PrefabUtility.SaveAsPrefabAsset(root,path).GetComponent<RailRouteMarker>();
   }finally{UnityEngine.Object.DestroyImmediate(root);}}
  foreach(string name in new[]{"StartRegionSandbox","OpenWorldSandbox"}){var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();s.RailwayView.MarkerPrefab=prefab;EditorUtility.SetDirty(s.RailwayView);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);}
  EditorSceneManager.OpenScene(original);AssetDatabase.SaveAssets();return "PASS saved world Canvas number and direction marker prefab, both scenes linked";
 }
}
