using System;
using System.IO;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.Railway;
using EternalSteam.OpenWorld;
public static class VerifyRailwayA3Issues
{
 const string Root="/tmp/eternal-railway-a3-issues";
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static OpenWorldSandbox World(){Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")==Root,"Isolated Play required");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(s.GetComponent<OpenWorldInput>().Edits!=null,"Wait for scene Start");s.Persistence.Automatic=false;s.Clock.Paused=true;return s;}
 static void Click(RailwayHud h,string cmd){h.SendMessage("Update");var b=h.Commands.First(x=>x.Command==cmd&&x.Button.gameObject.activeInHierarchy).Button;Check(b.interactable,"Disabled: "+cmd);b.onClick.Invoke();}
 public static string LoadFixture(){var s=World();var payload=File.ReadAllText("/tmp/eternal-railway-a1-after.json");var snapshot=JsonUtility.FromJson<SingleMapSnapshot>(payload);new JsonSaveStore(Root).Write(snapshot.runId,payload);s.Persistence.ContinueSaved();return "PASS isolated fixture reload requested";}
 public static string Main()
 {
  var s=World();var h=s.RailwayHud;var n=s.Content.Railway.Network;var r=n.Routes.Single();string before=JsonUtility.ToJson(n.Capture());h.Execute("open");h.RowButtons[0].onClick.Invoke();Click(h,"edit");
  for(int row=0;row<2;row++){Click(h,"draft-row:"+row);Click(h,"map-arrival");h.Execute("port:"+r.stops[row].departure);}
  Check(h.DraftIssues.Count==2&&h.DraftIssues.All(i=>i.Code==RouteIssueCode.InvalidPort),"Two independent port errors");Check(h.IssueRows.Take(2).All(b=>b.gameObject.activeSelf)&&h.Summary.text.Contains("[검사 불가]"),"Rows and blocked segment states");
  Click(h,"issue:1");Check(h.SelectedDraftStationId==r.stops[1].stationId&&h.DraftIssue.StopIndex==1,"Error selects second station");Check(s.RailwayView.PortLabels[r.stops[1].departure].color==Color.red,"Selected bad port highlighted red");Click(h,"map-arrival");h.Execute("port:"+r.stops[1].arrival);Check(h.DraftIssues.Count==1&&h.DraftIssue.StopIndex==0,"Correction retains only remaining error");
  Click(h,"issue:0");Click(h,"map-arrival");h.Execute("port:"+r.stops[0].arrival);Check(h.DraftIssues.Count==0&&h.Summary.text.Contains("[정상]"),"All corrections restore valid states");Click(h,"validate");Check(h.Stage==RailwayHud.EditorStage.Confirming,"Confirmation after corrections");Click(h,"cancel");Click(h,"discard");Check(JsonUtility.ToJson(n.Capture())==before,"Draft diagnostics never mutate route");
  // Recover the first outbound track. The independent return remains selectable and valid.
  var input=s.GetComponent<OpenWorldInput>();var firstCell=r.legs[0].cells[0];var first=s.Content.GroundWorld.Buildings.First(b=>b.Module<RailFacility>()?.Kind==RailFacilityKind.Track&&b.Cell==firstCell);input.BeginEditing();Check(input.Edits.ToggleGroundRecovery(first,out var error),error);Check(input.Confirm(),s.Message);input.Cancel();Click(h,"edit");
  Check(h.DraftIssues.Count==1&&h.Summary.text.Contains("구간 2: [정상]"),"Later valid segment retains index");Click(h,"draft-row:1");Check(s.RailwayView.SelectedLegHighlight.enabled,"Later segment highlights despite earlier failure");
  var secondCell=r.legs[1].cells[0];var second=s.Content.GroundWorld.Buildings.First(b=>b.Module<RailFacility>()?.Kind==RailFacilityKind.Track&&b.Cell==secondCell);input.BeginEditing();Check(input.Edits.ToggleGroundRecovery(second,out error),error);Check(input.Confirm(),s.Message);input.Cancel();Click(h,"validate");Check(h.DraftIssues.Count==2&&h.DraftIssues.All(i=>i.LegIndex>=0),"Both broken segments reported");Click(h,"issue:1");Check(h.SelectedDraftStationId==r.stops[1].stationId&&h.DraftIssue.ReturnLeg&&s.RailwayView.RouteHighlight.enabled,"Return error selection highlights cell and station");return "PASS multiple port errors, per-segment blocked/valid/error states, selectable diagnostics and red port, correction/discard preservation, later valid segment, two broken segments";
 }
}
