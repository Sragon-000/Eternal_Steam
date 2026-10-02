using System;using System.Linq;using System.IO;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using EternalSteam.OpenWorld;using TMPro;using Newtonsoft.Json.Linq;
public static class AuthorResponsiveHudAnchors{
 static void TopStretch(RectTransform r,float left,float right,float top,float height){r.anchorMin=new Vector2(0,1);r.anchorMax=Vector2.one;r.pivot=new Vector2(.5f,1);r.offsetMin=new Vector2(left,-top-height);r.offsetMax=new Vector2(-right,-top);}
 public static string Main(){if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Clean edit mode required");string original=EditorSceneManager.GetActiveScene().path;var report=new JArray();
 try{foreach(var name in new[]{"StartRegionSandbox","OpenWorldSandbox"}){var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");var h=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();RectTransform F(string n)=>h.GetComponentsInChildren<RectTransform>(true).Single(r=>r.name==n);
 var top=F("TopBar");TopStretch(top,320,16,16,60);var time=F("TimeSummary");time.anchorMin=time.anchorMax=time.pivot=new Vector2(0,1);time.anchoredPosition=Vector2.zero;
 var left=F("LeftStatus");left.anchorMin=left.anchorMax=left.pivot=new Vector2(0,1);left.anchoredPosition=new Vector2(16,-16);left.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,288);
 TopStretch(F("HealthPanel"),0,0,0,76);TopStretch(h.Layout.ClockPanel,0,0,84,156);TopStretch(F("clock-fold"),0,0,0,28);TopStretch(h.Layout.ClockBody,8,8,32,124);
 var dial=F("ClockDial");dial.anchorMin=dial.anchorMax=dial.pivot=new Vector2(.5f,1);dial.anchoredPosition=Vector2.zero;
 var right=F("RightStatus");right.anchorMin=right.anchorMax=right.pivot=Vector2.one;right.anchoredPosition=new Vector2(-16,-84);
 TopStretch(h.Layout.MapPanel,0,0,0,292);TopStretch(h.Layout.SelectionPanel,0,0,300,400);
 // Minimap content remains a square with top-left anchoring; layout sizes it to remaining vertical space.
 h.Layout.MapBody.anchorMin=h.Layout.MapBody.anchorMax=h.Layout.MapBody.pivot=new Vector2(0,1);
 var bottom=h.Layout.ConstructionBar;bottom.anchorMin=Vector2.zero;bottom.anchorMax=new Vector2(1,0);bottom.pivot=new Vector2(.5f,0);bottom.offsetMin=new Vector2(16,16);bottom.offsetMax=new Vector2(-16,232);
 var notice=h.Layout.Notifications;notice.anchorMin=Vector2.zero;notice.anchorMax=new Vector2(1,0);notice.pivot=new Vector2(.5f,0);notice.offsetMin=new Vector2(320,240);notice.offsetMax=new Vector2(-304,292);
 h.Layout.FloatingPanels=new[]{F("ProgressAndSave"),F("PowerDetails")};foreach(var popup in h.Layout.FloatingPanels){popup.anchorMin=popup.anchorMax=popup.pivot=Vector2.one;popup.anchoredPosition=new Vector2(-304,-84);}
 foreach(var t in new[]{F("date"),F("remaining"),F("power")}.Select(r=>r.GetComponent<TMP_Text>())){t.enableAutoSizing=true;t.fontSizeMin=14;t.fontSizeMax=t.fontSize;t.overflowMode=TextOverflowModes.Overflow;}
 h.Layout.Apply(true);EditorUtility.SetDirty(h.Layout);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);report.Add(new JObject{{"scene",name},{"top","top stretch"},{"sides","top left / top right"},{"construction","bottom stretch"},{"selection","right column, available height"},{"popups","top right, fit available height"}});
 }}finally{EditorSceneManager.OpenScene(original);}File.WriteAllText("Docs/Measurements/2026-10-02-train-hud-integration/responsive-anchors.json",report.ToString());return report.ToString();}
}
