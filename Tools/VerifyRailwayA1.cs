using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;
public static class VerifyRailwayA1
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static string Prepare(){
 var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(Application.isPlaying,"Play required");Check(Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")?.StartsWith("/tmp/eternal-")==true,"Isolated save root required");s.Persistence.Automatic=false;s.Clock.Paused=true;Check(!s.Persistence.Blocked,s.Persistence.Status);
 var c=s.Content;var input=s.GetComponent<OpenWorldInput>();input.Cancel();Check(c.GroundWorld.Buildings.Count==1,"Fresh fixture required");
 var mainId=c.MainBase.Module<IBaseIdentity>().BaseId;
 var subDef=s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.nexus");BuildingInstance sub=null;
 for(int z=20;z>=12&&sub==null;z--)for(int x=-5;x<10&&sub==null;x++)if(c.GroundPlacement.Add(subDef,new Vector2Int(x,z),out _).Success){var result=c.GroundPlacement.Confirm();Check(result.Success,result.Message);sub=c.GroundWorld.Buildings.Last();}
 Check(sub!=null,"Sub base placement");var subId=sub.Module<IBaseIdentity>().BaseId;c.Bases.Refresh();c.Bases.Select(mainId);
 var aBank=c.Inventories.Available(mainId);var bBank=c.Inventories.Available(subId);aBank.Deposit("iron",500);aBank.Deposit("coal",100);bBank.Deposit("iron",100);
 var station=s.RailwayHud.Station;var track=s.RailwayHud.Track;
 var relative=new List<Vector2Int>();Vector2Int second=default;Vector2Int? offset=null;int rotation=0,apIn=0,apOut=1,bpIn=0,bpOut=1;
 var candidates=new List<Vector2Int>();var available=new HashSet<Vector2Int>();var directions=new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};var ports=new[]{new Vector2Int(-1,0),new Vector2Int(2,1),new Vector2Int(1,-1),new Vector2Int(0,2)};
 for(int z=-32;z<40;z++)for(int x=-32;x<40;x++){var cell=new Vector2Int(x,z);if(c.GroundPlacement.Validate(new PlacementRequest(-1,station,cell)).Success&&c.Bases.Bases.Values.Any(baseContext=>baseContext.Nexus.Module<IBuildArea>()?.Contains(c.GroundWorld.Grid.Center(cell,station.Footprint),(Vector2)station.Footprint,WorldGridGeometry.Rotation)==true))candidates.Add(cell);if(c.GroundPlacement.Validate(new PlacementRequest(-2,track,cell)).Success)available.Add(cell);}
 List<Vector2Int> Path(Vector2Int start,Vector2Int end,HashSet<Vector2Int> blocked){if(!available.Contains(start)||!available.Contains(end)||blocked.Contains(start)||blocked.Contains(end))return null;var queue=new Queue<Vector2Int>();var prev=new Dictionary<Vector2Int,Vector2Int>();queue.Enqueue(start);prev[start]=start;while(queue.Count>0){var cell=queue.Dequeue();if(cell==end){var path=new List<Vector2Int>{end};while(cell!=start){cell=prev[cell];path.Add(cell);}return path;}foreach(var dir in directions){var next=cell+dir;if(!available.Contains(next)||blocked.Contains(next)||prev.ContainsKey(next))continue;prev[next]=cell;queue.Enqueue(next);}}return null;}
 foreach(var ca in candidates){if(offset.HasValue)break;foreach(var cb in candidates){if(offset.HasValue)break;if(Math.Abs(ca.x-cb.x)<2&&Math.Abs(ca.y-cb.y)<2)continue;var occupied=new HashSet<Vector2Int>();foreach(var anchor in new[]{ca,cb})for(int dx=0;dx<2;dx++)for(int dy=0;dy<2;dy++)occupied.Add(anchor+new Vector2Int(dx,dy));
 for(int ao=0;ao<4&&!offset.HasValue;ao++)for(int bi=0;bi<4&&!offset.HasValue;bi++){var first=Path(ca+ports[ao],cb+ports[bi],occupied);if(first==null)continue;var excluded=new HashSet<Vector2Int>(occupied);foreach(var cell in first){excluded.Add(cell);foreach(var dir in directions)excluded.Add(cell+dir);}for(int bo=0;bo<4&&!offset.HasValue;bo++)for(int ai=0;ai<4&&!offset.HasValue;ai++){if(bo==bi||ai==ao)continue;var back=Path(cb+ports[bo],ca+ports[ai],excluded);if(back==null)continue;offset=ca;second=cb-ca;relative=first.Concat(back).Select(cell=>cell-ca).ToList();apIn=ai;apOut=ao;bpIn=bi;bpOut=bo;}}
 }}
 Check(offset.HasValue,"Find terrain-valid independent outward/return paths");var origin=offset.Value;
 BuildingInstance Install(BuildingDefinition def,Vector2Int cell,string owner){c.Bases.Select(owner);input.BeginEditing();var ledger=c.Inventories.Available(owner);Check(ledger!=null,"Owner ledger unavailable: "+owner);Check(input.Edits!=null,"WorldEditSession missing");double before=ledger.Amount("iron");Check(input.Edits.AddContent(def,c.GroundWorld.Grid.Center(cell,Vector2Int.one),WorldGridGeometry.Rotation*Quaternion.Euler(0,rotation*90,0)*Vector3.forward,out var why),why);Check(ledger.Amount("iron")==before,"No reservation charge");var result=input.Edits.Confirm();Check(result.Success,result.Message);Check(ledger.Amount("iron")==before-(def==station?10:1),"Owner charged once");input.Cancel();return c.GroundWorld.Buildings.Last();}
 var a=Install(station,origin,mainId);var b=Install(station,origin+second,subId);foreach(var p in relative)Install(track,p+origin,mainId);
 c.Railway.Refresh();var n=c.Railway.Network;var stops=new[]{new RailStop{stationId=a.PersistentId,arrival=apIn,departure=apOut,load=100},new RailStop{stationId=b.PersistentId,arrival=bpIn,departure=bpOut,unload=100}};
 Check(n.Commit(Guid.NewGuid().ToString("N"),stops,out var r,out var error),error);Check(!r.train.HasCargoResource,"Fresh route has no cargo resource");
 s.Clock.SetPhase(DayPhase.Day);s.Enemies.Reset();Check(s.Persistence.Save(),s.Persistence.Status);File.WriteAllText("/tmp/eternal-railway-a1-before.json",JsonUtility.ToJson(s.Persistence.Capture()));s.Persistence.ContinueSaved();
 return "PASS real railway construction; unconfigured route saved through SingleMapPersistence; scene reload requested";
 }
 public static string ReloadedAndRecovered()
 {
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(Application.isPlaying,"Play required");Check(Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")=="/tmp/eternal-railway-a1","Isolated root");s.Persistence.Automatic=false;s.Clock.Paused=true;Check(!s.Persistence.Blocked,s.Persistence.Status);
  var before=JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText("/tmp/eternal-railway-a1-before.json"));var after=s.Persistence.Capture();before.savedUtc=after.savedUtc;Check(JsonUtility.ToJson(before)==JsonUtility.ToJson(after),"Unconfigured route exact reload");
  var c=s.Content;var n=c.Railway.Network;var r=n.Routes.Single();Check(!r.train.HasCargoResource,"Still unconfigured");var hud=s.RailwayHud;hud.Execute("open");hud.Execute("row:0");Check(hud.Summary.text.Contains("미지정"),"Unconfigured HUD label");
  hud.Resource.text="iron";hud.Load.text="100";hud.Unload.text="0";hud.Execute("configure");Check(r.train.resource=="iron"&&r.stops[0].load==100,"First resource accepted through HUD after reload");
  hud.Resource.text="coal";hud.Execute("configure");Check(r.train.resource=="iron","Assigned resource cannot be changed");hud.Resource.text="iron";
  hud.Execute("next-stop");hud.Load.text="0";hud.Unload.text="0";hud.Execute("configure");Check(r.stops[1].unload==0,"Keep cargo for recovery checks");
  Check(n.Refuel(r,50,out var error),error);hud.Execute("start");c.Railway.Tick(11.25);Check(r.train.segmentPaid,"Moving fixture");n.Stop(r);c.Railway.Tick(30);Check(r.train.status==TrainStatus.Stopped&&r.train.stop==1&&r.train.cargo==100,"Stopped with cargo");
  double fuel=r.train.fuel,cargo=r.train.cargo;var input=s.GetComponent<OpenWorldInput>();hud.Execute("close");
  var used=r.legs.SelectMany(l=>l.cells).ToHashSet();var tracks=c.GroundWorld.Buildings.Where(b=>b.Module<RailFacility>()?.Kind==RailFacilityKind.Track&&used.Contains(b.Cell)).Take(3).ToArray();Check(tracks.Length==3,"Recovery fixture");var cells=tracks.Select(b=>b.Cell).ToArray();var owner=tracks[0].OwnerBaseId;
  input.BeginEditing();Check(input.Edits.ToggleGroundRecovery(tracks[0],out error),error);Check(input.Confirm(),s.Message);input.Cancel();c.Railway.Refresh();Check(r.train.status==TrainStatus.RouteError,"Broken route enters error");
  input.BeginEditing();var occupied=n.Station(r.stops[r.train.stop].stationId);Check(input.Edits.ToggleGroundRecovery(occupied,out error),error);Check(input.Edits.ToggleGroundRecovery(tracks[1],out error),error);Check(!input.Confirm()&&!occupied.Disposed&&!tracks[1].Disposed,"Occupied station blocks entire recovery commit");input.Cancel();input.BeginEditing();
  Check(input.Edits.ToggleGroundRecovery(tracks[1],out error),error);Check(input.Edits.ToggleGroundRecovery(tracks[2],out error),error);Check(input.Edits.RecoveryCount==2,"Multi-selection in error state");Check(input.Confirm(),s.Message);input.Cancel();c.Railway.Refresh();Check(tracks.All(b=>b.Disposed),"Three tracks recovered");Check(r.train.cargo==cargo&&r.train.fuel==fuel,"Recovery preserves cargo/fuel");
  c.Bases.Select(owner);input.BeginEditing();foreach(var cell in cells)Check(input.Edits.AddContent(hud.Track,c.GroundWorld.Grid.Center(cell,Vector2Int.one),WorldGridGeometry.Rotation*Vector3.forward,out error),error);Check(input.Confirm(),s.Message);input.Cancel();c.Railway.Refresh();Check(r.train.cargo==cargo&&r.train.fuel==fuel,"Repair preserves cargo/fuel");
  hud.Execute("open");hud.Execute("row:0");hud.Execute("start");Check(r.train.status==TrainStatus.Dwelling,"Manual HUD resume");Check(r.train.cargo==cargo&&r.train.fuel==fuel,"Resume does not repeat transfer or fuel debit");c.Railway.Tick(10.1);Check(r.train.segmentPaid,"Resumed moving");
  var protectedTrack=c.GroundWorld.Buildings.First(b=>b.Module<RailFacility>()?.Kind==RailFacilityKind.Track&&r.legs.Any(l=>l.cells.Contains(b.Cell)));input.BeginEditing();Check(input.Edits.ToggleGroundRecovery(protectedTrack,out error),error);Check(!input.Confirm()&&!protectedTrack.Disposed,"Moving segment recovery commit protected");input.Cancel();n.Stop(r);c.Railway.Tick(60);Check(r.train.status==TrainStatus.Stopped,"Return to station");
  Check(s.Persistence.Save(),s.Persistence.Status);File.WriteAllText("/tmp/eternal-railway-a1-after.json",JsonUtility.ToJson(s.Persistence.Capture()));s.Persistence.ContinueSaved();return "PASS unconfigured exact reload and HUD first resource lock; error multi-recovery, occupied station protection, repair/manual resume/cargo/fuel preservation, moving track protection; final reload requested";
 }
 public static string FinalReload()
 {
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")=="/tmp/eternal-railway-a1","Isolated root");s.Persistence.Automatic=false;s.Clock.Paused=true;Check(!s.Persistence.Blocked,s.Persistence.Status);var expected=JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText("/tmp/eternal-railway-a1-after.json"));var actual=s.Persistence.Capture();expected.savedUtc=actual.savedUtc;Check(JsonUtility.ToJson(expected)==JsonUtility.ToJson(actual),"Recovered railway exact final reload");return "PASS repaired route/train/cargo/fuel/base inventories exact actual scene reload";
 }
}
