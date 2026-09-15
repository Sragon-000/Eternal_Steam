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
  InputSystem.settings=settings;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
  try {
   (root.Q<Button>("explore") ?? root.Q<Button>("run")).Focus();await Task.Delay(100);var before=s.CameraRig.Focus;
   InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));await Task.Delay(200);InputSystem.ResetDevice(keyboard);
   if(s.CameraRig.Focus.z<=before.z)throw new Exception("W must move camera: pressed="+keyboard.wKey.isPressed+" block="+s.CameraRig.BlockKeyboard+" current="+(Keyboard.current==keyboard));
   using(var e=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0})){e.target=root.Q<TextField>("amount");root.Q<TextField>("amount").SendEvent(e);}await Task.Delay(100);before=s.CameraRig.Focus;
   InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));await Task.Delay(200);InputSystem.ResetDevice(keyboard);
   if(Vector3.Distance(before,s.CameraRig.Focus)>.001f)throw new Exception("Typing must block camera");
   using(var e=KeyDownEvent.GetPooled(new Event{type=EventType.KeyDown,keyCode=KeyCode.Return})){e.target=root.Q<TextField>("amount");root.Q<TextField>("amount").SendEvent(e);}
   InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(Screen.width*.8f,Screen.height*.5f)});await Task.Delay(100);
   float zoom=s.CameraRig.Zoom;
   InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(Screen.width*.8f,Screen.height*.5f),scroll=new Vector2(0,120)});await Task.Delay(100);
   if(s.CameraRig.Zoom>=zoom)throw new Exception("Wheel must zoom in over terrain");
   InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(20,Screen.height-40)});await Task.Delay(100);zoom=s.CameraRig.Zoom;
   InputSystem.QueueStateEvent(mouse,new MouseState {position=new Vector2(20,Screen.height-40),scroll=new Vector2(0,120)});await Task.Delay(100);
   if(s.CameraRig.Zoom!=zoom)throw new Exception("UI scroll must not zoom camera");
   return "PASS: InputSystem virtual keyboard W pans, text focus blocks WASD, virtual mouse wheel zooms terrain and is blocked over UI.";
  } finally {InputSystem.ResetDevice(keyboard);s.CameraRig.Focus=saved;(root.Q<Button>("explore") ?? root.Q<Button>("run")).Focus();InputSystem.RemoveDevice(mouse);originalMouse?.MakeCurrent();s.CameraRig.Zoom=savedZoom;InputSystem.RemoveDevice(keyboard);originalKeyboard?.MakeCurrent();InputSystem.settings=originalSettings;UnityEngine.Object.Destroy(settings);}
 }
}
