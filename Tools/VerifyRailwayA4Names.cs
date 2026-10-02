using System;
using System.IO;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.Railway;
using EternalSteam.OpenWorld;
public static class VerifyRailwayA4Names
{
 const string Root="/tmp/eternal-railway-a4-names";
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static OpenWorldSandbox World(){Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")==Root,"Isolated Play required");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(s.GetComponent<OpenWorldInput>().Edits!=null,"Wait for scene Start");s.Persistence.Automatic=false;s.Clock.Paused=true;return s;}
 static void Click(RailwayHud h,string cmd){h.SendMessage("Update");var b=h.Commands.First(x=>x.Command==cmd&&x.Button.gameObject.activeInHierarchy).Button;Check(b.interactable,"Disabled: "+cmd);b.onClick.Invoke();}
 public static string LoadFixture(){var s=World();var payload=File.ReadAllText("/tmp/eternal-railway-a1-after.json");var snapshot=JsonUtility.FromJson<SingleMapSnapshot>(payload);new JsonSaveStore(Root).Write(snapshot.runId,payload);s.Persistence.ContinueSaved();return "PASS isolated fixture reload requested";}
 public static string Main()
 {
  var s=World();var n=s.Content.Railway.Network;var labels=n.Capture().labels;
  Check(labels.Count>=2,"Fixture stations");var names=labels.Select(l=>n.StationName(l.stationId)).ToArray();Check(names.Distinct().Count()==names.Length,"Unique names");
  File.WriteAllText(Root+"/names.json",JsonUtility.ToJson(n.Capture()));
  Check(s.Persistence.Save(),s.Persistence.Status);s.Persistence.ContinueSaved();return "PASS legacy fixture names unique; saved and requested reload: "+string.Join(", ",names);
 }
 public static string AfterReload()
 {
  var s=World();var n=s.Content.Railway.Network;var expected=JsonUtility.FromJson<RailwayNetwork.Snapshot>(File.ReadAllText(Root+"/names.json"));var actual=n.Capture();
  Check(JsonUtility.ToJson(expected)==JsonUtility.ToJson(actual),"Exact railway registry and route reload");
  var h=s.RailwayHud;h.Execute("open");h.RowButtons[0].onClick.Invoke();Click(h,"edit");
  foreach(var stop in n.Routes.Single().stops)Check(h.Summary.text.Contains(n.StationName(stop.stationId)),"HUD name");
  Click(h,"cancel");return "PASS exact names/routes reload and HUD names";
 }
}
