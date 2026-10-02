using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using EternalSteam.OpenWorld;
using TMPro;
public static class AuthorRailwayA2
{
 static UnityEngine.UI.Button Add(RailwayHud h,Transform parent,string cmd,string label,float x)
 {
  var b=UnityEngine.Object.Instantiate(h.CancelPendingButton,parent);b.name=cmd;var r=(RectTransform)b.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,0);r.sizeDelta=new Vector2(190,36);b.GetComponentInChildren<TMP_Text>().text=label;
  while(b.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(b.onClick,0);UnityEventTools.AddStringPersistentListener(b.onClick,h.Execute,cmd);return b;
 }
 static GameObject Panel(RailwayHud h,string name)
 {
  var existing=h.Panel.transform.Find(name);if(existing!=null)return existing.gameObject;
  var go=new GameObject(name,typeof(RectTransform));var r=(RectTransform)go.transform;r.SetParent(h.Panel.transform,false);var src=(RectTransform)h.DraftPanel.transform;r.anchorMin=src.anchorMin;r.anchorMax=src.anchorMax;r.pivot=src.pivot;r.anchoredPosition=src.anchoredPosition;r.sizeDelta=src.sizeDelta;return go;
 }
 public static string Main()
 {
  if(EditorApplication.isPlaying)throw new Exception("EditMode required");var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(original.isDirty)throw new Exception("Unsaved scene must be preserved");string path=original.path;
  foreach(string name in new[]{"StartRegionSandbox","OpenWorldSandbox"}){
   var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");var h=UnityEngine.Object.FindFirstObjectByType<RailwayHud>(FindObjectsInactive.Include);if(h==null||h.DraftPanel==null)throw new Exception("Missing authored HUD");
   h.ConfirmPanel=Panel(h,"RailwayConfirm");h.DiscardPanel=Panel(h,"RailwayDiscard");
   var commit=h.Panel.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.name=="draft-commit");commit.transform.SetParent(h.ConfirmPanel.transform,false);var cr=(RectTransform)commit.transform;cr.anchorMin=cr.anchorMax=cr.pivot=new Vector2(0,1);cr.anchoredPosition=Vector2.zero;cr.sizeDelta=new Vector2(190,36);commit.GetComponentInChildren<TMP_Text>().text="노선 확정";
   if(h.ConfirmPanel.transform.Find("back-edit")==null){Add(h,h.ConfirmPanel.transform,"back-edit","편집으로 돌아가기",198);Add(h,h.ConfirmPanel.transform,"cancel","취소",396);}
   if(h.DiscardPanel.transform.Find("keep")==null){Add(h,h.DiscardPanel.transform,"keep","계속 편집",0);Add(h,h.DiscardPanel.transform,"discard","변경 폐기",198);}
   if(h.CommandHints==null){h.CommandHints=UnityEngine.Object.Instantiate(h.Feedback,h.Panel.transform);h.CommandHints.name="RailwayCommandHints";var rt=h.CommandHints.rectTransform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,0);rt.anchoredPosition=new Vector2(14,56);rt.sizeDelta=new Vector2(596,46);h.CommandHints.fontSize=13;h.CommandHints.raycastTarget=false;h.CommandHints.text="";}
   var bindings=new List<RailwayHud.CommandBinding>();
   foreach(var button in h.GetComponentsInChildren<UnityEngine.UI.Button>(true)){
    var so=new SerializedObject(button);var calls=so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");for(int i=0;i<calls.arraySize;i++){var call=calls.GetArrayElementAtIndex(i);if(call.FindPropertyRelative("m_Target").objectReferenceValue!=h||call.FindPropertyRelative("m_MethodName").stringValue!="Execute")continue;bindings.Add(new RailwayHud.CommandBinding{Button=button,Command=call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue});}
   }
   h.Commands=bindings.ToArray();h.ConfirmPanel.SetActive(false);h.DiscardPanel.SetActive(false);EditorUtility.SetDirty(h);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  }
  EditorSceneManager.OpenScene(path);return "PASS saved confirm/discard panels, persistent callbacks and command bindings in both scenes";
 }
}
