using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.Railway;
using EternalSteam.OpenWorld;
using Newtonsoft.Json.Linq;
public static class VerifyIntegratedTrainPlay
{
 const string Root="Docs/Measurements/2026-10-02-train-hud-integration/";
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 static OpenWorldSandbox World(){Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")==Path.GetFullPath(Root+"play-save"),"Isolated Play required");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();s.Persistence.Automatic=false;s.Clock.Paused=true;Check(!s.Persistence.Blocked,s.Persistence.Status);return s;}
 static void Record(string key,object value){string path=Root+"play-measurements.json";var j=File.Exists(path)?JObject.Parse(File.ReadAllText(path)):new JObject();j[key]=JToken.FromObject(value);File.WriteAllText(path,j.ToString());}
 static void Save(OpenWorldSandbox s,string stage){Check(s.Persistence.Save(),s.Persistence.Status);File.WriteAllText(Root+stage+"-snapshot.json",JsonUtility.ToJson(s.Persistence.Capture()));}
 static void ReloadExact(OpenWorldSandbox s,string stage){var before=JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText(Root+stage+"-snapshot.json"));var after=s.Persistence.Capture();before.savedUtc=after.savedUtc;Check(JsonUtility.ToJson(before)==JsonUtility.ToJson(after),"Exact reload "+stage);}
 public static string Build(){
  var s=World();var c=s.Content;var h=s.RailwayHud;var input=s.GetComponent<OpenWorldInput>();input.Cancel();Check(c.GroundWorld.Buildings.Count==1,"Fresh fixture");
  string main=c.MainBase.Module<IBaseIdentity>().BaseId;var bank=c.Inventories.Available(main);bank.Deposit("iron",1000);bank.Deposit("coal",100);Check(!bank.InfiniteResources,"Finite stock");
  var subDef=s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.nexus");BuildingInstance sub=null;
  for(int z=20;z>=12&&sub==null;z--)for(int x=-5;x<10&&sub==null;x++)if(c.GroundPlacement.Add(subDef,new Vector2Int(x,z),out _).Success){var result=c.GroundPlacement.Confirm();Check(result.Success,result.Message);sub=c.GroundWorld.Buildings.Last();}
  Check(sub!=null,"Sub-base coverage fixture");c.Bases.Refresh();string secondary=sub.Module<IBaseIdentity>().BaseId;c.Inventories.Available(secondary);
  Vector2Int? origin=null;for(int y=8;y<30&&!origin.HasValue;y++)for(int x=-15;x<15&&!origin.HasValue;x++){
   var o=new Vector2Int(x,y);if(!new[]{o,o+new Vector2Int(6,0)}.All(cell=>c.HasBuildArea(c.GroundWorld.Grid.Center(cell,h.Station.Footprint),h.Station.Footprint)))continue;if(!c.GroundPlacement.Validate(new PlacementRequest(-1,h.Station,o)).Success||!c.GroundPlacement.Validate(new PlacementRequest(-1,h.Station,o+new Vector2Int(6,0))).Success)continue;
   if(new[]{new Vector2Int(2,1),new Vector2Int(3,1),new Vector2Int(4,1),new Vector2Int(5,1),new Vector2Int(5,0)}.All(p=>c.GroundPlacement.Validate(new PlacementRequest(-1,h.Track,o+p)).Success))origin=o;
  }
  Check(origin.HasValue,"Terrain-valid station pair");
  BuildingInstance Install(BuildingDefinition def,Vector2Int cell){c.Bases.Select(main);input.BeginEditing();Check(input.Edits.AddContent(def,c.GroundWorld.Grid.Center(cell,Vector2Int.one),WorldGridGeometry.Rotation*Vector3.forward,out var e),e);var r=input.Edits.Confirm();Check(r.Success,r.Message);input.Cancel();c.Railway.Refresh();return c.GroundWorld.Buildings.Single(b=>b.Cell==cell);}
  var a=Install(h.Station,origin.Value);var b=Install(h.Station,origin.Value+new Vector2Int(6,0));Install(h.Track,origin.Value+new Vector2Int(3,1));
  void Press(string cmd){h.Commands.Single(v=>v.Command==cmd).Button.onClick.Invoke();}
  Check(h.OpenFromStation(a.PersistentId),"Open station");Press("connect");Check(h.HasDraft&&h.ConnectionPanel.activeSelf,"Connection UI entry");Check(input.ClickWorld(b.Position),"World endpoint click");
  Press("connection-preview");Check(h.CanExecute("connection-confirm",out var why),why);
  Check(c.Railway.Network.Connections.Count==0&&c.GroundWorld.Grid.ReservationCount==0,"Preview has no mutation");
  Record("preview",new{summary=h.Summary.text,origin=origin.Value,fixtureIronInjection=1000,fixtureCoalInjection=100});
  int count=c.GroundWorld.Buildings.Count;double stock=bank.Amount("iron");bank.Withdraw("iron",stock);Press("connection-confirm");Check(c.GroundWorld.Buildings.Count==count&&c.Railway.Network.Connections.Count==0&&c.GroundWorld.Grid.ReservationCount==0,"Insufficient stock leaves world unchanged");bank.Deposit("iron",stock);
  var service=new RailwayConstruction(c,input.Edits,h.Track);var plan=service.Preview(a.PersistentId,b.PersistentId);Check(plan.Status==RailPlanStatus.Found,"Service preview");
  Check(c.Bases.Select(secondary),"Select second payer");Check(!service.Confirm(out why),"Changed payer rejects original quote");Check(c.Railway.Network.Connections.Count==0&&bank.Amount("iron")==stock,"Payer rejection unchanged");Check(c.Bases.Select(main),"Restore payer");
  var changed=plan.Connection.cells.First(cell=>!c.Railway.Network.HasTrack(cell));Install(h.Track,changed);
  Check(!service.Confirm(out why),"Stale preview must reject a newly occupied cell");Check(c.Railway.Network.Connections.Count==0,"Stale preview leaves graph unchanged");
  // Reopen after the ordinary build interaction closed the panel, then exercise the saved callbacks again.
  Press("connection-cancel");Check(h.OpenFromStation(a.PersistentId),"Reopen");Press("connect");input.ClickWorld(b.Position);Press("connection-preview");Check(h.CanExecute("connection-confirm",out why),why);
  stock=bank.Amount("iron");int oldTracks=c.GroundWorld.Buildings.Count(v=>v.Module<RailFacility>()?.Kind==RailFacilityKind.Track);Press("connection-confirm");
  Check(c.Railway.Network.Connections.Count==1&&!h.HasDraft,"Explicit link committed");var link=c.Railway.Network.Connections[0];int newTracks=c.GroundWorld.Buildings.Count(v=>v.Module<RailFacility>()?.Kind==RailFacilityKind.Track)-oldTracks;
  Check(stock-bank.Amount("iron")==newTracks,"Only newly installed rails charged");Check(c.Railway.Network.Routes.Count==0,"Link does not create a train");Check(c.GroundWorld.Grid.ReservationCount==0,"No leftover reservations");
  double paidStock=bank.Amount("iron");Press("connection-confirm");Check(bank.Amount("iron")==paidStock&&c.Railway.Network.Connections.Count==1,"Repeated UI confirm cannot double charge");
  Record("commit",new{newTracks,reusedTracks=link.cells.Count-newTracks,cells=link.cells.Count,ironSpent=stock-bank.Amount("iron"),insufficientRejected=true,staleRejected=true,payerChangeRejected=true,repeatUnchanged=true,persistentCallbacksInvoked=true});
  Save(s,"unassigned-link");return "PASS preview, finite cost, reuse, stale/insufficient rejection and unassigned connection save; reload next";
 }
 public static string PrepareReload(){var s=World();Save(s,"unassigned-link");return "Canonical fixture ledgers saved";}
 public static string ReloadAndRun(){
  var s=World();ReloadExact(s,"unassigned-link");var n=s.Content.Railway.Network;var c=n.Connections.Single();Check(n.Routes.Count==0,"Unassigned link reload");
  Check(n.Commit(Guid.NewGuid().ToString("N"),new[]{new RailStop{stationId=c.fromStation,load=10},new RailStop{stationId=c.toStation,unload=10}},out var route,out var e,RailRouteMode.Shuttle),e);
  Check(n.Configure(route,"iron",0,10,0,out e),e);Check(n.Refuel(route,50,out e),e);Check(n.Start(route,out e),e);n.Tick(20+c.cells.Count+.5);Check(route.train.reverse,"Return journey");
  Record("reload",new{unassignedExact=true,routeCreated=true,reverse=route.train.reverse,progress=route.train.progress});Save(s,"running-link");return "PASS unassigned connection reload, route creation and return journey; reload next";
 }
 public static string ReloadRunning(){var s=World();ReloadExact(s,"running-link");var n=s.Content.Railway.Network;Check(n.ValidateExisting(n.Routes.Single(),out var e),e);Record("runningReload",new{exact=true,graph=n.Connections.Count,tracks=n.Capture().trackRecords.Count});return "PASS exact running graph/train/inventory reload";}
}
