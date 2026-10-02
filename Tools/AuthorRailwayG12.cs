using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using EternalSteam.OpenWorld;
using TMPro;
public static class AuthorRailwayG12
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
   if(h.StationContextPanel==null){h.StationContextPanel=new GameObject("RailwayStationContext",typeof(RectTransform),typeof(UnityEngine.UI.Image));h.StationContextPanel.transform.SetParent(h.Panel.transform,false);h.StationContextPanel.GetComponent<UnityEngine.UI.Image>().color=h.Panel.GetComponent<UnityEngine.UI.Image>().color;}
   var panel=(RectTransform)h.StationContextPanel.transform;panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(0,1);panel.anchoredPosition=new Vector2(690,-40);panel.sizeDelta=new Vector2(320,390);
   if(h.StationContextHeader==null){h.StationContextHeader=UnityEngine.Object.Instantiate(h.DraftSelection,panel);h.StationContextHeader.name="StationContextHeader";h.StationContextHeader.raycastTarget=false;}
   var header=h.StationContextHeader.rectTransform;header.anchorMin=header.anchorMax=header.pivot=new Vector2(0,1);header.anchoredPosition=new Vector2(10,-12);header.sizeDelta=new Vector2(300,68);h.StationContextHeader.text="선택 역";
   h.StationRows=new UnityEngine.UI.Button[4];for(int i=0;i<4;i++)h.StationRows[i]=Button(h,panel,"station-row:"+i,"역 "+(i+1),10,-90-i*42,300);
   Button(h,panel,"station-prev","이전",10,-268,145);Button(h,panel,"station-next","다음",165,-268,145);
   Button(h,panel,"station-context","이 역의 노선 보기",10,-310,300);
   var bindings=new List<RailwayHud.CommandBinding>();foreach(var b in h.GetComponentsInChildren<UnityEngine.UI.Button>(true)){
    var calls=new SerializedObject(b).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");for(int i=0;i<calls.arraySize;i++){var call=calls.GetArrayElementAtIndex(i);if(call.FindPropertyRelative("m_Target").objectReferenceValue==h&&call.FindPropertyRelative("m_MethodName").stringValue=="Execute")bindings.Add(new RailwayHud.CommandBinding{Button=b,Command=call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue});}
   }
   h.Commands=bindings.ToArray();h.StationContextPanel.SetActive(false);EditorUtility.SetDirty(h);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  }
  EditorSceneManager.OpenScene(path);return "PASS station context rows/pagination/back-to-station saved in both scenes";
 }
}
