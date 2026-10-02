using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using EternalSteam.OpenWorld;using Newtonsoft.Json.Linq;
public static class EnableExistingTestHud {
 public static string Main(){
 if(EditorApplication.isPlaying)throw new Exception("Edit mode required");
 for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)throw new Exception("Unsaved scene changes; preserve before proceeding");
 var setup=EditorSceneManager.GetSceneManagerSetup();var output=new JArray();
 try{foreach(var name in new[]{"StartRegionSandbox","OpenWorldSandbox"}){
 var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");var h=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();if(h==null)throw new Exception("HUD missing");
 h.ShowDevelopmentControls=true;var sections=h.Sections.Where(s=>s.Id=="developer"||s.Id=="test-shortcuts").ToArray();if(sections.Length!=2)throw new Exception("Expected existing test sections");foreach(var section in sections)section.Body.SetActive(true);
 EditorUtility.SetDirty(h);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
 output.Add(new JObject{{"scene",name},{"showDevelopmentControls",h.ShowDevelopmentControls},{"sections",new JArray(sections.Select(s=>new JObject{{"id",s.Id},{"activeSelf",s.Body.activeSelf},{"activeInHierarchy",s.Body.activeInHierarchy}}))}});
 }}finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
 File.WriteAllText("Docs/Measurements/2026-10-02-test-ui-enabled/result.json",output.ToString());return output.ToString();
 }
}
