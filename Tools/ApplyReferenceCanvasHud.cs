using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class ApplyReferenceCanvasHud
{
 const string Art="Assets/EternalSteam/Shared/UI/ReferenceHUD/";
 static CanvasWorldHud hud;
 static Color Cyan=new Color(.05f,.9f,.91f),White=new Color(.88f,.93f,.94f),Muted=new Color(.65f,.77f,.82f),Gold=new Color(1,.82f,.4f);
 static Sprite Sprite(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(Art+name+".png")??throw new Exception("Missing sprite "+name);
 static RectTransform Find(string name)=>hud.GetComponentsInChildren<RectTransform>(true).Single(x=>x.name==name);
 static void Box(RectTransform t,float x,float y,float w,float h){t.anchorMin=t.anchorMax=t.pivot=new Vector2(0,1);t.anchoredPosition=new Vector2(x,-y);t.sizeDelta=new Vector2(w,h);t.localScale=Vector3.one;}
 static void Stretch(RectTransform t,float l,float top,float r,float b){t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.pivot=Vector2.one*.5f;t.offsetMin=new Vector2(l,b);t.offsetMax=new Vector2(-r,-top);}
 static void Right(RectTransform t,float x,float y,float w,float h){Box(t,0,y,w,h);t.anchorMin=t.anchorMax=t.pivot=Vector2.one;t.anchoredPosition=new Vector2(-x,-y);}
 static RectTransform Node(string name,Transform parent){var t=parent.Find(name) as RectTransform;if(t==null){var go=new GameObject(name,typeof(RectTransform));go.layer=5;t=(RectTransform)go.transform;t.SetParent(parent,false);}return t;}
 static UnityEngine.UI.Image Image(RectTransform t,Sprite sprite,Color color,bool hit=false){var image=t.GetComponent<UnityEngine.UI.Image>();if(image==null)image=t.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=sprite;image.color=color;image.raycastTarget=hit;image.type=sprite!=null&&sprite.border.sqrMagnitude>0?UnityEngine.UI.Image.Type.Sliced:UnityEngine.UI.Image.Type.Simple;return image;}
 static UnityEngine.UI.Image Icon(string name,Transform parent,string asset,float x,float y,float w,float h){var t=Node(name,parent);Box(t,x,y,w,h);var image=Image(t,Sprite(asset),Color.white);image.preserveAspect=true;return image;}
 static TMP_Text Text(string name,Transform parent,string value,float x,float y,float w,float h,float size=18){var t=Node(name,parent);Box(t,x,y,w,h);var text=t.GetComponent<TMP_Text>();if(text==null)text=t.gameObject.AddComponent<TextMeshProUGUI>();text.font=hud.Texts[0].View.font;text.text=value;text.fontSize=size;text.color=White;text.raycastTarget=false;text.enableAutoSizing=false;return text;}
 static void Binding(string id,TMP_Text text){hud.Texts=hud.Texts.Where(x=>x.Id!=id).Concat(new[]{new CanvasWorldHud.TextBinding{Id=id,View=text}}).ToArray();}
 static void Skin(RectTransform t,string frame="Panel"){Image(t,Sprite(frame),Color.white,true);}
 public static string Main(string sceneName="StartRegionSandbox")
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play");if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new Exception("Unsaved scene");
  var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+sceneName+".unity");hud=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CanvasWorldHud>(true)).Single();Undo.RegisterFullObjectHierarchyUndo(hud.gameObject,"Apply reference HUD visuals");
  foreach(var b in hud.Buttons){var image=b.View.GetComponent<UnityEngine.UI.Image>();image.sprite=Sprite("Card");image.type=UnityEngine.UI.Image.Type.Sliced;image.color=Color.white;b.View.targetGraphic=image;b.View.transition=UnityEngine.UI.Selectable.Transition.ColorTint;var c=UnityEngine.UI.ColorBlock.defaultColorBlock;c.normalColor=Color.white;c.highlightedColor=new Color(1.2f,1.2f,1.2f,1);c.pressedColor=new Color(.6f,.9f,.95f,1);c.selectedColor=Color.white;c.disabledColor=new Color(.72f,.77f,.8f,.85f);c.colorMultiplier=1;b.View.colors=c;}
  foreach(string name in new[]{"HealthPanel","ClockPanel","ResourcePanel","MinimapPanel","SelectionPanel","TimeSummary","PowerSummary","ConstructionBar","ProgressAndSave","PowerDetails","DeveloperPanel"})Skin(Find(name));
  foreach(var t in hud.Texts)t.View.color=White;
  foreach(var b in hud.Buttons){var label=b.View.GetComponentInChildren<TMP_Text>();if(label!=null){label.alignment=TextAlignmentOptions.MidlineLeft;label.margin=new Vector4(10,0,8,0);}}
  foreach(string id in new[]{"date","remaining","power","main-title","main-health"})Find(id).GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft;
  var health=Find("HealthPanel");Icon("NexusIcon",health,"Nexus",12,18,40,40);Box(Find("main-title"),62,9,160,24);Box(Find("main-health"),62,33,160,23);Box(Find("HealthTrack"),62,62,156,6);hud.HealthFill.color=Cyan;
  Find("ModeSummary").gameObject.SetActive(false);
  var time=Find("TimeSummary");Box(time,0,0,420,60);time.anchorMin=time.anchorMax=new Vector2(.3f,1);time.pivot=new Vector2(.5f,1);time.anchoredPosition=Vector2.zero;Box(Find("date"),16,8,160,40);Find("date").GetComponent<TMP_Text>().color=Gold;Box(Find("remaining"),184,8,224,40);
  var power=Find("PowerSummary");Right(power,330,0,284,60);Icon("EnergyIcon",power,"Energy",10,12,26,32);Box(Find("power"),44,7,220,28);
  var rate=Text("power-rate",power,"0/s",202,35,68,20,16);rate.color=Cyan;Binding("power-rate",rate);
  var track=Node("PowerTrack",power);Box(track,44,40,148,6);Image(track,null,new Color(.12f,.19f,.21f));var fill=Node("PowerFill",track);Stretch(fill,0,0,0,0);hud.PowerFill=Image(fill,null,Cyan);
  Right(Find("pause"),168,8,144,44);var menu=Find("menu-fold");menu.SetParent(Find("TopBar"),false);Right(menu,0,8,156,44);
  var dial=Find("ClockDial");dial.GetComponent<CanvasClockDial>().enabled=false;var face=Node("ClockArtwork",dial);Stretch(face,0,0,0,0);Image(face,Sprite("ClockFace"),Color.white);face.SetAsFirstSibling();Box(dial,56,0,120,120);hud.ClockHand.sizeDelta=new Vector2(2,49);hud.ClockHand.GetComponent<UnityEngine.UI.Image>().color=Gold;
  foreach(string id in new[]{"clock-fold","resource-fold","minimap-fold"}){var i=Find(id).GetComponent<UnityEngine.UI.Image>();i.sprite=null;i.color=new Color(.025f,.06f,.07f,.2f);var t=Find(id+"-caption").GetComponent<TMP_Text>();t.margin=new Vector4(12,0,8,0);t.text=id=="resource-fold"?"보유 자원":t.text;}
  var names=Find("resources");Stretch(names,28,0,72,0);names.GetComponent<TMP_Text>().color=Muted;Stretch(Find("resource-values"),112,0,0,0);Find("resource-values").GetComponent<TMP_Text>().color=Gold;
  var resourceIcons=new List<CanvasWorldHud.ResourceIconBinding>();var ids=new HashSet<string>();foreach(var d in hud.Sandbox.ContentCatalog.Buildings)foreach(var p in d.Modules.OfType<ProductionModuleDefinition>())if(ids.Add(p.OutputId)){string key=p.OutputId.Substring(p.OutputId.LastIndexOf('.')+1);var icon=Icon("ResourceIcon_"+key,names.parent,key,0,resourceIcons.Count*26+2,20,20);resourceIcons.Add(new CanvasWorldHud.ResourceIconBinding{Id=p.OutputId,View=icon.rectTransform});}hud.ResourceIcons=resourceIcons.ToArray();
  // The same saved card controls/callbacks get a preview and a text area.
  hud.CardFrame=Sprite("Card");hud.SelectedFrame=Sprite("Selected");hud.CategoryFrame=Sprite("TabActive");
  for(int i=0;i<hud.Catalog.Length;i++){var entry=hud.Catalog[i];string prefab=entry.Definition!=null?entry.Definition.ViewPrefab.name:entry.Tool==WorldTool.Foundation?"Foundation":"Tower_"+entry.Kind;entry.Icon=Icon("Preview",entry.View.transform,"Building_"+prefab,6,6,72,72);var caption=entry.View.GetComponentInChildren<TMP_Text>();Stretch(caption.rectTransform,84,4,8,4);caption.margin=Vector4.zero;caption.alignment=TextAlignmentOptions.MidlineLeft;caption.textWrappingMode=TextWrappingModes.Normal;hud.Catalog[i]=entry;}
  var inventory=Find("InventoryBody");Stretch(inventory,8,44,8,28);Stretch(Find("CatalogScroll"),0,0,0,0);
  var strip=Node("CategoryStrip",Find("ConstructionBar"));Box(strip,8,4,504,36);
  for(int i=-1;i<4;i++){var tab=Find("category-"+i);tab.SetParent(strip,false);Box(tab,(i+1)*100,0,96,36);Icon("CategoryIcon",tab,i==-1?"All":i==0?"Shield":i==1?"iron":i==2?"Install":"Tools",8,8,20,20);var label=Find("category-"+i+"-caption").GetComponent<TMP_Text>();label.margin=new Vector4(34,0,4,0);}
  Box(Find("inventory-fold"),516,4,112,36);Find("inventory-fold-caption").GetComponent<TMP_Text>().text="목록 접기";Right(Find("edit"),8,4,128,36);Right(Find("EditActions"),144,4,196,36);Right(Find("power-fold"),348,4,128,36);
  foreach(string name in new[]{"edit","confirm","upgrade"})Skin(Find(name),"Selected");
  // Selection portrait and health remain outside the scrollable detail body.
  var selection=Find("SelectionPanel");hud.SelectionPortrait=Icon("SelectionPortrait",selection,"Building_Main",8,12,66,66);Box(Find("selection-title"),82,8,148,70);Right(Find("selection-close"),8,4,30,28);Find("selection-close-caption").GetComponent<TMP_Text>().text="×";
  var hpTrack=Node("SelectionHealthTrack",selection);Box(hpTrack,12,86,248,5);Image(hpTrack,null,new Color(.12f,.19f,.21f));var hpFill=Node("SelectionHealthFill",hpTrack);Stretch(hpFill,0,0,0,0);hud.SelectionHealthFill=Image(hpFill,null,Cyan);
  Stretch(Find("SelectionScroll"),12,102,12,76);var stats=Find("selection-stats");Box(stats,0,0,244,300);stats.GetComponent<TMP_Text>().lineSpacing=10;
  var up=Find("UpgradeControls");up.SetParent(selection,false);up.anchorMin=new Vector2(0,0);up.anchorMax=new Vector2(1,0);up.pivot=new Vector2(.5f,0);up.offsetMin=new Vector2(12,8);up.offsetMax=new Vector2(-12,72);Box(Find("upgrade"),0,0,248,32);Box(Find("upgrade-status"),0,36,248,28);Find("upgrade-status").GetComponent<TMP_Text>().fontSize=16;
  var portraits=new List<CanvasWorldHud.PortraitBinding>();foreach(var path in AssetDatabase.FindAssets("t:BuildingDefinition",new[]{"Assets/EternalSteam/Content"}).Select(AssetDatabase.GUIDToAssetPath)){var d=AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);if(d.ViewPrefab==null)continue;var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Art+"Building_"+d.ViewPrefab.name+".png");if(sprite!=null&&!portraits.Any(x=>x.DefinitionId==d.Id))portraits.Add(new CanvasWorldHud.PortraitBinding{DefinitionId=d.Id,Sprite=sprite});}hud.Portraits=portraits.ToArray();
  var layout=hud.Layout;layout.CategoryStrip=strip;layout.SingleRowCatalog=true;layout.ExpandedHeight=168;
  var catalogScroll=hud.CatalogLayout.GetComponentInParent<UnityEngine.UI.ScrollRect>();catalogScroll.horizontal=true;catalogScroll.vertical=false;var catalogContent=(RectTransform)hud.CatalogLayout.transform;catalogContent.anchorMin=catalogContent.anchorMax=catalogContent.pivot=new Vector2(0,1);catalogContent.anchoredPosition=Vector2.zero;
  Stretch(catalogScroll.viewport,0,0,0,12);
  var bar=Node("CatalogScrollbar",catalogScroll.transform);bar.anchorMin=Vector2.zero;bar.anchorMax=new Vector2(1,0);bar.pivot=new Vector2(.5f,0);bar.offsetMin=new Vector2(0,2);bar.offsetMax=new Vector2(0,8);Image(bar,null,new Color(.13f,.2f,.22f),true);var sliding=Node("SlidingArea",bar);Stretch(sliding,0,0,0,0);var handle=Node("Handle",sliding);Stretch(handle,0,0,0,0);var handleImage=Image(handle,null,new Color(.32f,.57f,.61f),true);var scrollbar=bar.GetComponent<UnityEngine.UI.Scrollbar>();if(scrollbar==null)scrollbar=bar.gameObject.AddComponent<UnityEngine.UI.Scrollbar>();scrollbar.handleRect=handle;scrollbar.targetGraphic=handleImage;scrollbar.direction=UnityEngine.UI.Scrollbar.Direction.LeftToRight;catalogScroll.horizontalScrollbar=scrollbar;catalogScroll.horizontalScrollbarVisibility=UnityEngine.UI.ScrollRect.ScrollbarVisibility.AutoHide;
  var fitter=catalogContent.GetComponent<UnityEngine.UI.ContentSizeFitter>();fitter.horizontalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;fitter.verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;layout.SelectionMaximumHeight=400;layout.CellHeight=84;layout.MinimumCellHeightPixels=58;layout.MinimumCellWidth=228;layout.MinimumCellPixels=200;
  layout.Texts=hud.GetComponentsInChildren<TMP_Text>(true);layout.FontSizes=layout.Texts.Select(t=>t.name=="date"?22f:t.name=="pending"||t.name=="message"||t.name=="placement-hint"||t.name=="power-rate"||t.name=="upgrade-status"?16f:18f).ToArray();
  string materialPath=Art+"NeutralGrid.mat";var gridMaterial=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(gridMaterial==null){gridMaterial=new Material(Shader.Find("EternalSteam/NeutralConstructionGrid"));AssetDatabase.CreateAsset(gridMaterial,materialPath);}gridMaterial.SetColor("_Color",new Color(.63f,.72f,.74f,.24f));EditorUtility.SetDirty(gridMaterial);foreach(var r in hud.Sandbox.WorldGrid.GetComponentsInChildren<MeshRenderer>(true))r.sharedMaterial=gridMaterial;AssetDatabase.SaveAssets();
  Canvas.ForceUpdateCanvases();layout.Apply(true);UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)hud.transform);EditorUtility.SetDirty(hud);EditorUtility.SetDirty(layout);EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");return "Saved reference visuals: "+sceneName+", cards="+hud.Catalog.Length+", resource icons="+hud.ResourceIcons.Length+", portraits="+hud.Portraits.Length;
 }
}
