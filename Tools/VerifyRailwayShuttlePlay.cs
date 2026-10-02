using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;

public static class VerifyRailwayShuttlePlay
{
    const string Before="/tmp/eternal-rx02-return-before.json";
    const string Report="Docs/Validation/2026-10-02-railway-shuttle-play.json";
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static OpenWorldSandbox Sandbox(){var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(Application.isPlaying,"Play required");Check(Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")?.StartsWith("/tmp/eternal-rx02-")==true,"Isolated saves required");s.Persistence.Automatic=false;s.Clock.Paused=true;return s;}
    public static string BuildAndRun()
    {
        var s=Sandbox();Check(!s.Persistence.Blocked,s.Persistence.Status);var c=s.Content;var input=s.GetComponent<OpenWorldInput>();input.Cancel();
        Check(c.GroundWorld.Buildings.Count==1,"Fresh fixture required");string main=c.MainBase.Module<IBaseIdentity>().BaseId;
        var subDef=s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.nexus");BuildingInstance sub=null;
        for(int z=20;z>=12&&sub==null;z--)for(int x=-5;x<10&&sub==null;x++)if(c.GroundPlacement.Add(subDef,new Vector2Int(x,z),out _).Success){var result=c.GroundPlacement.Confirm();Check(result.Success,result.Message);sub=c.GroundWorld.Buildings.Last();}
        Check(sub!=null,"Sub placement");string secondary=sub.Module<IBaseIdentity>().BaseId;c.Bases.Refresh();
        var aBank=c.Inventories.Available(main);var bBank=c.Inventories.Available(secondary);aBank.Deposit("iron",500);aBank.Deposit("coal",100);bBank.Deposit("iron",100);
        var station=s.RailwayHud.Station;var track=s.RailwayHud.Track;
        var cells=new[]{new Vector2Int(2,1),new Vector2Int(3,1),new Vector2Int(4,1),new Vector2Int(5,1),new Vector2Int(5,0)};
        var legacyReturn=new List<Vector2Int>();for(int y=1;y<=4;y++)legacyReturn.Add(new Vector2Int(8,y));for(int x=7;x>=-1;x--)legacyReturn.Add(new Vector2Int(x,4));for(int y=3;y>=0;y--)legacyReturn.Add(new Vector2Int(-1,y));
        Vector2Int? origin=null;
        for(int z=12;z<26&&!origin.HasValue;z++)for(int x=-5;x<8&&!origin.HasValue;x++){
            var o=new Vector2Int(x,z);if(!c.HasBuildArea(c.GroundWorld.Grid.Center(o,station.Footprint),station.Footprint)||!c.HasBuildArea(c.GroundWorld.Grid.Center(o+new Vector2Int(6,0),station.Footprint),station.Footprint))continue;if(!c.GroundPlacement.Validate(new PlacementRequest(-1,station,o)).Success||!c.GroundPlacement.Validate(new PlacementRequest(-2,station,o+new Vector2Int(6,0))).Success)continue;
            if(legacyReturn.All(p=>c.GroundPlacement.Validate(new PlacementRequest(-4,track,p+o)).Success)&&cells.All(p=>c.GroundPlacement.Validate(new PlacementRequest(-3,track,p+o)).Success))origin=o;
        }
        Check(origin.HasValue,"Terrain-valid single connection fixture");
        BuildingInstance Install(BuildingDefinition def,Vector2Int cell,string owner){c.Bases.Select(owner);input.BeginEditing();double before=c.Inventories.Available(owner).Amount("iron");Check(input.Edits.AddContent(def,c.GroundWorld.Grid.Center(cell,Vector2Int.one),WorldGridGeometry.Rotation*Vector3.forward,out var why),why);var result=input.Edits.Confirm();Check(result.Success,result.Message);Check(c.Inventories.Available(owner).Amount("iron")==before-(def==station?10:1),"Charge once per actual new building");input.Cancel();return c.GroundWorld.Buildings.Last();}
        var a=Install(station,origin.Value,main);var b=Install(station,origin.Value+new Vector2Int(6,0),secondary);foreach(var p in cells)Install(track,p+origin.Value,main);
        c.Railway.Refresh();var n=c.Railway.Network;var hud=s.RailwayHud;
        Check(n.StationActive(a.PersistentId)&&n.StationActive(b.PersistentId),"Active station fixture");
        Check(hud.OpenFromStation(a.PersistentId),"Open station context");hud.Execute("new");Check(input.ClickWorld(b.Position),"Select destination through world input");hud.Execute("add");
        Check(hud.CanExecute("validate",out _),"No explicit close-loop needed");Check(!hud.CanExecute("arrival",out _)&&!hud.CanExecute("departure",out _),"No manual in/out ports");hud.Execute("validate");Check(hud.Stage==RailwayHud.EditorStage.Confirming,"Shuttle confirmation");hud.Execute("commit");
        var r=n.Routes.Single();Check(r.IsShuttle&&r.legs.Count==1&&r.legs[0].cells.Count==5,"One physical connection only");
        hud.Resource.SetTextWithoutNotify("iron");hud.Load.SetTextWithoutNotify("30");hud.Unload.SetTextWithoutNotify("0");hud.Execute("configure");
        Check(n.Configure(r,"iron",0,30,0,out var error),error);Check(n.Configure(r,"iron",1,0,30,out error),error);
        Check(n.Refuel(r,50,out error),error);double stockA=aBank.Amount("iron"),stockB=bBank.Amount("iron");hud.Execute("start");Check(r.train.status==TrainStatus.Dwelling,"HUD start");
        c.Railway.Tick(90);Check(r.train.stop==0&&!r.train.reverse&&r.train.cargo==30,"Three complete round trips");Check(bBank.Amount("iron")==stockB+90,"Three deliveries");
        Check(aBank.Amount("iron")+bBank.Amount("iron")+r.train.cargo==stockA+stockB,"Cargo conserved");Check(Math.Abs(r.train.fuel-(50-6*n.FuelCost(r.legs[0])))<1e-8,"One fuel charge per departure");
        c.Railway.Tick(26.25);Check(r.train.reverse&&Math.Abs(r.train.progress-1.25)<1e-8,"Return leg moving");
        var pose=c.Railway.Network.Routes.Single();Check(float.IsFinite(s.RailwayView.Pose(pose,c).position.x),"Return pose available");
        s.Clock.SetPhase(DayPhase.Day);s.Enemies.Reset();Check(s.Persistence.Save(),s.Persistence.Status);var before=s.Persistence.Capture();Check(before.version==3,"v3 container");File.WriteAllText(Before,JsonUtility.ToJson(before));
        File.WriteAllText(Report,Newtonsoft.Json.JsonConvert.SerializeObject(new {date="2026-10-02",scope="Isolated actual Unity Play; production/balance bypassed for fixture; normal railway Tick, HUD/world command callbacks, not physical mouse input",physicalTracks=5,connections=1,roundTrips=3,delivered=90,containerVersion=3,returnMovingSaved=true,hudNoManualPorts=true,hudNoCycleClose=true,importAndPlay=true,reload="pending"},Newtonsoft.Json.Formatting.Indented));
        return "PASS saved Canvas HUD creates shuttle without port/cycle commands; 5 paid tracks; three round trips; cargo/fuel; moving return save. Reload next.";
    }
    public static string ReloadReturn()
    {
        var s=Sandbox();Check(!s.Persistence.Blocked,s.Persistence.Status);var before=JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText(Before));var after=s.Persistence.Capture();before.savedUtc=after.savedUtc;
        Check(JsonUtility.ToJson(before)==JsonUtility.ToJson(after),"Exact return-moving save restore");var n=s.Content.Railway.Network;var r=n.Routes.Single();double fuel=r.train.fuel;
        n.Stop(r);s.Content.Railway.Tick(3.75);Check(r.train.status==TrainStatus.Stopped&&r.train.stop==0&&!r.train.reverse,"Stop at next station on return");Check(r.train.fuel==fuel,"No recharge of paid segment");
        Check(s.Persistence.Save(),s.Persistence.Status);File.WriteAllText(Before,JsonUtility.ToJson(s.Persistence.Capture()));
        var report=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Report));report["returnMovingReloadExact"]=true;report["stopRequestReturnsToFirstStation"]=true;File.WriteAllText(Report,report.ToString());return "PASS exact return-moving v3 reload and stop at first station; stopped save ready.";
    }
    public static string ReloadStopped()
    {
        var s=Sandbox();var before=JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText(Before));var after=s.Persistence.Capture();before.savedUtc=after.savedUtc;Check(JsonUtility.ToJson(before)==JsonUtility.ToJson(after),"Stopped save exact restore");
        var n=s.Content.Railway.Network;var r=n.Routes.Single();double cargo=r.train.cargo;Check(n.Start(r,out var e),e);Check(r.train.cargo==cargo,"No duplicate loading after restore");n.Stop(r);
        var report=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Report));report["stoppedReloadExact"]=true;report["noDuplicateServiceAfterReload"]=true;report["reload"]="passed";File.WriteAllText(Report,report.ToString());return "PASS stopped save exact restore and no duplicate loading. Isolated fixture complete.";
    }
}
