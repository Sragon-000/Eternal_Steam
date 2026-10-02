using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using EternalSteam.OpenWorld;
using TMPro;
public static class AuthorRailwayA3Order
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
   if(h.DraftStopsPanel==null){h.DraftStopsPanel=new GameObject("RailwayDraftStations",typeof(RectTransform),typeof(UnityEngine.UI.Image));h.DraftStopsPanel.transform.SetParent(h.Panel.transform,false);h.DraftStopsPanel.GetComponent<UnityEngine.UI.Image>().color=h.Panel.GetComponent<UnityEngine.UI.Image>().color;}
   var panel=(RectTransform)h.DraftStopsPanel.transform;panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(0,1);panel.anchoredPosition=new Vector2(628,0);panel.sizeDelta=new Vector2(320,450);
   if(h.DraftSelection==null){h.DraftSelection=UnityEngine.Object.Instantiate(h.Feedback,panel);h.DraftSelection.name="SelectionAndLoop";h.DraftSelection.raycastTarget=false;}
   var title=h.DraftSelection.rectTransform;title.anchorMin=title.anchorMax=title.pivot=new Vector2(0,1);title.anchoredPosition=new Vector2(10,-12);title.sizeDelta=new Vector2(300,90);h.DraftSelection.text="역 선택 · 순서 편집";h.DraftSelection.fontSize=16;
   h.DraftRows=new UnityEngine.UI.Button[4];for(int i=0;i<4;i++)h.DraftRows[i]=Button(h,panel,"draft-row:"+i,"역 "+(i+1),10,-108-i*42,300);
   Button(h,panel,"draft-prev","이전",10,-280,145);Button(h,panel,"draft-next","다음",165,-280,145);
   Button(h,panel,"down","선택 역 아래로",10,-322,145);Button(h,panel,"first","선택 역을 첫 역으로",165,-322,145);
   Button(h,panel,"open-loop","순환 연결 해제",10,-364,145);Button(h,panel,"close-loop","마지막 → 첫 역 연결",165,-364,145);
   var bindings=new List<RailwayHud.CommandBinding>();foreach(var b in h.GetComponentsInChildren<UnityEngine.UI.Button>(true)){
    var calls=new SerializedObject(b).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");for(int i=0;i<calls.arraySize;i++){var call=calls.GetArrayElementAtIndex(i);if(call.FindPropertyRelative("m_Target").objectReferenceValue==h&&call.FindPropertyRelative("m_MethodName").stringValue=="Execute")bindings.Add(new RailwayHud.CommandBinding{Button=b,Command=call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue});}
   }
   h.Commands=bindings.ToArray();h.DraftStopsPanel.SetActive(false);EditorUtility.SetDirty(h);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  }
  EditorSceneManager.OpenScene(path);return "PASS saved draft station rows, pagination, down/first/loop controls and callbacks in both scenes";
 }
}
