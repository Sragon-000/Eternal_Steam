using System;
using System.IO;
using System.Linq;
using System.Collections;
using UnityEngine;
using EternalSteam;
using EternalSteam.Railway;
using EternalSteam.OpenWorld;
public static class VerifyRailwayA3Map
{
 const string Root="/tmp/eternal-railway-a3-map";
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static OpenWorldSandbox World(){Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")==Root,"Isolated Play required");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(s.GetComponent<OpenWorldInput>().Edits!=null,"Wait for scene Start");s.Persistence.Automatic=false;s.Clock.Paused=true;return s;}
 static void Click(RailwayHud h,string cmd){h.SendMessage("Update");var b=h.Commands.First(x=>x.Command==cmd&&x.Button.gameObject.activeInHierarchy).Button;Check(b.interactable,"Disabled: "+cmd);b.onClick.Invoke();}
 public static string LoadFixture(){var s=World();var payload=File.ReadAllText("/tmp/eternal-railway-a1-after.json");var snapshot=JsonUtility.FromJson<SingleMapSnapshot>(payload);new JsonSaveStore(Root).Write(snapshot.runId,payload);s.Persistence.ContinueSaved();return "PASS isolated fixture reload requested";}
 public static string Main()
 {
  var s=World();var h=s.RailwayHud;var n=s.Content.Railway.Network;var r=n.Routes.Single();var input=s.GetComponent<OpenWorldInput>();var view=s.RailwayView;string before=JsonUtility.ToJson(n.Capture());
  h.Execute("open");h.RowButtons[0].onClick.Invoke();Click(h,"edit");Check(view.PortFrames.All(f=>f.gameObject.activeSelf),"Saved four ports visible");Check(view.SelectedLegHighlight.enabled,"Initial selected leg shown");
  Click(h,"draft-row:1");Check(h.SelectedDraftStationId==r.stops[1].stationId&&h.SelectedSegment.text.Contains("복귀"),"Row selects return leg with description");
  var station=n.Station(r.stops[1].stationId);for(int i=0;i<4;i++)Check(s.Content.GroundWorld.Grid.WorldToCell(view.PortFrames[i].transform.position)==RailwayNetwork.Port(station,i),"Port frame matches rotated socket "+i);
  Check(input.ClickWorld(s.Content.GroundWorld.Grid.Center(r.legs[0].cells[0],Vector2Int.one)),"Map segment click");Check(h.SelectedDraftStationId==r.stops[0].stationId,"Segment selects departure station");
  Check(view.SelectedLegHighlight.positionCount==Math.Max(2,r.legs[0].cells.Count)&&h.SelectedSegment.text.Contains(r.legs[0].cells.Count+"칸"),"Selected segment geometry and length");
  Check(input.ClickWorld(station.Position),"Map station click");Check(h.SelectedDraftStationId==station.PersistentId,"Map station synchronizes row");
  Click(h,"map-arrival");Check(h.PickingPort==RailwayHud.PortPick.Arrival,"Arrival picking armed");var wrong=RailwayNetwork.Port(station,r.stops[1].departure);Check(input.ClickWorld(s.Content.GroundWorld.Grid.Center(wrong,Vector2Int.one)),"Pick port cell");Check(h.DraftIssue?.Code==RouteIssueCode.InvalidPort,"Identical arrival/departure diagnosed immediately");Check(JsonUtility.ToJson(n.Capture())==before,"Port edit does not mutate actual route");
  Click(h,"map-arrival");Check(input.ClickWorld(s.Content.GroundWorld.Grid.Center(RailwayNetwork.Port(station,r.stops[1].arrival),Vector2Int.one)),"Restore port");Check(h.DraftIssue==null&&view.SelectedLegHighlight.enabled,"Correct port restores segment");
  Click(h,"map-departure");Click(h,"map-cancel");Check(h.PickingPort==RailwayHud.PortPick.None,"Cancel picking without edits");
  Click(h,"validate");Check(view.PortFrames.All(f=>!f.gameObject.activeSelf)&&!view.SelectedLegHighlight.enabled,"Confirmation hides editing overlays");h.Execute("port:0");Check(h.Stage==RailwayHud.EditorStage.Confirming&&JsonUtility.ToJson(n.Capture())==before,"Modal rejects forged port command");Click(h,"back-edit");
  input.BeginEditing();Check(!h.CanExecute("map-arrival",out _),"Construction blocks port mutation");Check(!h.TryHandleDraftWorldClick(station.Position),"Construction keeps its own click handler");input.Cancel();
  Click(h,"cancel");Click(h,"discard");Check(JsonUtility.ToJson(n.Capture())==before&&view.PortFrames.All(f=>!f.gameObject.activeSelf)&&!view.SelectedLegHighlight.enabled,"Discard clears overlays and preserves route");
  Click(h,"edit");Click(h,"map-arrival");return "PASS row/map/segment selection, authored port positions, immediate port diagnostics and correction, draft-only mutation, modal/construction guards, discard preservation";
 }
 public static string ScreenPort(){World().StartCoroutine(ScreenCheck());return "Frame screen-port check scheduled";}
 static IEnumerator ScreenCheck(){yield return new WaitForEndOfFrame();var s=World();var h=s.RailwayHud;var r=s.Content.Railway.Network.Routes.Single();int port=r.stops[0].arrival;var label=s.RailwayView.PortLabels[port];var screen=s.CameraRig.View.WorldToScreenPoint(label.rectTransform.TransformPoint(label.rectTransform.rect.center));var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=screen},hits);bool clear=hits.Count==0;bool handled=h.TryHandlePortScreenClick(screen);bool ok=clear&&handled&&h.PickingPort==RailwayHud.PortPick.None&&h.DraftIssue==null;File.WriteAllText("/tmp/eternal-a3-map-screen.json",JsonUtility.ToJson(new ScreenResult{passed=ok,unobstructed=clear,width=Screen.width,height=Screen.height,port=port+1,point=screen.ToString()},true));}
 [Serializable]public class ScreenResult{public bool passed,unobstructed;public int width,height,port;public string point;}
}
