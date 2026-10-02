using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using EternalSteam.OpenWorld;
using TMPro;
public static class AuthorRailwayStationEntry
{
 public static string Main(){
  if(EditorApplication.isPlaying)throw new Exception("Edit Mode required");var old=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(old.isDirty)throw new Exception("Unsaved scene");var path=old.path;
  foreach(var name in new[]{"StartRegionSandbox","OpenWorldSandbox"}){
   var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");var h=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();var parent=h.Layout.SelectionPanel;var source=h.Buttons.Single(b=>b.Id=="upgrade").View;
   var found=parent.Find("station-railway");var b=found==null?UnityEngine.Object.Instantiate(source,parent):found.GetComponent<UnityEngine.UI.Button>();b.name="station-railway";var rt=(RectTransform)b.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,0);rt.anchoredPosition=new Vector2(12,48);rt.sizeDelta=new Vector2(248,32);b.GetComponentInChildren<TMP_Text>().text="선택한 기차역의 노선 보기";
   while(b.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(b.onClick,0);UnityEventTools.AddStringPersistentListener(b.onClick,h.Execute,"station-railway");
   var bindings=h.Buttons.Where(x=>x.Id!="station-railway").ToList();bindings.Add(new CanvasWorldHud.ButtonBinding{Id="station-railway",View=b});h.Buttons=bindings.ToArray();b.gameObject.SetActive(false);
   EditorUtility.SetDirty(h);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   if(b.onClick.GetPersistentTarget(0)!=h||b.onClick.GetPersistentMethodName(0)!="Execute")throw new Exception("Saved callback missing");
  }
  EditorSceneManager.OpenScene(path);return "PASS station detail entry button saved in both scenes";
 }
}
