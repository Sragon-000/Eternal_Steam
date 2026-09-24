using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using EternalSteam.OpenWorld;
public static class VerifyDragMouseInput
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static async Task<string> Main(){
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(Application.isPlaying&&s!=null,"Play required");
  var input=s.GetComponent<OpenWorldInput>();var hud=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>();
  Check(s.Content.GroundWorld.Buildings.Count==0,"Empty ground required");
  var oldMouse=Mouse.current;var mouse=InputSystem.AddDevice<Mouse>("DragTestMouse");var original=InputSystem.settings;var settings=UnityEngine.Object.Instantiate(original);
  InputSystem.settings=settings;settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
  var focus=s.CameraRig.Focus;float zoom=s.CameraRig.Zoom;bool visible=hud.Inventory.Visible;
  async Task Move(Vector2 p,bool down){InputSystem.QueueStateEvent(mouse,new MouseState{position=p,buttons=(ushort)(down?1:0)});await Task.Delay(160);}
  try{
   input.Cancel();hud.Inventory.SetVisible(false);
   Vector2Int cell=default;bool found=false;for(int z=-64;z<=64&&!found;z+=4)for(int x=-64;x<=64&&!found;x+=4)if(s.Content.CheckGround(new Vector2Int(x,z),new Vector2Int(8,8),out _,out _)){cell=new Vector2Int(x,z);found=true;}
   Check(found,"Ground patch");var a=WorldGridGeometry.Center(cell,2);var b=WorldGridGeometry.Center(cell+new Vector2Int(5,0),2);
   a.y=s.Ground.SampleHeight(a)+s.Ground.transform.position.y;b.y=s.Ground.SampleHeight(b)+s.Ground.transform.position.y;
   s.CameraRig.Focus=(a+b)*.5f;s.CameraRig.Zoom=18;await Task.Delay(200);
   Vector2 p=s.CameraRig.View.WorldToScreenPoint(a),q=s.CameraRig.View.WorldToScreenPoint(b);
   input.BeginEditing();input.SelectContent(s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.wall"));
   await Move(p,false);Check(!input.PointerOverUI,"Start outside UI");await Move(p,true);Check(input.Dragging&&input.Edits.Count==0,"Mouse press captures without placing");
   await Move(q,true);Check(input.Edits.ContentPending.Count==0,"Holding and moving must not paint walls");Check(s.CameraRig.BlockKeyboard&&s.CameraRig.BlockPointer,"Camera blocked during gesture");
   await Move(q,false);Check(!input.Dragging&&input.Edits.Count==0,"First click only stores start");await Move(q,true);await Move(q,false);Check(input.Edits.ContentPending.Count==6,"Second click reserves straight six-cell line");Check(input.Confirm(),"Confirm walls");
   var buildings=s.Content.GroundWorld.Buildings.ToArray();var bounds=BuildingRectangleSelection.Bounds(s.CameraRig.View.WorldToScreenPoint(buildings[0].Position),s.CameraRig.View.WorldToScreenPoint(buildings[5].Position));
   input.BeginEditing();var lo=bounds.min-Vector2.one*15;var hi=bounds.max+Vector2.one*15;
   await Move(lo,false);await Move(lo,true);await Move(hi,true);Check(input.Dragging,"Actual rectangle drag active");await Move(hi,false);
   Check(input.Edits.RecoveryCount==6&&buildings.All(x=>!x.Disposed),"Mouse rectangle marks six without premature removal");input.Cancel();Check(buildings.All(x=>!x.Disposed),"Cancel preserves walls");
   return "PASS: InputSystem mouse press/held/release drives two-click six-cell wall line and rectangle recovery selection; camera blocked during drag; cancel preserves installed walls.";
  }finally{input.Cancel();foreach(var b in s.Content.GroundWorld.Buildings.ToArray())s.Content.GroundWorld.Remove(b.Id);s.CameraRig.Focus=focus;s.CameraRig.Zoom=zoom;hud.Inventory.SetVisible(visible);InputSystem.RemoveDevice(mouse);oldMouse?.MakeCurrent();InputSystem.settings=original;UnityEngine.Object.Destroy(settings);}
 }
}
