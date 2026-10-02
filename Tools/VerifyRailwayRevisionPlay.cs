using System;
using System.IO;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;
public static class VerifyRailwayRevisionPlay
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static string Main(){
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(Application.isPlaying,"Play required");Check(Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")?.StartsWith("/tmp/eternal-railway-p5-")==true,"Isolated P5 save root required");s.Persistence.Automatic=false;s.Clock.Paused=true;Check(!s.Persistence.Blocked,s.Persistence.Status);
  var n=s.Content.Railway.Network;var r=n.Routes.Single();Check(r.train.status==TrainStatus.Stopped&&r.train.stop==1,"Use two-base stopped fixture");Check(n.Start(r,out var error),error);s.Content.Railway.Tick(11);Check(r.train.segmentPaid,"Moving");
  var h=s.RailwayHud;h.Execute("open");h.RowButtons[0].onClick.Invoke();h.Execute("edit");
  for(int i=0;i<2;i++){h.Execute("arrival");for(int j=0;j<3;j++)h.Execute("departure");h.Execute("next-stop");}
  h.Execute("validate");h.Execute("commit");Check(n.HasPending(r)&&!h.HasDraft,h.Feedback.text);Check(r.stops[0].departure==1&&r.train.progress==1,"Active route untouched");Check(h.CancelPendingButton!=null&&h.CancelPendingButton.interactable,"Authored cancel button enabled");
  Check(s.Persistence.Save(),s.Persistence.Status);File.WriteAllText("/tmp/eternal-railway-p5-before.json",JsonUtility.ToJson(s.Persistence.Capture()));return "PASS live moving edit via HUD, deferred active route, saved pending revision and cancel callback reference";
 }
 public static string Reloaded(){
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();s.Persistence.Automatic=false;Check(!s.Persistence.Blocked,s.Persistence.Status);var before=JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText("/tmp/eternal-railway-p5-before.json"));var after=s.Persistence.Capture();before.savedUtc=after.savedUtc;Check(JsonUtility.ToJson(before)==JsonUtility.ToJson(after),"Exact pending snapshot restore");
  var n=s.Content.Railway.Network;var r=n.Routes.Single();string train=r.trainId;double fuel=r.train.fuel;Check(n.HasPending(r),"Pending restored");n.Stop(r);s.Content.Railway.Tick(r.legs[r.train.stop].cells.Count-r.train.progress);
  Check(r.revision==1&&!n.HasPending(r)&&r.stops[0].departure==0,"Applied at original first station");Check(r.train.status==TrainStatus.Stopped&&r.train.stop==0&&r.trainId==train&&r.train.fuel==fuel,"Stop and train/fuel preserved");
  Check(n.Start(r,out var error),error);s.Content.Railway.Tick(11);var h=s.RailwayHud;h.Execute("open");h.RowButtons[0].onClick.Invoke();h.Execute("edit");h.Execute("validate");h.Execute("commit");Check(n.HasPending(r),"Second pending");h.CancelPendingButton.onClick.Invoke();Check(!n.HasPending(r)&&r.revision==1&&r.train.segmentPaid,"Real cancellation callback keeps active train");
  h.Execute("edit");h.Execute("validate");h.Execute("commit");return "PASS exact pending scene reload, first-station application, stop priority, train/fuel preservation, cancellation callback; pending UI ready for capture";
 }
}
