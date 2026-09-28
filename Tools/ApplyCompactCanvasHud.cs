using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using EternalSteam.OpenWorld;
public static class ApplyCompactCanvasHud
{
    static CanvasWorldHud hud;
    static RectTransform Find(string name){return hud.GetComponentsInChildren<RectTransform>(true).Single(x=>x.name==name);}
    static void Box(RectTransform t,float x,float y,float w,float h){t.anchorMin=t.anchorMax=t.pivot=new Vector2(0,1);t.anchoredPosition=new Vector2(x,-y);t.sizeDelta=new Vector2(w,h);t.localScale=Vector3.one;}
    static void Stretch(RectTransform t,float left,float top,float right,float bottom){t.anchorMin=Vector2.zero;t.anchorMax=Vector2.one;t.pivot=new Vector2(.5f,.5f);t.offsetMin=new Vector2(left,bottom);t.offsetMax=new Vector2(-right,-top);}
    static void Bottom(RectTransform t,float left,float right,float bottom,float h){t.anchorMin=new Vector2(0,0);t.anchorMax=new Vector2(1,0);t.pivot=new Vector2(.5f,0);t.offsetMin=new Vector2(left,bottom);t.offsetMax=new Vector2(-right,bottom+h);}
    static void Right(RectTransform t,float right,float top,float w,float h){t.anchorMin=t.anchorMax=t.pivot=new Vector2(1,1);t.anchoredPosition=new Vector2(-right,-top);t.sizeDelta=new Vector2(w,h);}
    static void ClearBackground(RectTransform t){var i=t.GetComponent<UnityEngine.UI.Image>();if(i!=null){i.color=Color.clear;i.raycastTarget=false;}}
    static RectTransform Panel(string name,RectTransform parent){var t=parent.Find(name) as RectTransform;if(t==null){var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.layer=5;t=(RectTransform)go.transform;t.SetParent(parent,false);}var image=t.GetComponent<UnityEngine.UI.Image>();if(image==null)image=t.gameObject.AddComponent<UnityEngine.UI.Image>();image.color=new Color(.035f,.065f,.08f,.91f);image.raycastTarget=true;return t;}
    static void Move(string name,RectTransform parent,float x,float y,float w,float h){var t=Find(name);t.SetParent(parent,false);Box(t,x,y,w,h);}
    public static string Main(string sceneName="StartRegionSandbox")
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before authoring");
        var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(active.isDirty)throw new Exception("Save pending scene changes before authoring");
        var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+sceneName+".unity");
        hud=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<CanvasWorldHud>(true)).Single();
        Undo.RegisterFullObjectHierarchyUndo(hud.gameObject,"Compact Canvas HUD");
        var scaler=hud.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var root=(RectTransform)hud.transform;root.localScale=Vector3.one;
        var top=Find("TopBar");top.anchorMin=new Vector2(0,1);top.anchorMax=Vector2.one;top.pivot=new Vector2(.5f,1);top.offsetMin=new Vector2(264,-76);top.offsetMax=new Vector2(-16,-16);ClearBackground(top);
        var time=Panel("TimeSummary",top);Box(time,0,0,344,60);Move("date",time,8,5,132,48);Move("remaining",time,144,5,192,48);
        var status=Panel("ModeSummary",top);Box(status,352,0,204,60);Move("mode",status,8,5,188,48);
        var power=Panel("PowerSummary",top);Right(power,112,0,276,60);Move("power",power,8,4,260,52);
        Right(Find("pause"),0,8,104,44);
        var left=Find("LeftStatus");Box(left,16,16,232,640);ClearBackground(left);
        var health=Panel("HealthPanel",left);Box(health,0,0,232,76);Move("main-title",health,8,4,216,26);Move("main-health",health,8,30,216,26);Move("HealthTrack",health,8,62,216,6);
        var clock=Panel("ClockPanel",left);Box(clock,0,84,232,156);Move("clock-fold",clock,0,0,232,28);Move("ClockBody",clock,0,32,232,124);Move("clock-face",Find("ClockBody"),8,0,216,22);Move("ClockDial",Find("ClockBody"),60,12,112,112);Find("clock-face").gameObject.SetActive(false);Find("ClockHand").sizeDelta=new Vector2(3,48);
        var resource=Panel("ResourcePanel",left);Box(resource,0,248,208,280);Move("resource-fold",resource,0,0,208,28);Move("ResourceBody",resource,8,36,192,244);
        Stretch(Find("ResourceScroll"),0,0,0,36);Bottom(Find("resources-all"),0,0,0,28);
        var names=Find("resources");Stretch(names,0,0,80,0);var nameText=names.GetComponent<TMP_Text>();nameText.richText=true;nameText.textWrappingMode=TextWrappingModes.NoWrap;
        var values=names.parent.Find("resource-values") as RectTransform;if(values==null){var go=UnityEngine.Object.Instantiate(names.gameObject,names.parent);go.name="resource-values";values=(RectTransform)go.transform;hud.Texts=hud.Texts.Concat(new[]{new CanvasWorldHud.TextBinding{Id="resource-values",View=values.GetComponent<TMP_Text>()}}).ToArray();}
        Stretch(values,100,0,0,0);values.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.TopRight;values.GetComponent<TMP_Text>().richText=true;
        var right=Find("RightStatus");Right(right,16,84,272,700);ClearBackground(right);
        var map=Panel("MinimapPanel",right);Box(map,0,0,272,292);Move("minimap-fold",map,0,0,272,28);Move("MinimapBody",map,12,36,248,248);Stretch(Find("WorldMap"),0,0,0,0);
        var selection=Find("SelectionPanel");Box(selection,0,300,272,240);var background=selection.GetComponent<UnityEngine.UI.Image>();if(background==null)background=selection.gameObject.AddComponent<UnityEngine.UI.Image>();background.color=new Color(.035f,.065f,.08f,.91f);background.raycastTarget=true;
        Box(Find("selection-title"),8,4,194,60);Right(Find("selection-close"),8,4,58,30);Stretch(Find("SelectionScroll"),8,72,8,8);
        Box(Find("selection-stats"),0,0,244,320);Box(Find("UpgradeControls"),0,324,244,166);Box(Find("upgrade"),0,0,244,36);Box(Find("upgrade-status"),0,40,244,120);
        var bottom=Find("ConstructionBar");Bottom(bottom,16,16,16,204);
        Box(Find("edit"),8,4,132,36);Box(Find("EditActions"),148,4,196,36);Box(Find("confirm"),0,0,94,36);Box(Find("cancel"),102,0,94,36);Box(Find("inventory-fold"),352,4,140,36);Right(Find("power-fold"),160,4,128,36);Right(Find("menu-fold"),8,4,144,36);
        var inventory=Find("InventoryBody");Stretch(inventory,8,48,8,28);
        for(int i=0;i<5;i++)Box(Find("category-"+(i-1)),i*98,0,92,30);
        Stretch(Find("CatalogScroll"),0,36,0,0);Bottom(Find("pending"),8,8,0,24);
        foreach(var entry in hud.Catalog){var image=entry.View.GetComponent<UnityEngine.UI.Image>();image.color=new Color(.12f,.19f,.23f,1);var text=entry.View.GetComponentInChildren<TMP_Text>();text.margin=new Vector4(8,4,8,4);text.alignment=TextAlignmentOptions.MidlineLeft;}
        var notice=Find("Notifications");Bottom(notice,264,304,228,52);Stretch(Find("message"),0,0,0,26);Stretch(Find("placement-hint"),0,26,0,0);
        var layout=hud.GetComponent<CanvasHudLayout>();if(layout==null)layout=hud.gameObject.AddComponent<CanvasHudLayout>();hud.Layout=layout;
        layout.Canvas=hud.GetComponent<Canvas>();layout.Root=root;layout.ClockPanel=clock;layout.ClockBody=Find("ClockBody");layout.ResourcePanel=resource;layout.ResourceBody=Find("ResourceBody");layout.ResourceScroll=Find("ResourceScroll");layout.MapPanel=map;layout.MapBody=Find("MinimapBody");layout.SelectionPanel=selection;layout.SelectionScroll=Find("SelectionScroll");layout.ConstructionBar=bottom;layout.InventoryBody=inventory;layout.StatusRow=Find("pending");layout.Notifications=notice;layout.Catalog=hud.CatalogLayout;
        layout.Texts=hud.GetComponentsInChildren<TMP_Text>(true);layout.FontSizes=new float[layout.Texts.Length];
        for(int i=0;i<layout.Texts.Length;i++){var t=layout.Texts[i];float size=t.name=="date"||t.name=="main-title"?22:t.name=="pending"||t.name=="message"||t.name=="placement-hint"||t.name=="menu-fold-caption"||t.name=="power-fold-caption"?16:18;layout.FontSizes[i]=size;t.fontSize=size;t.enableAutoSizing=false;t.raycastTarget=false;}
        foreach(var b in hud.Buttons){if(b.View.onClick.GetPersistentEventCount()!=1||b.View.onClick.GetPersistentTarget(0)!=hud)throw new Exception("Invalid callback: "+b.Id);}
        Canvas.ForceUpdateCanvases();layout.Apply(true);UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        EditorUtility.SetDirty(hud);EditorUtility.SetDirty(layout);EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed");
        return "Saved compact hierarchy: "+sceneName+", catalog="+hud.Catalog.Length+", buttons="+hud.Buttons.Length;
    }
}
