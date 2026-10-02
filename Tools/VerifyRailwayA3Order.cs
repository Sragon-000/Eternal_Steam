using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine;
using EternalSteam;
using EternalSteam.Railway;
using EternalSteam.OpenWorld;
public static class VerifyRailwayA3Order
{
 const string Root="/tmp/eternal-railway-a3-order";
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static OpenWorldSandbox World(){Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")==Root,"Isolated Play required");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(s.GetComponent<OpenWorldInput>().Edits!=null,"Wait for scene Start");s.Persistence.Automatic=false;s.Clock.Paused=true;return s;}
 static void Click(RailwayHud h,string cmd){h.SendMessage("Update");var b=h.Commands.First(x=>x.Command==cmd&&x.Button.gameObject.activeInHierarchy).Button;Check(b.interactable,"Disabled: "+cmd);b.onClick.Invoke();}
 public static string LoadFixture(){var s=World();var payload=File.ReadAllText("/tmp/eternal-railway-a1-after.json");var data=JsonUtility.FromJson<SingleMapSnapshot>(payload);new JsonSaveStore(Root).Write(data.runId,payload);s.Persistence.ContinueSaved();return "PASS fixture reload requested";}
 public static string Main()
 {
  var s=World();var h=s.RailwayHud;var n=s.Content.Railway.Network;var route=n.Routes.Single();var first=route.stops[0].stationId;var second=route.stops[1].stationId;string original=JsonUtility.ToJson(n.Capture());string train=route.trainId;
  h.Execute("open");h.RowButtons[0].onClick.Invoke();Click(h,"edit");Check(h.DraftStopsPanel.activeSelf,"Authored selection panel visible");Click(h,"draft-row:1");Check(h.SelectedDraftStationId==second,"Direct row selection");Click(h,"up");Check(h.SelectedDraftStationId==second,"Up preserves identity");Click(h,"down");Check(h.SelectedDraftStationId==second,"Down preserves identity");Click(h,"first");Check(h.SelectedDraftStationId==second&&h.DraftRows[0].GetComponentInChildren<TMPro.TMP_Text>().text.Contains("> "),"First rotates loop and keeps selection");
  Click(h,"open-loop");Check(!h.DraftLoopClosed&&!h.CanExecute("validate",out _),"Open loop blocks validation");h.Execute("validate");h.Execute("commit");Check(JsonUtility.ToJson(n.Capture())==original,"Open loop cannot commit");Click(h,"close-loop");Click(h,"validate");Check(h.Stage==RailwayHud.EditorStage.Confirming,"Closed loop validates");Click(h,"commit");Check(route.stops[0].stationId==second&&route.trainId==train&&route.stops[route.train.stop].stationId==first,"Confirmed rotation preserves train/current station");
  Click(h,"edit");Click(h,"draft-row:1");Check(!h.CanExecute("remove",out _),"Occupied station deletion blocked");Click(h,"cancel");
  Check(n.Start(route,out var error),error);h.SendMessage("Update");Click(h,"edit");Click(h,"draft-row:1");Check(!h.CanExecute("first",out _)&&!h.CanExecute("up",out _),"Running revision anchor protected");h.Execute("first");Check(h.SelectedDraftStationId==first,"Rejected command keeps selected station");Click(h,"draft-row:0");Check(!h.CanExecute("down",out _)&&!h.CanExecute("remove",out _),"Anchor cannot move or be removed");Click(h,"cancel");n.Stop(route);h.SendMessage("Update");
  Click(h,"list");Click(h,"new");Check(!h.DraftLoopClosed&&!h.CanExecute("close-loop",out _),"Empty new draft starts open");Click(h,"cancel");
  Click(h,"row:0");Click(h,"edit");return "PASS direct rows, up/down identity, first-station rotation/current train preservation, explicit loop gates, occupied stop protection, running anchor guards and new draft initial state";
 }
 public static string NewRoute()
 {
  var s=World();var h=s.RailwayHud;if(h.HasDraft)Click(h,"cancel");var n=s.Content.Railway.Network;var old=n.Routes.Single();var stops=old.stops.Select(x=>x.Copy()).ToArray();var snapshot=n.Capture();snapshot.routes.Clear();n.Restore(snapshot);s.Content.Railway.Tick(0);
  h.Execute("open");Click(h,"new");var input=s.GetComponent<OpenWorldInput>();
  foreach(var stop in stops){input.Cancel();Check(input.ClickWorld(n.Station(stop.stationId).Position),"Select station");h.SendMessage("Update");Click(h,"add");for(int i=0;i<stop.arrival;i++)Click(h,"arrival");for(int i=1;i<stop.departure;i++)Click(h,"departure");if(stop.departure==0)for(int i=0;i<3;i++)Click(h,"departure");}
  Check(!h.CanExecute("validate",out _),"New draft requires explicit loop closure");Click(h,"close-loop");Click(h,"validate");Check(h.ConfirmPanel.activeSelf&&h.Summary.text.Contains("정지 상태"),"New route confirmation explains stopped train");Click(h,"close");Check(h.DiscardPanel.activeSelf,"Close asks before discarding");Click(h,"keep");Check(h.ConfirmPanel.activeSelf,"Keep returns to confirmation");Click(h,"commit");Check(n.Routes.Count==1&&n.Routes.Single().train.status==TrainStatus.Stopped,"One new stopped train");h.Execute("commit");Check(n.Routes.Count==1,"No duplicate train");Check(!h.CanExecute("start",out _),"Unconfigured start disabled");
  h.Resource.text="iron";h.Load.text="0";h.Unload.text="0";Click(h,"configure");var r=n.Routes.Single();Check(!h.CanExecute("start",out _),"No-fuel start disabled");Check(n.Refuel(r,50,out var error),error);Click(h,"start");Check(r.train.status==TrainStatus.Dwelling,"Manual start enabled after config/fuel");h.SendMessage("Update");Check(h.Resource.readOnly&&h.Load.readOnly&&h.Unload.readOnly&&!h.CanExecute("configure",out _),"Moving/dwelling inputs locked");
  string resource=r.train.resource;h.Resource.text="coal";h.Execute("configure");Check(r.train.resource==resource,"Disabled configure callback cannot mutate");n.Stop(r);h.Resource.text="iron";h.SendMessage("Update");Check(!h.Load.readOnly&&h.Resource.readOnly,"Stopped quantities editable; assigned resource stays locked");
  h.Execute("edit");Click(h,"validate");return "PASS new confirm/close/keep/commit, no duplicate train, start gates and running input lock; confirmation visible for review";
 }
 public static string PointerFrame(){UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>().StartCoroutine(PointerCheck());return "Frame raycast scheduled";}
 static IEnumerator PointerCheck(){yield return new WaitForEndOfFrame();var h=UnityEngine.Object.FindFirstObjectByType<RailwayHud>();var b=h.DraftRows[0];var rt=(RectTransform)b.transform;var point=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center));var hits=new List<RaycastResult>();var data=new PointerEventData(EventSystem.current){position=point};h.GetComponentInParent<UnityEngine.UI.GraphicRaycaster>().Raycast(data,hits);bool ok=hits.Count>0&&hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Button>()==b;File.WriteAllText("/tmp/eternal-a3-order-pointer-frame.json",JsonUtility.ToJson(new Result{acceptsPointer=ok,width=Screen.width,height=Screen.height,point=point.ToString(),hits=string.Join(",",hits.Select(x=>x.gameObject.name))},true));}
 [Serializable] public class Result{public bool acceptsPointer;public int width,height;public string point,hits;}
}
