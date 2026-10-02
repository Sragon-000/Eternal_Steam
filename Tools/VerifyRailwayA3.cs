using System;
using System.IO;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.Railway;
using EternalSteam.OpenWorld;
public static class VerifyRailwayA3
{
 const string Root="/tmp/eternal-railway-a3";
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 static OpenWorldSandbox World(){Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")==Root,"Isolated Play required");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(s.GetComponent<OpenWorldInput>().Edits!=null,"Wait for scene Start");s.Persistence.Automatic=false;s.Clock.Paused=true;return s;}
 static void Click(RailwayHud h,string cmd){var b=h.Commands.First(x=>x.Command==cmd&&x.Button.gameObject.activeInHierarchy).Button;Check(b.interactable,"Button disabled: "+cmd);b.onClick.Invoke();}
 public static string LoadFixture(){var s=World();string payload=File.ReadAllText("/tmp/eternal-railway-a1-after.json");var snapshot=JsonUtility.FromJson<SingleMapSnapshot>(payload);new JsonSaveStore(Root).Write(snapshot.runId,payload);s.Persistence.ContinueSaved();return "PASS isolated fixture reload requested";}
 public static string Main()
 {
  var s=World();var h=s.RailwayHud;var n=s.Content.Railway.Network;var r=n.Routes.Single();string before=JsonUtility.ToJson(n.Capture());
  h.Execute("open");h.RowButtons[0].onClick.Invoke();Click(h,"edit");
  // Match the arrival to the authored departure, using actual serialized button callbacks.
  int count=(r.stops[0].departure-r.stops[0].arrival+4)%4;for(int i=0;i<count;i++)Click(h,"arrival");
  Check(h.DraftIssue?.Code==RouteIssueCode.InvalidPort,"Automatic port validation");Check(h.Summary.text.Contains("[오류]")&&h.Summary.text.Contains("수정 안내"),"Station row and recovery shown");
  Click(h,"validate");h.SendMessage("Update");Check(h.DraftIssue?.Code==RouteIssueCode.InvalidPort&&s.RailwayView.RouteHighlight.enabled&&s.RailwayView.RouteHighlight.positionCount==5,"Error highlight survives refresh");
  // Interleaving unrelated domain validation must not replace the editor's diagnostic.
  Check(n.ValidateDraft(r.stops,r.id,out _,out _),"Stored route valid");h.SendMessage("Update");Check(h.DraftIssue?.Code==RouteIssueCode.InvalidPort,"Editor result isolated from network calls");
  for(int i=count;i<4;i++)Click(h,"arrival");Check(h.DraftIssue==null,"Corrected ports clear stale error");Click(h,"validate");Check(h.Stage==RailwayHud.EditorStage.Confirming,"Correction reaches confirmation");
  Click(h,"cancel");Click(h,"discard");Check(JsonUtility.ToJson(n.Capture())==before,"Diagnostics/discard do not mutate route");
  Click(h,"edit");Click(h,"validate");var track=s.Content.GroundWorld.Buildings.First(b=>b.Module<RailFacility>()?.Kind==RailFacilityKind.Track&&b.Cell==r.legs[0].cells[0]);var input=s.GetComponent<OpenWorldInput>();input.BeginEditing();Check(input.Edits.ToggleGroundRecovery(track,out var error),error);Check(input.Confirm(),s.Message);input.Cancel();s.Content.Railway.Refresh();Click(h,"commit");h.SendMessage("Update");
  Check(h.Stage==RailwayHud.EditorStage.Editing&&h.DraftIssue?.Code==RouteIssueCode.MissingTrack,"Stale confirmation yields structured topology error");Check(h.Summary.text.Contains("수정 안내")&&s.RailwayView.RouteHighlight.enabled,"Commit error stays visible");
  return "PASS automatic port validation, row/recovery feedback, persistent map highlight, independent diagnostic result, correction and discard, stale commit structured error";
 }
}
