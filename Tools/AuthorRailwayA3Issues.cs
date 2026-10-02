using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using EternalSteam.OpenWorld;
using TMPro;
public static class AuthorRailwayA3Issues
{
 static UnityEngine.UI.Button Button(RailwayHud h,Transform parent,string command,string label,float x,float y,float width)
 {
  var found=parent.Find(command);var b=found==null?UnityEngine.Object.Instantiate(h.CancelPendingButton,parent):found.GetComponent<UnityEngine.UI.Button>();b.name=command;
  var rt=(RectTransform)b.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,y);rt.sizeDelta=new Vector2(width,36);
  b.GetComponentInChildren<TMP_Text>().text=label;while(b.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(b.onClick,0);UnityEventTools.AddStringPersistentListener(b.onClick,h.Execute,command);return b;
 }
 public static string Main()
 {
  if(EditorApplication.isPlaying)throw new Exception("Edit Mode required");var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(original.isDirty)throw new Exception("Preserve unsaved scene");string path=original.path;
  foreach(var name in new[]{"StartRegionSandbox","OpenWorldSandbox"}){
   var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");var h=UnityEngine.Object.FindFirstObjectByType<RailwayHud>(FindObjectsInactive.Include);
   if(h.DraftStopsPanel==null)throw new Exception("Order editor must exist");
   var panel=(RectTransform)h.DraftStopsPanel.transform;panel.sizeDelta=new Vector2(320,810);
   if(h.IssueHeader==null){h.IssueHeader=UnityEngine.Object.Instantiate(h.Feedback,panel);h.IssueHeader.name="DiagnosticHeader";}
   var title=h.IssueHeader.rectTransform;title.anchorMin=title.anchorMax=title.pivot=new Vector2(0,1);title.anchoredPosition=new Vector2(10,-612);title.sizeDelta=new Vector2(300,30);h.IssueHeader.fontSize=16;h.IssueHeader.raycastTarget=false;h.IssueHeader.text="오류 목록";
   h.IssueRows=new UnityEngine.UI.Button[3];for(int i=0;i<3;i++)h.IssueRows[i]=Button(h,panel,"issue:"+i,"오류 "+(i+1),10,-644-i*38,300);
   Button(h,panel,"issue-prev","이전 오류",10,-762,145);Button(h,panel,"issue-next","다음 오류",165,-762,145);
   var bindings=new List<RailwayHud.CommandBinding>();foreach(var b in h.GetComponentsInChildren<UnityEngine.UI.Button>(true)){
    var calls=new SerializedObject(b).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");for(int i=0;i<calls.arraySize;i++){var call=calls.GetArrayElementAtIndex(i);if(call.FindPropertyRelative("m_Target").objectReferenceValue==h&&call.FindPropertyRelative("m_MethodName").stringValue=="Execute")bindings.Add(new RailwayHud.CommandBinding{Button=b,Command=call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue});}
   }
   h.Commands=bindings.ToArray();h.DraftStopsPanel.SetActive(false);EditorUtility.SetDirty(h);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  }
  EditorSceneManager.OpenScene(path);return "PASS saved diagnostic rows, pagination and callbacks in both scenes";
 }
}
