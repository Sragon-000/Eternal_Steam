using System;
using System.Reflection;
using System.Threading.Tasks;
using EternalSteam;
using EternalSteam.OpenWorld;
using UnityEngine;
using UnityEngine.UIElements;
public static class VerifyHudLayout
{
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    public static async Task<string> Main()
    {
        var hud=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>();Check(hud!=null&&Application.isPlaying,"OpenWorld Play required");
        Check(!hud.Input.IsEditing,"Run in normal state without pending edits");
        var root=hud.GetComponent<UIDocument>().rootVisualElement;
        void Click(string name){var b=root.Q<Button>(name);typeof(Clickable).GetMethod("Invoke",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(b.clickable,new object[]{null});}
        async Task SetFold(string name,bool folded){if(root.Q(name+"-panel").ClassListContains("folded")!=folded)Click(name+"-fold");await Task.Delay(100);}
        bool menuFold=root.Q("menu-panel").ClassListContains("folded"),devFold=root.Q("test-panel").ClassListContains("folded"),inventory=hud.Inventory.Visible;
        var original=hud.Input.SelectedContent??hud.Input.SelectedTower?.building;
        try{
            hud.Input.ClearSelection();hud.Refresh();await Task.Delay(100);
            Check(!OpenWorldHud.IsShown(root.Q("selection-panel")),"No selection means no detail panel");
            Check(!OpenWorldHud.IsShown(root.Q("confirm"))&&!OpenWorldHud.IsShown(root.Q("cancel")),"Explore hides commit/cancel");
            var main=hud.Sandbox.Content.MainBase;Check(main!=null,"Main base required");hud.Input.ClickWorld(main.Position);hud.Refresh();await Task.Delay(100);
            Check(OpenWorldHud.IsShown(root.Q("selection-panel")),"Selected main opens detail");
            Check(root.Q<Label>("content-stats").text.Contains(main.DisplayName),"Actual selected name");
            Check(hud.IsPointerOverHud(root.Q("selection-close").worldBound.center),"Selected panel blocks world input");
            Check(root.Q("selection-panel").worldBound.yMax<=root.Q("panel").worldBound.yMin,"Details stay above catalog");
            Check(root.Q("minimap-panel").worldBound.yMax<=root.Q("selection-panel").worldBound.yMin,"Map and details do not overlap");
            Click("selection-close");Check(hud.Input.SelectedContent==null&&hud.Input.SelectedTower==null,"Close clears selected building");
            await SetFold("menu",false);Check(OpenWorldHud.IsShown(root.Q("save")),"Save reachable in player menu");
            Check(hud.IsPointerOverHud(root.Q("save").worldBound.center),"Menu input blocked");
            await SetFold("test",false);Check(OpenWorldHud.IsShown(root.Q("amount")),"Developer input reachable");
            var field=root.Q<TextField>("amount");using(var e=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0})){e.target=field;field.SendEvent(e);}
            Check(!field.isReadOnly,"Quantity editing armed");await SetFold("test",true);Check(field.isReadOnly,"Folding developer panel releases quantity focus");
            hud.Input.BeginEditing();hud.Refresh();Check(!OpenWorldHud.IsShown(root.Q("selection-panel")),"Editing hides detail");
            Check(OpenWorldHud.IsShown(root.Q("confirm"))&&OpenWorldHud.IsShown(root.Q("cancel")),"Editing exposes commit and cancel");
            hud.Inventory.SetVisible(false);await Task.Delay(100);Check(hud.Input.IsEditing,"Fold keeps edit session");
            Check(hud.IsPointerOverHud(root.Q("cancel").worldBound.center),"Folded catalog retains input-blocking actions");
            Click("cancel");Check(!hud.Input.IsEditing,"Explicit cancel returns to normal");
            return $"PASS HUD panel ownership, selected main/detail bounds, menu input, hidden developer focus release, edit actions and folded-catalog input at {Screen.width}x{Screen.height}.";
        }
        finally{
            hud.Input.Cancel();hud.Inventory.SetVisible(inventory);await SetFold("menu",menuFold);await SetFold("test",devFold);
            if(original!=null&&original.Active&&!original.Disposed)hud.Input.ClickWorld(original.Position);hud.Refresh();
        }
    }
}
