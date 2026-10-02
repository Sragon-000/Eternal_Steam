using System;
using System.IO;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.Railway;
using EternalSteam.OpenWorld;
public static class VerifyRailwayA2
{
 const string Root="/tmp/eternal-railway-a2";
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static OpenWorldSandbox World(){Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")==Root,"Isolated Play required");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(s.GetComponent<OpenWorldInput>().Edits!=null,"Wait for input Start after scene reload before invoking this phase");s.Persistence.Automatic=false;s.Clock.Paused=true;Check(!s.Persistence.Blocked,s.Persistence.Status);return s;}
 static void Click(RailwayHud h,string command){var binding=h.Commands.First(b=>b.Command==command&&b.Button.gameObject.activeInHierarchy);binding.Button.onClick.Invoke();}
 public static string LoadFixture()
 {
  var s=World();s.RailwayHud.Execute("open");Check(!s.RailwayHud.CanExecute("new",out _),"New disabled without two active stations");
  var payload=File.ReadAllText("/tmp/eternal-railway-a1-after.json");var data=JsonUtility.FromJson<SingleMapSnapshot>(payload);new JsonSaveStore(Root).Write(data.runId,payload);s.Persistence.ContinueSaved();return "PASS fresh new-route gate; load A1 isolated railway fixture";
 }
 public static string Main()
 {
  var s=World();var h=s.RailwayHud;var n=s.Content.Railway.Network;var route=n.Routes.Single();h.Execute("open");h.RowButtons[0].onClick.Invoke();
  var original=JsonUtility.ToJson(n.Capture());Click(h,"edit");Check(h.HasDraft&&h.DraftPanel.activeSelf,"Editing panel");h.Execute("commit");Check(h.HasDraft&&JsonUtility.ToJson(n.Capture())==original,"Commit before confirmation rejected without mutation");
  Click(h,"validate");Check(h.Stage==RailwayHud.EditorStage.Confirming&&h.ConfirmPanel.activeSelf&&!h.DraftPanel.activeSelf,"Dedicated confirmation");Check(h.Summary.text.Contains("총 ")&&h.Summary.text.Contains("예상 ")&&s.RailwayView.RouteHighlight.enabled,"Preview lengths/time/path");Click(h,"back-edit");Check(h.HasDraft&&h.DraftPanel.activeSelf,"Back preserves draft");
  Click(h,"arrival");Click(h,"cancel");Check(h.DiscardPanel.activeSelf&&h.HasDraft,"Dirty cancel opens discard prompt");h.Execute("start");Check(JsonUtility.ToJson(n.Capture())==original,"Modal rejects unrelated command");Click(h,"keep");Check(h.DraftPanel.activeSelf&&h.HasDraft,"Keep returns to draft");Click(h,"cancel");Click(h,"discard");Check(!h.HasDraft&&h.DetailPanel.activeSelf&&JsonUtility.ToJson(n.Capture())==original,"Discard returns to original detail without mutation");
  Click(h,"edit");Click(h,"validate");int revision=route.revision;Click(h,"commit");Check(!h.HasDraft&&route.revision==revision+1,"Confirmed revision committed");h.Execute("commit");Check(route.revision==revision+1,"Repeated commit rejected");
  Click(h,"edit");Click(h,"validate");var track=s.Content.GroundWorld.Buildings.First(b=>b.Module<RailFacility>()?.Kind==RailFacilityKind.Track&&route.legs.Any(l=>l.cells.Contains(b.Cell)));var input=s.GetComponent<OpenWorldInput>();input.BeginEditing();Check(input.Edits.ToggleGroundRecovery(track,out var error),error);Check(input.Confirm(),s.Message);input.Cancel();s.Content.Railway.Refresh();Click(h,"commit");Check(h.HasDraft&&h.DraftPanel.activeSelf&&route.revision==revision+1,"Topology changed after confirmation: commit revalidates and returns to editing");
  Click(h,"cancel");if(h.HasDraft)Click(h,"discard");s.Persistence.ContinueSaved();return "PASS edit/confirm/back, dirty discard/keep/modal guards, repeated commit, stale topology revalidation; restore fixture for new-route tests";
 }
 public static string NewRoute()
 {
  var s=World();var h=s.RailwayHud;var n=s.Content.Railway.Network;var old=n.Routes.Single();var stops=old.stops.Select(x=>x.Copy()).ToArray();var snapshot=n.Capture();snapshot.routes.Clear();n.Restore(snapshot);s.Content.Railway.Tick(0);
  h.Execute("open");Click(h,"new");var input=s.GetComponent<OpenWorldInput>();
  foreach(var stop in stops){input.Cancel();Check(input.ClickWorld(n.Station(stop.stationId).Position),"Select station");h.SendMessage("Update");Click(h,"add");for(int i=0;i<stop.arrival;i++)Click(h,"arrival");for(int i=1;i<stop.departure;i++)Click(h,"departure");if(stop.departure==0)for(int i=0;i<3;i++)Click(h,"departure");}
  Click(h,"close-loop");Click(h,"validate");Check(h.ConfirmPanel.activeSelf&&h.Summary.text.Contains("정지 상태"),"New route confirmation explains stopped train");Click(h,"close");Check(h.DiscardPanel.activeSelf,"Close asks before discarding");Click(h,"keep");Check(h.ConfirmPanel.activeSelf,"Keep returns to confirmation");Click(h,"commit");Check(n.Routes.Count==1&&n.Routes.Single().train.status==TrainStatus.Stopped,"One new stopped train");h.Execute("commit");Check(n.Routes.Count==1,"No duplicate train");Check(!h.CanExecute("start",out _),"Unconfigured start disabled");
  h.Resource.text="iron";h.Load.text="0";h.Unload.text="0";Click(h,"configure");var r=n.Routes.Single();Check(!h.CanExecute("start",out _),"No-fuel start disabled");Check(n.Refuel(r,50,out var error),error);Click(h,"start");Check(r.train.status==TrainStatus.Dwelling,"Manual start enabled after config/fuel");h.SendMessage("Update");Check(h.Resource.readOnly&&h.Load.readOnly&&h.Unload.readOnly&&!h.CanExecute("configure",out _),"Moving/dwelling inputs locked");
  string resource=r.train.resource;h.Resource.text="coal";h.Execute("configure");Check(r.train.resource==resource,"Disabled configure callback cannot mutate");n.Stop(r);h.Resource.text="iron";h.SendMessage("Update");Check(!h.Load.readOnly&&h.Resource.readOnly,"Stopped quantities editable; assigned resource stays locked");
  h.Execute("edit");Click(h,"validate");return "PASS new confirm/close/keep/commit, no duplicate train, start gates and running input lock; confirmation visible for review";
 }
 public static string CloseAndReopen()
 {
  var s=World();var h=s.RailwayHud;Click(h,"back-edit");Click(h,"arrival");Click(h,"close");Check(h.DiscardPanel.activeSelf,"Close prompt");
  Check(h.Commands.Where(b=>b.Command=="open").All(b=>!b.Button.interactable),"Toolbar blocked while discard pending");Click(h,"discard");Check(!h.Panel.activeSelf&&!h.HasDraft,"Discard closes panel");
  Check(h.Commands.Where(b=>b.Command=="open").All(b=>b.Button.interactable),"Toolbar restored after close");h.Execute("open");Check(h.Panel.activeSelf&&h.ListPanel.activeSelf,"Reopen list");h.RowButtons[0].onClick.Invoke();Click(h,"edit");Click(h,"validate");return "PASS discard closes and re-enables toolbar, reopen list and confirmation";
 }

}
