using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using EternalSteam.OpenWorld;
public static class VerifyQuantityFocus
{
 static void Check(bool ok,string m){if(!ok)throw new Exception(m);}
 public static async Task<string> Main()
 {
  var w=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var root=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>().GetComponent<UIDocument>().rootVisualElement;
  var field=root.Q<TextField>("amount");var original=InputSystem.settings;var settings=UnityEngine.Object.Instantiate(original);InputSystem.settings=settings;
  settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
  var oldKey=Keyboard.current;var key=InputSystem.AddDevice<Keyboard>("QuantityTestKeyboard");
  var oldMouse=Mouse.current;var mouse=InputSystem.AddDevice<Mouse>("QuantityTestMouse");Vector3 saved=w.CameraRig.Focus;
  void Pointer(){using(var e=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,button=0})){e.target=field;field.SendEvent(e);}}
  void Press(KeyCode code,char c){using(var e=KeyDownEvent.GetPooled(new Event{type=EventType.KeyDown,keyCode=code,character=c})){e.target=field;field.SendEvent(e);}}
  try {
   field.value="100";field.Focus();await Task.Delay(80);Check(field.isReadOnly,"Programmatic/navigation focus must not arm editing");
   var before=w.CameraRig.Focus;InputSystem.QueueStateEvent(key,new KeyboardState(Key.W));InputSystem.QueueTextEvent(key,'w');await Task.Delay(150);InputSystem.ResetDevice(key);
   Check(w.CameraRig.Focus.z>before.z,"W moves camera without clicking input");Check(field.value=="100","W does not enter quantity");
   Pointer();await Task.Delay(80);Check(!field.isReadOnly&&w.CameraRig.BlockKeyboard,"Only pointer click arms typing");before=w.CameraRig.Focus;
   InputSystem.QueueStateEvent(key,new KeyboardState(Key.W));InputSystem.QueueTextEvent(key,'w');Press(KeyCode.W,'w');await Task.Delay(150);InputSystem.ResetDevice(key);
   Check(field.value=="100","Letter blocked even when editing");Check(Vector3.Distance(before,w.CameraRig.Focus)<.001f,"Typing blocks camera");
   field.value="25";Check(field.value=="25","Digits accepted");field.value="2was";Check(field.value=="25","Invalid paste rejected");
   Press(KeyCode.Return,'\n');await Task.Delay(80);Check(field.isReadOnly&&!w.CameraRig.BlockKeyboard,"Enter releases input");
   Pointer();await Task.Delay(50);
   InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(Screen.width*.8f,Screen.height*.5f),buttons=1});await Task.Delay(80);InputSystem.ResetDevice(mouse);
   Check(field.isReadOnly&&!w.CameraRig.BlockKeyboard,"World click releases input");
   Pointer();await Task.Delay(50);var spawn=root.Q<Button>("spawn");using(var e=NavigationSubmitEvent.GetPooled()){e.target=spawn;spawn.SendEvent(e);}
   await Task.Delay(50);Check(field.isReadOnly&&!w.CameraRig.BlockKeyboard,"Spawn releases input");Check(w.Enemies.Spawned==25,"Spawn count exact");
   return "PASS: W camera with no input focus, explicit pointer typing, letter/paste rejection, digit input, Enter/world-click/spawn release, exact 25 spawn.";
  } finally {InputSystem.RemoveDevice(key);InputSystem.RemoveDevice(mouse);oldKey?.MakeCurrent();oldMouse?.MakeCurrent();InputSystem.settings=original;UnityEngine.Object.Destroy(settings);w.ResetEnemies();w.CameraRig.Focus=saved;field.value="100";}
 }
}
