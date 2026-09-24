using System;
using System.Linq;
using System.Reflection;
using EternalSteam.OpenWorld;
using UnityEngine;
using UnityEngine.UIElements;
public static class VerifyMinimap
{
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    public static string Main()
    {
        var hud=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>();var s=hud.Sandbox;s.Clock.Paused=true;s.Persistence.Automatic=false;
        var root=hud.GetComponent<UIDocument>().rootVisualElement;var map=root.Q<MinimapElement>("minimap");Check(map!=null&&hud.Minimap!=null,"Authored element and adapter");
        var projection=hud.Minimap.Projection;var original=s.CameraRig.Focus;float zoom=s.CameraRig.Zoom;
        for(int i=0;i<4;i++){var point=projection.ToWorld(new Vector2(i%2,i/2));var back=projection.ToMap(point);Check((back-new Vector2(i%2,i/2)).sqrMagnitude<.000001f,"Corner roundtrip");}
        Check(projection.ToMap(projection.ToWorld(new Vector2(.5f,0))).y==0,"North is up");
        hud.Minimap.Refresh(10000);Check(map.Buildings.Count==s.Content.Bases.Buildings.Count&&map.Buildings.Any(m=>m.Base),"Registered main marker");Check(map.HasViewport,"Camera footprint");
        int buildings=s.Content.GroundWorld.Buildings.Count;s.GetComponent<OpenWorldInput>().BeginEditing();
        var target=new Vector2(.4f,.6f);hud.Minimap.Navigate(target);var expected=projection.ToWorld(target);var origin=s.Ground.transform.position;var size=s.Ground.terrainData.size;
        expected.x=Mathf.Clamp(expected.x,origin.x+8,origin.x+size.x-8);expected.z=Mathf.Clamp(expected.z,origin.z+8,origin.z+size.z-8);
        Check(Mathf.Abs(s.CameraRig.Focus.x-expected.x)<.001f&&Mathf.Abs(s.CameraRig.Focus.z-expected.z)<.001f,"Navigation projection");Check(s.CameraRig.Zoom==zoom,"Navigation preserves zoom");
        var click=new Event{type=EventType.MouseDown,button=0,mousePosition=map.worldBound.center};using(var evt=PointerDownEvent.GetPooled(click)){evt.target=map;map.SendEvent(evt);}Check(map.Interacting,"Pointer captured on map");
        click.type=EventType.MouseUp;using(var evt=PointerUpEvent.GetPooled(click)){evt.target=map;map.SendEvent(evt);}Check(!map.Interacting,"Pointer released");
        Check(s.GetComponent<OpenWorldInput>().IsEditing&&s.GetComponent<OpenWorldInput>().Edits.Count==0&&buildings==s.Content.GroundWorld.Buildings.Count,"Navigation does not edit buildings");
        var fold=root.Q<Button>("minimap-fold");var invoke=typeof(Clickable).GetMethod("Invoke",BindingFlags.Instance|BindingFlags.NonPublic);invoke.Invoke(fold.clickable,new object[]{null});Check(root.Q("minimap-body").ClassListContains("minimap-hidden"),"Fold");invoke.Invoke(fold.clickable,new object[]{null});Check(!root.Q("minimap-body").ClassListContains("minimap-hidden"),"Unfold");
        s.GetComponent<OpenWorldInput>().Cancel();s.CameraRig.MoveFocus(original);
        for(int i=0;i<100;i++)s.Enemies.TrySpawn(original,original,0,10);hud.Minimap.Refresh(10001);Check(map.Density.Sum()==s.Enemies.Alive,"All live enemies represented in bounded density cells");s.Enemies.Reset();hud.Minimap.Refresh(10002);Check(map.Density.Sum()==0,"Dead/reset enemies removed");
        return "PASS north-up/corner mapping, actual 45-degree map, main marker, camera footprint, zoom-preserving navigation, pointer capture/release, editing isolation, fold and enemy density/reset.";
    }

    public static async System.Threading.Tasks.Task<string> Input()
    {
        var hud=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>();var s=hud.Sandbox;s.Clock.Paused=true;s.Persistence.Automatic=false;
        var root=hud.GetComponent<UIDocument>().rootVisualElement;var map=root.Q<MinimapElement>("minimap");var original=s.CameraRig.Focus;
        var oldMouse=UnityEngine.InputSystem.Mouse.current;var mouse=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
        var oldSettings=UnityEngine.InputSystem.InputSystem.settings;var settings=UnityEngine.Object.Instantiate(oldSettings);UnityEngine.InputSystem.InputSystem.settings=settings;
        settings.backgroundBehavior=UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Vector2 ScreenPoint(Vector2 p)=>new(p.x/root.panel.visualTree.worldBound.width*Screen.width,Screen.height-p.y/root.panel.visualTree.worldBound.height*Screen.height);
        async System.Threading.Tasks.Task Send(Vector2 p,bool pressed,float scroll=0){var state=new UnityEngine.InputSystem.LowLevel.MouseState{position=ScreenPoint(p),scroll=new Vector2(0,scroll)};if(pressed)state=state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left);UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse,state);await System.Threading.Tasks.Task.Delay(100);}
        try{
            hud.Input.BeginEditing();hud.Input.Select(WorldTool.Foundation);int pending=hud.Input.Edits.Count;float zoom=s.CameraRig.Zoom;
            var point=map.worldBound.position+map.worldBound.size*.65f;await Send(point,false);Check(hud.Input.PointerOverUI,"Minimap hover blocks world");await Send(point,true);Check(map.Interacting,"Real pointer capture");Check(s.CameraRig.BlockKeyboard,"Drag blocks WASD");Check((s.CameraRig.Focus-original).sqrMagnitude>1,"Real pointer navigates");
            await Send(point+new Vector2(-15,10),true,1);Check(Mathf.Approximately(zoom,s.CameraRig.Zoom),"Map wheel does not zoom world");await Send(point,false);Check(!map.Interacting&&hud.Input.Edits.Count==pending,"No placement on release");return "PASS actual InputSystem minimap click/drag camera navigation, WASD/wheel isolation and no foundation reservation.";
        }finally{UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse);oldMouse?.MakeCurrent();UnityEngine.InputSystem.InputSystem.settings=oldSettings;UnityEngine.Object.Destroy(settings);hud.Input.Cancel();s.CameraRig.MoveFocus(original);}
    }
}
