using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam.OpenWorld;
using EternalSteam.OpenWorld.Editor;
using EternalSteam.Demo;
public static class VerifySceneAuthoring
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static string Main()
 {
  const string original="Assets/EternalSteam/Scene/Tests/OpenWorldSandbox.unity";
  const string temporary="Assets/EternalSteam/Scene/Tests/AuthoringVerification.unity";
  if(Application.isPlaying)throw new Exception("Edit mode required");
  if(AssetDatabase.LoadAssetAtPath<SceneAsset>(temporary)!=null)throw new Exception("Temporary test path already exists");
  EditorSceneManager.OpenScene(original);
  EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),temporary,true);
  try {
   EditorSceneManager.OpenScene(temporary);var w=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
   Check(w.GetComponentsInChildren<SceneTower>().Length==0&&w.GetComponentsInChildren<SceneFoundation>().Length==0,"Initial Terrain has no supplied layout");
   Check(WorldAuthoring.Foundation(w,Vector3.one,out _),"Foundation from empty Terrain");
   Check(WorldAuthoring.Foundation(w,new Vector3(-1,0,1),out _),"Adjacent foundation");
   for(int i=0;i<4;i++)Check(WorldAuthoring.Tower(w,new Vector3(i*2+1,0,1),Vector3.left,(HordeTowerKind)i,out _),"Place four kinds from empty Terrain");
   EditorSceneManager.SaveScene(w.gameObject.scene);return Run(temporary);
  } finally {EditorSceneManager.OpenScene(original);AssetDatabase.DeleteAsset(temporary);}
 }
 static string Run(string path)
 {
  Check(!Application.isPlaying,"Edit mode required");

  EditorSceneManager.OpenScene(path);
  var w=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
  Check(w.SceneAuthored,"Scene authoring enabled");Check(w.GetComponentsInChildren<SceneFoundation>().Length==2,"Saved foundations visible before Play");
  Check(w.GetComponentsInChildren<SceneTower>().Length==4,"Saved towers visible before Play");
  Check(!w.GetComponent<OpenWorldInput>().enabled,"Runtime construction disabled");
  foreach(var t in w.GetComponentsInChildren<SceneTower>()) {
   Check(PrefabUtility.IsPartOfPrefabInstance(t),"Actual tower prefab instance");Check(t.Head!=null&&t.Tracer!=null&&t.Coverage!=null,"Serialized view references");
   Check(Mathf.Abs(t.transform.position.y-1.95f)<.01f,"Saved foundation elevation");
   foreach(var r in t.GetComponentsInChildren<Renderer>())Check(r.sharedMaterial!=null&&AssetDatabase.Contains(r.sharedMaterial),"Persistent material references");
  }
  Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
  try {
   Check(!WorldAuthoring.Foundation(w,Vector3.one,out _),"Duplicate foundation rejected");
   Check(!WorldAuthoring.Tower(w,new Vector3(30,0,30),Vector3.forward,HordeTowerKind.Arrow,out _),"Tower requires foundation");
   Check(WorldAuthoring.Tower(w,new Vector3(1,0,3),Vector3.right,HordeTowerKind.Arrow,out _),"Editor place");
   Undo.CollapseUndoOperations(group);Undo.PerformUndo();Check(w.GetComponentsInChildren<SceneTower>().Length==4,"Placement Undo");
   Undo.PerformRedo();Check(w.GetComponentsInChildren<SceneTower>().Length==5,"Placement Redo");
   Undo.IncrementCurrentGroup();Check(WorldAuthoring.Recover(w,new Vector3(1,0,3),out _),"Editor recovery");
   Check(w.GetComponentsInChildren<SceneTower>().Length==4,"Editor recovery removes saved object");
   Undo.PerformUndo();Check(w.GetComponentsInChildren<SceneTower>().Length==5,"Recovery Undo");
  } finally {Undo.RevertAllDownToGroup(group);EditorSceneManager.OpenScene(path);}
  return "PASS: saved edit-mode terrain/foundations/four tower prefabs, persistent materials, elevation, no runtime construction, editor placement/recovery, Undo/Redo, scene reload.";
 }
}
