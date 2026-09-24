using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using EternalSteam.OpenWorld;
public static class VerifyOpenWorldCameraInput
{
 public static async Task<string> Main()
 {
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();if(s==null)throw new Exception("OpenWorld Play required");
  var originalKeyboard=Keyboard.current;var keyboard=InputSystem.AddDevice<Keyboard>("OpenWorldTestKeyboard");var root=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>().GetComponent<UIDocument>().rootVisualElement;
  var originalMouse=Mouse.current;var mouse=InputSystem.AddDevice<Mouse>("OpenWorldTestMouse");
  float savedZoom=s.CameraRig.Zoom;
  Vector3 saved=s.CameraRig.Focus;
  var originalSettings=InputSystem.settings;var settings=UnityEngine.Object.Instantiate(originalSettings);
  InputSystem.settings=settings;settings.scrollDeltaBehavior=InputSettings.ScrollDeltaBehavior.UniformAcrossAllPlatforms;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
  try {
   (root.Q<Button>("explore") ?? root.Q<Button>("run")).Focus();await Task.Delay(100);var before=s.CameraRig.Focus;
   InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));await Task.Delay(200);InputSystem.ResetDevice(keyboard);
   if(s.CameraRig.Focus.z<=before.z)throw new Exception("W must move camera: pressed="+keyboard.wKey.isPressed+" block="+s.CameraRig.BlockKeyboard+" current="+(Keyboard.current==keyboard));
   using(var e=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0})){e.target=root.Q<TextField>("amount");root.Q<TextField>("amount").SendEvent(e);}await Task.Delay(100);before=s.CameraRig.Focus;
   InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));await Task.Delay(200);InputSystem.ResetDevice(keyboard);
   if(Vector3.Distance(before,s.CameraRig.Focus)>.001f)throw new Exception("Typing must block camera");
   using(var e=KeyDownEvent.GetPooled(new Event{type=EventType.KeyDown,keyCode=KeyCode.Return})){e.target=root.Q<TextField>("amount");root.Q<TextField>("amount").SendEvent(e);}
   InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(Screen.width*.5f,Screen.height*.6f)});await Task.Delay(100);
   var terrainPoint=new Vector2(Screen.width*.5f,Screen.height*.6f);
   async Task Wheel(float delta,Vector2 point) {
    InputSystem.QueueStateEvent(mouse,new MouseState {position=point,scroll=new Vector2(0,delta)});await Task.Delay(100);
   }
   s.CameraRig.Zoom=26;float zoom=s.CameraRig.Zoom;
   await Wheel(1,terrainPoint);
   float expected=zoom*Mathf.Exp(-.12f);
   if(Mathf.Abs(s.CameraRig.Zoom-expected)>.001f || Mathf.Abs(s.CameraRig.View.orthographicSize-expected)>.001f)throw new Exception("One normalized notch must visibly zoom in once");
   await Task.Delay(100);if(Mathf.Abs(s.CameraRig.Zoom-expected)>.001f)throw new Exception("No zoom drift without wheel input");
   await Wheel(-1,terrainPoint);if(Mathf.Abs(s.CameraRig.Zoom-zoom)>.001f)throw new Exception("Reverse wheel restores zoom");
   await Wheel(.25f,terrainPoint);if(Mathf.Abs(s.CameraRig.Zoom-zoom*Mathf.Exp(-.03f))>.001f)throw new Exception("Fractional trackpad input preserved");
   s.CameraRig.Zoom=26;await Wheel(3,terrainPoint);if(Mathf.Abs(s.CameraRig.Zoom-26*Mathf.Exp(-.36f))>.001f)throw new Exception("Multiple wheel steps accumulate");
   await Wheel(100,terrainPoint);if(s.CameraRig.Zoom!=10)throw new Exception("Minimum zoom clamp");
   await Wheel(-100,terrainPoint);if(s.CameraRig.Zoom!=70)throw new Exception("Maximum zoom clamp");
   await Wheel(1,terrainPoint);if(s.CameraRig.Zoom>=70)throw new Exception("Can reverse away from limit");
   var uiPoint=new Vector2(Screen.width-30,Screen.height-40);
   InputSystem.QueueStateEvent(mouse,new MouseState {position=uiPoint});await Task.Delay(100);zoom=s.CameraRig.Zoom;
   await Wheel(1,uiPoint);await Wheel(-1,uiPoint);
   if(s.CameraRig.Zoom!=zoom)throw new Exception("UI scroll must not zoom camera");
   return "PASS: InputSystem virtual keyboard W pans, text focus blocks WASD, normalized +/-1 wheel changes actual camera size, reverse/no-drift/fractional/multiple-step/limits, UI scroll blocked.";
  } finally {InputSystem.ResetDevice(keyboard);s.CameraRig.Focus=saved;(root.Q<Button>("explore") ?? root.Q<Button>("run")).Focus();InputSystem.RemoveDevice(mouse);originalMouse?.MakeCurrent();s.CameraRig.Zoom=savedZoom;InputSystem.RemoveDevice(keyboard);originalKeyboard?.MakeCurrent();InputSystem.settings=originalSettings;UnityEngine.Object.Destroy(settings);}
 }
}
