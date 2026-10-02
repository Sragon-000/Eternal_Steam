using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using EternalSteam.OpenWorld;
using TMPro;
public static class AuthorRailwayConnections
{
 static UnityEngine.UI.Button Button(RailwayHud h,Transform parent,string command,string caption,float x,float y,float width)
 {
  var found=parent.Find(command);var b=found==null?UnityEngine.Object.Instantiate(h.CancelPendingButton,parent):found.GetComponent<UnityEngine.UI.Button>();b.name=command;
  var rt=(RectTransform)b.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,y);rt.sizeDelta=new Vector2(width,36);
  b.GetComponentInChildren<TMP_Text>().text=caption;while(b.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(b.onClick,0);UnityEventTools.AddStringPersistentListener(b.onClick,h.Execute,command);return b;
 }
 public static string Main(){
  if(EditorApplication.isPlaying)throw new Exception("Edit Mode required");var old=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(old.isDirty)throw new Exception("Unsaved scene");var path=old.path;
  foreach(var name in new[]{"StartRegionSandbox","OpenWorldSandbox"}){
   var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");var h=UnityEngine.Object.FindFirstObjectByType<RailwayHud>();
   var entry=h.Commands.Single(b=>b.Command=="new").Button;((RectTransform)entry.transform).sizeDelta=new Vector2(90,34);
   Button(h,entry.transform.parent,"connect","역 연결",492,-160,94);
   if(h.ConnectionPanel==null){h.ConnectionPanel=new GameObject("RailwayConnection",typeof(RectTransform));h.ConnectionPanel.transform.SetParent(h.Panel.transform,false);}
   var panel=(RectTransform)h.ConnectionPanel.transform;panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(0,1);panel.anchoredPosition=new Vector2(12,-280);panel.sizeDelta=new Vector2(596,225);
   h.ConnectionFrom=Button(h,panel,"connection-from","출발 역",0,0,586);
   h.ConnectionTo=Button(h,panel,"connection-to","도착 역",0,-44,586);
   h.ConnectionFromPort=Button(h,panel,"connection-from-port","출발 연결구 · 자동",0,-88,286);
   h.ConnectionToPort=Button(h,panel,"connection-to-port","도착 연결구 · 자동",300,-88,286);
   Button(h,panel,"connection-preview","경로 미리보기",0,-136,186);
   Button(h,panel,"connection-confirm","연결 확정",200,-136,186);
   Button(h,panel,"connection-cancel","취소",400,-136,186);
   var bindings=new List<RailwayHud.CommandBinding>();foreach(var b in h.GetComponentsInChildren<UnityEngine.UI.Button>(true)){
    var calls=new SerializedObject(b).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");for(int i=0;i<calls.arraySize;i++){var call=calls.GetArrayElementAtIndex(i);if(call.FindPropertyRelative("m_Target").objectReferenceValue==h&&call.FindPropertyRelative("m_MethodName").stringValue=="Execute")bindings.Add(new RailwayHud.CommandBinding{Button=b,Command=call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue});}
   }
   h.Commands=bindings.ToArray();h.ConnectionPanel.SetActive(false);EditorUtility.SetDirty(h);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  }
  EditorSceneManager.OpenScene(path);return "PASS railway connection helper saved in both scenes";
 }
}
