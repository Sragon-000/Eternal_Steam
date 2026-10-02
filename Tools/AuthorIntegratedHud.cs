using System;using System.Linq;using System.IO;using UnityEngine;using UnityEngine.UI;using UnityEditor;using UnityEditor.Events;using UnityEditor.SceneManagement;using TMPro;using EternalSteam.OpenWorld;using Newtonsoft.Json.Linq;
public static class AuthorIntegratedHud
{
 static void Box(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);r.localScale=Vector3.one;}
 static Sprite Sprite(string n)=>AssetDatabase.LoadAllAssetsAtPath("Assets/EternalSteam/Shared/UI/Railway/"+n+".png").OfType<Sprite>().Single();
 public static string Main(){
  if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Clean edit mode required");string original=EditorSceneManager.GetActiveScene().path;var report=new JArray();
  try{foreach(string name in new[]{"StartRegionSandbox","OpenWorldSandbox"}){
   var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");var h=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();var rail=h.Sandbox.RailwayHud;
   RectTransform Find(string n)=>h.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name==n);
   var panelSprite=Sprite("RailwayPanel");var buttonSprite=Sprite("RailwayButton");
   foreach(var b in h.GetComponentsInChildren<Button>(true)){
    if(b.targetGraphic is Image image&&!h.Catalog.Any(c=>c.View==b)){image.sprite=buttonSprite;image.type=Image.Type.Sliced;image.color=Color.white;}
    b.transition=Selectable.Transition.ColorTint;var colors=ColorBlock.defaultColorBlock;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.2f,1.2f,1.08f);colors.pressedColor=new Color(.7f,.85f,.85f);colors.disabledColor=new Color(.48f,.53f,.53f,.8f);b.colors=colors;
   }
   foreach(string n in new[]{"HealthPanel","ClockPanel","ResourcePanel","MinimapPanel","SelectionPanel","TimeSummary","PowerSummary","ConstructionBar","ProgressAndSave","PowerDetails"}){var im=Find(n).GetComponent<Image>();im.sprite=panelSprite;im.type=Image.Type.Sliced;im.color=Color.white;}
   var workspace=h.GetComponent<GameplayHudWorkspace>()??h.gameObject.AddComponent<GameplayHudWorkspace>();workspace.Hud=h;workspace.Railway=rail;
   var dock=rail.transform.Find("RailwayWorkspace") as RectTransform;if(dock==null){dock=new GameObject("RailwayWorkspace",typeof(RectTransform)).GetComponent<RectTransform>();dock.SetParent(rail.transform,false);}Box(dock,320,96,948,810);workspace.RailwayWorkspace=dock;
   rail.Panel.transform.SetParent(dock,false);Box((RectTransform)rail.Panel.transform,0,0,620,570);Box((RectTransform)rail.StationContextPanel.transform,628,0,320,390);
   var open=rail.Commands.Single(c=>c.Command=="open").Button;open.transform.SetParent(h.Layout.ConstructionBar,false);Box((RectTransform)open.transform,168,6,152,36);open.name="WorkspaceRailway";
   while(open.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(open.onClick,0);UnityEventTools.AddPersistentListener(open.onClick,workspace.Railways);workspace.RailwayTab=open;
   var build=h.Buttons.Single(b=>b.Id=="inventory-fold").View;build.name="WorkspaceConstruction";Box((RectTransform)build.transform,8,6,152,36);build.GetComponentInChildren<TMP_Text>().text="건설 메뉴";
   while(build.onClick.GetPersistentEventCount()>0)UnityEventTools.RemovePersistentListener(build.onClick,0);UnityEventTools.AddPersistentListener(build.onClick,workspace.Construction);workspace.ConstructionTab=build;
   // Category tabs belong to the construction catalogue, never to the railway workspace.
   h.Layout.CategoryStrip.SetParent(h.Layout.InventoryBody,false);Box(h.Layout.CategoryStrip,0,0,504,36);
   var scroll=Find("CatalogScroll");scroll.offsetMax=new Vector2(scroll.offsetMax.x,-42);
   h.Layout.ExpandedHeight=216;
   // Relocate existing quick construction callbacks inside railway management.
   var toolbar=Find("RailwayToolbar");toolbar.SetParent(rail.Panel.transform,false);Box(toolbar,0,578,620,88);
   var quick=rail.Commands.Where(c=>c.Command=="station"||c.Command.StartsWith("build:")||c.Command=="rotate").ToArray();
   for(int i=0;i<quick.Length;i++){quick[i].Button.transform.SetParent(toolbar,false);Box((RectTransform)quick[i].Button.transform,(i%3)*208,(i/3)*42,204,36);}
   rail.Feedback.text="역을 선택해 노선을 관리하거나 두 역 사이 선로를 연결하세요.";
   foreach(var t in h.GetComponentsInChildren<TMP_Text>(true)){t.raycastTarget=false;if(t.color.r>.65f&&t.color.g>.65f&&t.color.b>.65f)t.color=new Color(.84f,.9f,.89f);}
   h.ShowDevelopmentControls=false;foreach(var section in h.Sections)if(section.Id is "developer" or "test-shortcuts")section.Body.SetActive(false);
   rail.Panel.SetActive(false);h.Layout.InventoryBody.gameObject.SetActive(true);h.Layout.Apply(true);workspace.Apply();
   EditorUtility.SetDirty(h);EditorUtility.SetDirty(rail);EditorUtility.SetDirty(h.Layout);EditorUtility.SetDirty(workspace);EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");
   report.Add(new JObject{{"scene",name},{"buttons",h.GetComponentsInChildren<Button>(true).Length},{"staticWorkspace",true},{"developmentControls",false}});
  }}finally{EditorSceneManager.OpenScene(original);}File.WriteAllText("Docs/Measurements/2026-10-02-train-hud-integration/hud-authoring.json",report.ToString());return report.ToString();
 }
}
