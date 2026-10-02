using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using EternalSteam.OpenWorld;
using TMPro;
public static class AuthorTestControls
{
 public static string Main()
 {
  if(EditorApplication.isPlaying)throw new Exception("Edit mode required");var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(original.isDirty)throw new Exception("Unsaved scene changes");var path=original.path;
  foreach(var name in new[]{"StartRegionSandbox","OpenWorldSandbox"}){
   var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");var h=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();var root=h.GetComponentInParent<Canvas>().transform;
   var panel=root.Find("TestShortcuts") as RectTransform;if(panel==null){panel=new GameObject("TestShortcuts",typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();panel.SetParent(root,false);panel.GetComponent<UnityEngine.UI.Image>().color=new Color(.06f,.09f,.12f,.95f);}
   panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,1);panel.anchoredPosition=new Vector2(0,-126);panel.sizeDelta=new Vector2(450,44);
   var buttons=h.Buttons.ToList();var texts=h.Texts.ToList();var template=buttons.Single(b=>b.Id=="day").View;
   void Button(string id,string command,string caption,float x,float width){var t=panel.Find(id);var b=t==null?UnityEngine.Object.Instantiate(template,panel):t.GetComponent<UnityEngine.UI.Button>();b.name=id;b.gameObject.SetActive(true);var rt=(RectTransform)b.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-4);rt.sizeDelta=new Vector2(width,36);while(b.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(b.onClick,0);UnityEventTools.AddStringPersistentListener(b.onClick,h.Execute,command);var label=b.GetComponentInChildren<TMP_Text>();label.text=caption;label.fontSize=16;label.raycastTarget=false;buttons.RemoveAll(v=>v.Id==id);buttons.Add(new CanvasWorldHud.ButtonBinding{Id=id,View=b});texts.RemoveAll(v=>v.Id==id+"-caption");texts.Add(new CanvasWorldHud.TextBinding{Id=id+"-caption",View=label});}
   Button("infinite-resources","infinite-resources","자원 무한: OFF",4,218);Button("quick-day","day","낮으로",226,108);Button("quick-night","night","밤으로",338,108);
   var sections=h.Sections.ToList();sections.RemoveAll(v=>v.Id=="test-shortcuts");sections.Add(new CanvasWorldHud.Section{Id="test-shortcuts",Body=panel.gameObject});h.Sections=sections.ToArray();h.Buttons=buttons.ToArray();h.Texts=texts.ToArray();EditorUtility.SetDirty(h);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   foreach(var id in new[]{"infinite-resources","quick-day","quick-night"})if(h.Buttons.Single(b=>b.Id==id).View.onClick.GetPersistentEventCount()!=1)throw new Exception("Missing callback");
  }
  EditorSceneManager.OpenScene(path);return "PASS authored test shortcuts and persistent callbacks in both scenes; original scene restored";
 }
}
