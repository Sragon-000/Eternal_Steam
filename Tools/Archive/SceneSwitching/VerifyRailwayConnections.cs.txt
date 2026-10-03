using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam.OpenWorld;
using Newtonsoft.Json.Linq;
public static class VerifyRailwayConnections
{
 public static string Main(){
  if(EditorApplication.isPlaying)throw new Exception("Edit Mode required");var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(current.isDirty)throw new Exception("Unsaved scene");var path=current.path;var result=new JArray();
  try{foreach(var name in new[]{"StartRegionSandbox","OpenWorldSandbox"}){
   var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");var h=UnityEngine.Object.FindFirstObjectByType<RailwayHud>();
   if(h.ConnectionPanel==null||h.ConnectionFrom==null||h.ConnectionTo==null||h.ConnectionFromPort==null||h.ConnectionToPort==null)throw new Exception(name+" missing saved refs");
   foreach(var command in new[]{"connect","connection-from","connection-to","connection-from-port","connection-to-port","connection-preview","connection-confirm","connection-cancel"}){
    var binding=h.Commands.Single(v=>v.Command==command);var so=new SerializedObject(binding.Button);var calls=so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
    if(calls.arraySize!=1)throw new Exception(command+" callback count");var call=calls.GetArrayElementAtIndex(0);
    if(call.FindPropertyRelative("m_Target").objectReferenceValue!=h||call.FindPropertyRelative("m_MethodName").stringValue!="Execute"||call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue!=command)throw new Exception(command+" callback mismatch");
   }
   foreach(var binding in h.Commands){var image=binding.Button.GetComponent<UnityEngine.UI.Image>();if(AssetDatabase.GetAssetPath(image.sprite)!="Assets/EternalSteam/Shared/UI/Railway/RailwayButton.png"||image.type!=UnityEngine.UI.Image.Type.Sliced)throw new Exception("Placeholder button "+binding.Command);}
   if(AssetDatabase.GetAssetPath(h.Panel.GetComponent<UnityEngine.UI.Image>().sprite)!="Assets/EternalSteam/Shared/UI/Railway/RailwayPanel.png")throw new Exception("Placeholder panel");
   var hud=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();if(hud.ShowDevelopmentControls||hud.Sections.Any(s=>(s.Id=="developer"||s.Id=="test-shortcuts")&&s.Body.activeSelf))throw new Exception("Test UI visible");
   result.Add(new JObject{{"scene",name},{"savedRefs",true},{"customSpriteButtons",h.Commands.Length},{"testUiHidden",true},{"persistentCommands",8},{"canvas",h.ConnectionPanel.GetComponentInParent<Canvas>().name},{"initiallyHidden",!h.ConnectionPanel.activeSelf}});
  }}finally{EditorSceneManager.OpenScene(path);}
  File.WriteAllText("Docs/Measurements/2026-10-02-railway-connections/saved-ui.json",result.ToString());return result.ToString();
 }
}
