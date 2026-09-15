using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UIElements;
using EternalSteam.Demo;
using EternalSteam.OpenWorld;
using EternalSteam.OpenWorld.Editor;
public static class SetupOpenWorldAuthoring
{
 const string Folder="Assets/EternalSteam/Content/Buildings/OpenWorldAuthoring";
 static Material Persist(Material source,string name,Color color)
 {
  var path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
  if(m==null){m=new Material(source);AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);EditorUtility.SetDirty(m);return m;
 }
 public static string Main()
 {
  if(Application.isPlaying)throw new Exception("Edit mode required");
  EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/OpenWorldSandbox.unity");
  var w=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
  if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/EternalSteam/Content/Buildings","OpenWorldAuthoring");
  var stage=new GameObject("Authoring asset staging");
  try {
   var slab=GameObject.CreatePrimitive(PrimitiveType.Cube);slab.transform.SetParent(stage.transform);slab.name="Foundation";
   slab.transform.localScale=new Vector3(8,.4f,8);slab.GetComponent<Renderer>().sharedMaterial=w.FoundationMaterial;slab.AddComponent<SceneFoundation>();
   for(int i=0;i<=4;i++) {
    var a=HordeVisualPrimitives.MakeLine("Grid X",slab.transform,w.ValidMaterial,.08f,2);a.useWorldSpace=false;
    a.SetPosition(0,new Vector3(i*.25f-.5f,.5375f,-.5f));a.SetPosition(1,new Vector3(i*.25f-.5f,.5375f,.5f));
    var b=HordeVisualPrimitives.MakeLine("Grid Z",slab.transform,w.ValidMaterial,.08f,2);b.useWorldSpace=false;
    b.SetPosition(0,new Vector3(-.5f,.5375f,i*.25f-.5f));b.SetPosition(1,new Vector3(.5f,.5375f,i*.25f-.5f));
   }
   w.FoundationPrefab=PrefabUtility.SaveAsPrefabAsset(slab,Folder+"/Foundation.prefab");
   var types=new Material[4];var effects=new Material[4];for(int i=0;i<4;i++){types[i]=Persist(w.TowerMaterial,"Tower_"+i,HordeTowerStats.Color((HordeTowerKind)i));effects[i]=Persist(w.LineMaterial,"Effect_"+i,HordeTowerStats.Color((HordeTowerKind)i));}
   w.TowerPrefabs=new GameObject[4];
   using(var factory=new HordeTowerFactory(stage.transform,new List<HordeTower>(),()=>HordeMapKind.Lane,w.BarrelMaterial,w.LineMaterial,w.ValidMaterial,types,effects))
   for(int i=0;i<4;i++) {
    var t=factory.CreateAuthoredView(new Vector3(0,-.01f,0),Vector3.forward,22,(HordeTowerKind)i);
    t.root.AddComponent<SceneTower>().Configure(t);w.TowerPrefabs[i]=PrefabUtility.SaveAsPrefabAsset(t.root,Folder+"/Tower_"+(HordeTowerKind)i+".prefab");
   }
  } finally {UnityEngine.Object.DestroyImmediate(stage);}
  w.SceneAuthored=true;w.GetComponent<OpenWorldInput>().enabled=true;
  var document=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>().GetComponent<UIDocument>();document.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/EternalSteam/Shared/UI/OpenWorld/OpenWorldHud.uxml");
  EditorUtility.SetDirty(w);EditorUtility.SetDirty(document);EditorSceneManager.MarkSceneDirty(w.gameObject.scene);
  EditorSceneManager.SaveScene(w.gameObject.scene);AssetDatabase.SaveAssets();
  return "Saved authoring prefab references without inserting a starter layout. In-game placement enabled.";
 }
}
