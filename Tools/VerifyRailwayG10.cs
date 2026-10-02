using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.Railway;
using EternalSteam.OpenWorld;
public static class VerifyRailwayG10
{
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 public static string Main(){
  Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")=="/tmp/eternal-railway-g12","Isolated Play");
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();s.Persistence.Automatic=false;s.Clock.Paused=true;var c=s.Content;var input=s.GetComponent<OpenWorldInput>();input.Cancel();
  var main=c.MainBase.Module<IBaseIdentity>().BaseId;c.Bases.Select(main);var bank=c.Inventories.Available(main);var station=s.RailwayHud.Station;var track=s.RailwayHud.Track;
  Check(WallPlacementStroke.Supports(track),"Linear rail stroke enabled");
  var empty=input.Edits.Quote(station);Check(empty.Stations==1&&empty.Iron==10&&empty.BaseId==main,"Single station quote");
  double initial=bank.Amount("iron");if(initial>0)bank.Withdraw("iron",initial);
  var poor=input.Edits.Quote(track,3);Check(poor.Tracks==3&&poor.Iron==3&&poor.Shortage==3&&!poor.Affordable,"Line shortage quote");
  bank.Deposit("iron",100);var funded=input.Edits.Quote(station);Check(funded.Affordable&&funded.Shortage==0,"Funded quote");
  input.BeginEditing();input.SelectContent(station);Vector3? anchor=null;Vector2Int selected=default;
  for(int z=9;z<28&&!anchor.HasValue;z++)for(int x=-8;x<12&&!anchor.HasValue;x++){
   var cell=new Vector2Int(x,z);var point=c.GroundWorld.Grid.Center(cell,station.Footprint);
   if(input.Edits.PreviewContent(station,point,out _,out _,out var snappedCell,out _)){anchor=point;selected=snappedCell;}
  }
  Check(anchor.HasValue,"Find valid station location");
  Check(input.ClickWorld(anchor.Value)&&input.ChoosingStationDirection,"First click anchors station");
  var direction=OpenWorldInput.SnapStationDirection(WorldGridGeometry.Rotation*Vector3.right*4);Check(input.ClickWorld(anchor.Value+direction*4),"Second click fixes direction");
  Check(!input.ChoosingStationDirection&&input.Edits.ContentPending.Count==1,"One reserved station");
  var pending=input.Edits.ContentPending.Single();Check(Vector3.Dot(pending.Request.Direction,direction)>.999f,"Direction stored in request");
  var quote=input.Edits.Quote();Check(quote.Stations==1&&quote.Iron==10&&quote.Affordable&&quote.Summary.Contains("철 10"),"Pending quote matches payment");
  var before=bank.Amount("iron");Check(Math.Abs(before-100)<.001,"Reservation does not charge");
  var port=RailwayNetwork.PortCell(selected,direction,1);s.RailwayView.PresentPlacementPorts(selected,direction,c);Check(s.RailwayView.PortFrames.All(f=>f.gameObject.activeSelf),"Four authored preview ports");
  Check(s.RailwayView.PortLabels[1].text.Contains("진출"),"Departure marker");
  ScreenCapture.CaptureScreenshot("/tmp/eternal-g10-station-preview.png");
  Check(input.Confirm(),s.Message);Check(Math.Abs(bank.Amount("iron")-(before-10))<.001,"Exactly quoted iron charged");
  var built=c.GroundWorld.Buildings.Last();Check(Vector3.Dot(built.Direction,direction)>.999f&&RailwayNetwork.Port(built,1)==port,"Installed orientation/port match preview");
  s.RailwayView.ClearDraftSelection();return "PASS track line quote/shortage, station two-click direction, four saved port markers, reservation unchanged, iron10 exact confirm, installed port match";
 }
}
