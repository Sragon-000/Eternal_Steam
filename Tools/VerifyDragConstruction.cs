using System;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.Demo;
using EternalSteam.OpenWorld;
public static class VerifyDragConstruction
{
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 public static string Main(){
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(Application.isPlaying&&s!=null,"Play required");var input=s.GetComponent<OpenWorldInput>();
  Check(s.Content.GroundWorld.Buildings.Count==0&&s.Foundations.Platforms.Count==0,"Empty scene required");
  var wall=s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.wall");var camera=s.CameraRig.View;var oldFocus=s.CameraRig.Focus;float oldZoom=s.CameraRig.Zoom;
  Vector2Int anchor=default;bool found=false;
  for(int z=-64;z<=64&&!found;z+=4)for(int x=-64;x<=64&&!found;x+=4)if(s.Content.CheckGround(new Vector2Int(x,z),new Vector2Int(8,8),out _,out _)){anchor=new Vector2Int(x,z);found=true;}
  Check(found,"16m flat test patch required");
  Vector3 Point(int x,int z)=>WorldGridGeometry.Center(anchor+new Vector2Int(x,z),2);
  Vector2 Screen(Vector3 p)=>camera.WorldToScreenPoint(p);
  void Box(Vector3 a,Vector3 b,bool force=false){var bounds=BuildingRectangleSelection.Bounds(Screen(a),Screen(b));var lo=bounds.min-Vector2.one*12;var hi=bounds.max+Vector2.one*12;input.BeginPointer(lo,null,force);input.MovePointer(hi,null);input.EndPointer(hi,null);}
  try{
   input.Cancel();s.CameraRig.Focus=Point(4,4);s.CameraRig.Zoom=26;camera.orthographicSize=26;camera.transform.rotation=Quaternion.Euler(60,0,0);camera.transform.position=Point(4,4)-camera.transform.forward*110;
   input.BeginEditing();input.SelectContent(wall);var a=Point(0,0);var b=Point(5,2);
   void Click(Vector3 point){input.BeginPointer(Screen(point),point);input.EndPointer(Screen(point),point);}
   Click(a);Check(input.Edits.Count==0,"First click only anchors start");input.MovePointer(Screen(b),b);Check(input.Edits.Count==0,"Hover does not reserve");Click(b);
   Check(input.Edits.ContentPending.Count==6,"Endpoint snaps to dominant grid axis");
   Check(input.Edits.ContentPending.All(x=>x.Request.Cell.y==anchor.y),"Straight row, no staircase");
   Check(input.Confirm(),"Line commit");var installed=s.Content.GroundWorld.Buildings.ToArray();
   input.BeginEditing();input.SelectContent(wall);Box(installed.First().Position,installed.Last().Position,true);Check(input.Edits.RecoveryCount==6,"Shift rectangle recovery unchanged");Box(installed.Last().Position,installed.First().Position,true);Check(input.Edits.RecoveryCount==6,"Selection remains additive");input.Cancel();Check(installed.All(x=>!x.Disposed),"Cancel preserves walls");
   input.BeginEditing();Box(installed.First().Position,installed.Last().Position);Check(input.Confirm()&&installed.All(x=>x.Disposed),"Rectangle recovery commit");
   input.BeginEditing();input.SelectContent(wall);Click(Point(0,7));Click(Point(0,7));Check(input.Edits.Count==1,"Same cell endpoints create one wall");Click(a);input.AbortPointer();Check(input.Edits.Count==1,"Abort unfinished line preserves prior reservations");Click(b);Check(input.Edits.Count==1,"Next click starts fresh line");input.Cancel();Check(input.Edits.Count==0,"Cancel releases completed line reservations and anchor");
   input.BeginEditing();input.SelectContent(wall);Click(Point(2,5));Click(Point(0,0));Check(input.Edits.ContentPending.Count==6&&input.Edits.ContentPending.All(x=>x.Request.Cell.x==anchor.x+2),"Reverse vertical axis line");input.Cancel();
   // Same local building IDs in separate foundation worlds must both be selected, floors retained.
   input.BeginEditing();input.Select(WorldTool.Foundation);Check(input.ClickWorld(WorldGridGeometry.Center(new Vector2Int(anchor.x/4,anchor.y/4),8)),"Foundation one");Check(input.ClickWorld(WorldGridGeometry.Center(new Vector2Int(anchor.x/4+1,anchor.y/4),8))&&input.Confirm(),"Foundation two");
   var platforms=s.Foundations.Platforms.ToArray();input.BeginEditing();input.Select(WorldTool.Tower);
   foreach(var p in platforms)Check(input.ClickWorld(p.World.Grid.Center(Vector2Int.zero,Vector2Int.one)),"Legacy tower reservation");Check(input.Confirm(),"Legacy towers commit");
   var towers=s.Towers.ToArray();Check(towers.Length==2&&towers[0].building.Id==towers[1].building.Id,"Fixture exercises duplicate local IDs");
   input.BeginEditing();Check(input.ClickWorld(towers[0].building.Position),"Existing single selection");Box(towers[0].building.Position,towers[1].building.Position);Check(input.Edits.RecoveryCount==2,"Rectangle adds cross-world targets without toggling prior selection");Check(input.Confirm()&&s.Towers.Count==0&&s.Foundations.Platforms.Count==2,"Only upper buildings recovered, foundations survive");
   return "PASS: two-click straight wall rows/columns, same-cell line, anchor cancellation, pending preservation, commit/cancel, Shift/reversed/additive rectangle recovery and cross-foundation IDs.";
  }finally{
   input.PointerOverUI=false;input.Cancel();foreach(var p in s.Foundations.Platforms.ToArray())s.Foundations.RemoveFoundation(p);foreach(var x in s.Content.GroundWorld.Buildings.ToArray())s.Content.GroundWorld.Remove(x.Id);s.CameraRig.Focus=oldFocus;s.CameraRig.Zoom=oldZoom;
  }
 }
}
