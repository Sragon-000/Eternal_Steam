using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using EternalSteam.OpenWorld;
using TMPro;
public static class AuthorRailwayA3Map
{
 static void Button(RailwayHud h,string command,string label,float x)
 {
  var parent=h.DraftStopsPanel.transform;var found=parent.Find(command);var b=found==null?UnityEngine.Object.Instantiate(h.CancelPendingButton,parent):found.GetComponent<UnityEngine.UI.Button>();b.name=command;
  var r=(RectTransform)b.transform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-408);r.sizeDelta=new Vector2(96,36);b.GetComponentInChildren<TMP_Text>().text=label;
  while(b.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(b.onClick,0);UnityEventTools.AddStringPersistentListener(b.onClick,h.Execute,command);
 }
 public static string Main()
 {
  if(EditorApplication.isPlaying)throw new Exception("Edit Mode required");var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(original.isDirty)throw new Exception("Preserve unsaved scene");string path=original.path;
  foreach(var name in new[]{"StartRegionSandbox","OpenWorldSandbox"}){
   var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");var h=UnityEngine.Object.FindFirstObjectByType<RailwayHud>(FindObjectsInactive.Include);var view=h.Sandbox.RailwayView;
   if(h.DraftStopsPanel==null||view.MarkerPrefab==null||view.RouteHighlight==null)throw new Exception("Existing authored presentation required");
   ((RectTransform)h.DraftStopsPanel.transform).sizeDelta=new Vector2(320,620);
   Button(h,"map-arrival","지도 진입",10);Button(h,"map-departure","지도 진출",112);Button(h,"map-cancel","선택 취소",214);
   if(h.SelectedSegment==null){h.SelectedSegment=UnityEngine.Object.Instantiate(h.Feedback,h.DraftStopsPanel.transform);h.SelectedSegment.name="SelectedSegment";}
   var r=h.SelectedSegment.rectTransform;r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(10,-454);r.sizeDelta=new Vector2(300,154);h.SelectedSegment.fontSize=16;h.SelectedSegment.raycastTarget=false;h.SelectedSegment.text="역과 구간을 선택하세요.";
   if(view.SelectedLegHighlight==null){view.SelectedLegHighlight=UnityEngine.Object.Instantiate(view.RouteHighlight,view.transform);view.SelectedLegHighlight.name="SelectedRailSegment";}
   view.SelectedLegHighlight.startColor=view.SelectedLegHighlight.endColor=new Color(1,.5f,.1f);view.SelectedLegHighlight.startWidth=view.SelectedLegHighlight.endWidth=.32f;view.SelectedLegHighlight.positionCount=0;view.SelectedLegHighlight.enabled=false;
   if(view.PortFrames.Length!=4){view.PortFrames=new LineRenderer[4];view.PortLabels=new TMP_Text[4];}
   for(int i=0;i<4;i++){
    if(view.PortFrames[i]==null){var frame=UnityEngine.Object.Instantiate(view.RouteHighlight,view.transform);frame.name="RailPort"+(i+1);view.PortFrames[i]=frame;
     var canvas=UnityEngine.Object.Instantiate(view.MarkerPrefab.Number.transform.parent.gameObject,frame.transform);canvas.name="PortLabelCanvas";canvas.transform.localPosition=new Vector3(0,1.1f,0);canvas.transform.localScale=Vector3.one*.04f;((RectTransform)canvas.transform).sizeDelta=new Vector2(80,32);view.PortLabels[i]=canvas.GetComponentInChildren<TMP_Text>();
    }
    var f=view.PortFrames[i];f.useWorldSpace=false;f.transform.localPosition=Vector3.zero;f.transform.localRotation=WorldGridGeometry.Rotation;f.startWidth=f.endWidth=.12f;f.startColor=f.endColor=Color.white;f.positionCount=5;f.SetPositions(new[]{new Vector3(-.8f,0,-.8f),new Vector3(.8f,0,-.8f),new Vector3(.8f,0,.8f),new Vector3(-.8f,0,.8f),new Vector3(-.8f,0,-.8f)});f.enabled=true;
    var label=view.PortLabels[i];label.fontSize=24;label.text=(i+1).ToString();label.raycastTarget=false;label.alignment=TextAlignmentOptions.Center;f.gameObject.SetActive(false);
   }
   var bindings=new List<RailwayHud.CommandBinding>();foreach(var b in h.GetComponentsInChildren<UnityEngine.UI.Button>(true)){
    var calls=new SerializedObject(b).FindProperty("m_OnClick.m_PersistentCalls.m_Calls");for(int i=0;i<calls.arraySize;i++){var call=calls.GetArrayElementAtIndex(i);if(call.FindPropertyRelative("m_Target").objectReferenceValue==h&&call.FindPropertyRelative("m_MethodName").stringValue=="Execute")bindings.Add(new RailwayHud.CommandBinding{Button=b,Command=call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue});}
   }
   h.Commands=bindings.ToArray();EditorUtility.SetDirty(h);EditorUtility.SetDirty(view);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  }
  EditorSceneManager.OpenScene(path);return "PASS saved map port labels/frames, selected segment renderer, selection info and persistent controls in both scenes";
 }
}
