using System;
using System.IO;
using System.Linq;
using UnityEngine;
using EternalSteam;

using EternalSteam.Railway;
using EternalSteam.OpenWorld;
public static class VerifyRailwayG12
{
 const string Root="/tmp/eternal-railway-g12";
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static OpenWorldSandbox World(){Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")?.StartsWith(Root)==true,"Isolated Play");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(s.GetComponent<OpenWorldInput>().Edits!=null,"Wait Start");s.Persistence.Automatic=false;s.Clock.Paused=true;return s;}
 public static string LoadFixture(){var s=World();var json=File.ReadAllText("/tmp/eternal-railway-a1-after.json");var snap=JsonUtility.FromJson<SingleMapSnapshot>(json);new JsonSaveStore(Root).Write(snap.runId,json);s.Persistence.ContinueSaved();return "load requested";}
 public static string Main(){var s=World();var h=s.RailwayHud;s.Content.Railway.Refresh();var n=s.Content.Railway.Network;RailRoute r=n.Routes.FirstOrDefault();if(r==null){
   var stations=n.Stations.ToArray();Check(stations.Length==2,"Two restored stations");
   for(int ai=0;ai<4&&r==null;ai++)for(int ao=0;ao<4&&r==null;ao++)for(int bi=0;bi<4&&r==null;bi++)for(int bo=0;bo<4&&r==null;bo++){
    var stops=new[]{new RailStop{stationId=stations[0].PersistentId,arrival=ai,departure=ao},new RailStop{stationId=stations[1].PersistentId,arrival=bi,departure=bo}};
    if(n.ValidateDraft(stops,null,out _,out _))n.Commit(Guid.NewGuid().ToString("N"),stops,out r,out _);
   }
   Check(r!=null,"Rebuild saved station/track route");
  }var second=r.stops[1].stationId;Check(h.OpenFromStation(second),"Station entry");Check(h.ContextStationId==second&&h.Title.text.Contains(s.Content.Railway.Network.StationName(second)),"Context title");h.Execute("row:0");Check(h.SelectedStopIndex==1&&h.Title.text.Contains(s.Content.Railway.Network.StationName(second)),"Route row retains stop");Check(h.StationContextHeader.text.Contains(s.Content.Railway.Network.StationName(second)),"Detail header");h.Execute("edit");Check(h.SelectedDraftStationId==second,"Draft keeps stop");h.Execute("cancel");h.Execute("station-row:0");Check(h.SelectedStopIndex==0,"Station row selects first");h.Execute("station-context");Check(h.ContextStationId==r.stops[0].stationId,"Name click opens station");h.Execute("list");h.Execute("open");Check(h.ContextStationId==null,"Global list clears context");Check(h.OpenFromStation(second),"Reopen station");h.Execute("new");Check(h.SelectedDraftStationId==second&&h.HasDraft,"New route begins at station");h.Execute("cancel");return "PASS context title/detail/edit, clickable names, list return, global list, new route starts at chosen station";}
}
