using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;
using Newtonsoft.Json.Linq;

// Isolated fixture; real scene factories, costs, defense, Tick and save/reload. No physical pointer claims.
public static class VerifyRailwayStationSlotsPlay
{
    const string Root="Docs/Measurements/2026-10-02-railway-station-slots/";
    const string Before=Root+"play-before-reload.json";
    const string Report=Root+"play-final-measurements.json";
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static OpenWorldSandbox Sandbox()
    {
        Check(Application.isPlaying,"Play required");
        Check(Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")=="/tmp/eternal-station-slots-20261002-final","Isolated save root required");
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();s.Persistence.Automatic=false;s.Clock.Paused=true;
        Check(!s.Persistence.Blocked,s.Persistence.Status);return s;
    }
    static void Record(string key,object value)
    {var report=File.Exists(Report)?JObject.Parse(File.ReadAllText(Report)):new JObject();report[key]=JToken.FromObject(value);File.WriteAllText(Report,report.ToString());}
    static void Save(OpenWorldSandbox s,string stage)
    {Check(s.Persistence.Save(),s.Persistence.Status);var json=JsonUtility.ToJson(s.Persistence.Capture());File.WriteAllText(Before,json);File.WriteAllText(Root+stage+"-snapshot.json",json);}
    static void CheckReload(OpenWorldSandbox s)
    {var before=JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText(Before));var after=s.Persistence.Capture();before.savedUtc=after.savedUtc;Check(JsonUtility.ToJson(before)==JsonUtility.ToJson(after),"Exact snapshot reload including inventories, pending, weapon and slots");}
    public static string Build()
    {
        var s=Sandbox();var c=s.Content;var input=s.GetComponent<OpenWorldInput>();input.Cancel();Check(c.GroundWorld.Buildings.Count==1,"Fresh fixture required");
        string main=c.MainBase.Module<IBaseIdentity>().BaseId;var subDef=s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.nexus");
        BuildingInstance sub=null;for(int z=20;z>=12&&sub==null;z--)for(int x=-5;x<10&&sub==null;x++)if(c.GroundPlacement.Add(subDef,new Vector2Int(x,z),out _).Success){var result=c.GroundPlacement.Confirm();Check(result.Success,result.Message);sub=c.GroundWorld.Buildings.Last();}
        Check(sub!=null,"Sub fixture");c.Bases.Refresh();string secondary=sub.Module<IBaseIdentity>().BaseId;
        var mainBank=c.Inventories.Available(main);var subBank=c.Inventories.Available(secondary);mainBank.Deposit("iron",1000);mainBank.Deposit("coal",200);subBank.Deposit("iron",100);subBank.Deposit("coal",100);
        Check(!mainBank.InfiniteResources&&!subBank.InfiniteResources,"Finite resource fixture");
        var station=s.RailwayHud.Station;var track=s.RailwayHud.Track;var rail=new List<Vector2Int>();
        foreach(int x in new[]{0,6})rail.AddRange(new[]{new Vector2Int(x+2,1),new Vector2Int(x+3,1),new Vector2Int(x+4,1),new Vector2Int(x+5,1),new Vector2Int(x+5,0)});
        Vector2Int? origin=null;
        for(int z=8;z<30&&!origin.HasValue;z++)for(int x=-15;x<15&&!origin.HasValue;x++){
            var o=new Vector2Int(x,z);if(!new[]{0,6,12}.All(dx=>c.HasBuildArea(c.GroundWorld.Grid.Center(o+new Vector2Int(dx,0),station.Footprint),station.Footprint)&&c.GroundPlacement.Validate(new PlacementRequest(-1,station,o+new Vector2Int(dx,0))).Success))continue;
            if(rail.All(p=>c.GroundPlacement.Validate(new PlacementRequest(-2,track,p+o)).Success))origin=o;
        }
        Check(origin.HasValue,"Three terrain-valid stations and two independent connections");
        double spent=0;
        BuildingInstance Install(BuildingDefinition def,Vector2Int cell,string owner){c.Bases.Select(owner);input.BeginEditing();var bank=c.Inventories.Available(owner);double before=bank.Amount("iron");Check(input.Edits.AddContent(def,c.GroundWorld.Grid.Center(cell,Vector2Int.one),WorldGridGeometry.Rotation*Vector3.forward,out var why),why);var result=input.Edits.Confirm();Check(result.Success,result.Message);double cost=before-bank.Amount("iron");Check(cost==(def==station?10:1),"Normal rail charge");spent+=cost;input.Cancel();return c.GroundWorld.Buildings.Last();}
        var a=Install(station,origin.Value,main);var b=Install(station,origin.Value+new Vector2Int(6,0),secondary);var d=Install(station,origin.Value+new Vector2Int(12,0),main);foreach(var p in rail)Install(track,p+origin.Value,main);
        c.Railway.Refresh();var n=c.Railway.Network;
        Check(n.Commit(Guid.NewGuid().ToString("N"),new[]{new RailStop{stationId=a.PersistentId,load=30},new RailStop{stationId=b.PersistentId,unload=30}},out var r,out var e,RailRouteMode.Shuttle),e);
        Check(n.Configure(r,"iron",0,30,0,out e),e);Check(n.Refuel(r,50,out e),e);
        Record("setup",new {stations=3,tracks=10,railIronSpent=spent,finiteResources=true,fixtureBattery=100,fixtureStockIron=1100,fixtureCoal=300,seed="Enemy world default 731; terrain existing asset; no random layout",origin=origin.Value});
        return ArmAndRun();
    }
    public static string ArmAndRun()
    {
        var s=Sandbox();var c=s.Content;var n=c.Railway.Network;var r=n.Routes.Single();
        var all=n.Stations.OrderBy(v=>v.Cell.x).ToArray();var a=all[0];var b=all[1];var d=all[2];string e;
        s.Campaign.MergeMainLevel(2);Record("fixtureMainLevel",2);
        var defense=c.Railway.Defense;BuildingDefinition weapon=null;
        for(int shape=0;shape<3&&weapon==null;shape++){
            var choices=defense.Choices(r).Where(v=>(v.Modules.OfType<WeaponModuleDefinition>().Single().Targets&TargetKind.Ground)!=0).ToArray();
            weapon=choices.FirstOrDefault(v=>v.Modules.OfType<WeaponModuleDefinition>().Single().Delivery==WeaponDelivery.Instant)??choices.FirstOrDefault();
            if(weapon==null)Check(defense.Shape(r,out e),e);
        }
        Check(weapon!=null,"Unlocked ground-compatible weapon required");
        Check(defense.Install(r,weapon,out e),e);
        // Fixture energy injection, while installation/refuel/rail construction use actual payment APIs.
        r.train.armament.battery=100;Check(n.Start(r,out e),e);c.Railway.Tick(26.25);Check(r.train.reverse&&r.train.segmentPaid,"Return movement");
        var startPose=s.RailwayView.Pose(r,c);var enemyPoint=s.RailwayView.CombatPosition(r,c)+Vector3.right*2;int enemy=-1;Action<int> onSpawn=id=>enemy=id;s.Enemies.SpawnedEnemy+=onSpawn;
        Check(s.Enemies.TrySpawn(enemyPoint,enemyPoint,0,1000),"Spawn stationary ground target");s.Enemies.SpawnedEnemy-=onSpawn;
        double battery=r.train.armament.battery;float maxPositionError=0,minDirectionDot=1;
        for(int i=0;i<40;i++){s.Enemies.MoveAndIndex(.05f);s.TargetAdapter.Tick(.05f);c.Railway.Tick(.05,true);var mounted=defense.Mounted.Single();var pose=s.RailwayView.Pose(r,c);maxPositionError=Math.Max(maxPositionError,Vector3.Distance(mounted.Building.Position,s.RailwayView.CombatPosition(r,c)));minDirectionDot=Math.Min(minDirectionDot,Vector3.Dot(mounted.Building.Direction,pose.direction));}
        int damage=1000-s.Enemies.GetEnemy(enemy).health;Check(damage>0,"Actual return-moving weapon damage");Check(r.train.reverse&&r.train.segmentPaid,"Still returning after attack");Check(maxPositionError<.001f&&minDirectionDot>.999f,"Weapon follows return pose");Check(r.train.armament.battery<battery,"Attack debits battery");
        Check(!s.Persistence.Save(),"Combat save must fail");s.Enemies.DespawnAll();s.TargetAdapter.Reset();
        for(int i=0;i<100&&defense.HasPendingExecution;i++)defense.Tick(.05f,true);Check(!defense.HasPendingExecution,"Attack execution settled");
        var extension=new[]{new RailStop{stationId=a.PersistentId},new RailStop{stationId=b.PersistentId},new RailStop{stationId=d.PersistentId}};
        Check(n.UpdateRoute(r,extension,r.revision,out e),e);
        Check(n.Commit(Guid.NewGuid().ToString("N"),new[]{new RailStop{stationId=d.PersistentId,load=30},new RailStop{stationId=b.PersistentId,unload=30}},out var competitor,out e,RailRouteMode.Shuttle),e);
        Check(n.Configure(competitor,"iron",0,30,0,out e),e);Check(n.Refuel(competitor,50,out e),e);
        Save(s,"return");Record("weapon",new {definition=weapon.Id,installIron=TrainArmament.InstallIron,shape=r.train.armament.footprint});
        Record("returnAttack",new{damage,batteryBefore=battery,batteryAfter=r.train.armament.battery,maxPositionError,minDirectionDot,progress=r.train.progress,combatSaveRejected=true,reverse=r.train.reverse});
        Record("returnPendingWeaponSaved",true);return "PASS return attack, pending extension plus weapon saved; reload scene next.";
    }
    public static string ReloadReturnAndShare()
    {
        var s=Sandbox();CheckReload(s);Record("returnPendingWeaponReloadExact",true);var n=s.Content.Railway.Network;var r=n.Routes[0];var q=n.Routes[1];
        n.Tick(r.CurrentLeg.cells.Count-r.train.progress);Check(r.pending.active&&r.pending.failed&&r.revision==0&&r.stops.Count==2,"Competing committed route wins; old route preserved");
        Check(n.Start(q,out var e),e);s.Content.Railway.Tick(10);n.Stop(r);n.Stop(q);s.Content.Railway.Tick(5);
        Check(r.train.status==TrainStatus.Stopped&&q.train.waitingForStation&&q.train.status==TrainStatus.StopRequested,"Simultaneous shared arrival arbitration");
        var waitingPose=s.RailwayView.Pose(q,s.Content);var socket=s.Content.GroundWorld.Grid.Center(q.TravelCell(q.CurrentLeg.cells.Count-1),Vector2Int.one);var offset=waitingPose.position-socket;offset.y=0;Check(offset.magnitude<.001f,"Waiting pose at outside socket");
        Check(n.StationSlotOwner(r.stops[r.train.stop].stationId)==r.id,"Single station owner");
        double cargo=q.train.cargo,fuel=q.train.fuel;string banks=Newtonsoft.Json.JsonConvert.SerializeObject(s.Persistence.Capture().inventories);n.Tick(100);
        Check(q.train.fuel==fuel&&q.train.cargo==cargo&&Newtonsoft.Json.JsonConvert.SerializeObject(s.Persistence.Capture().inventories)==banks,"No fuel/cargo mutation while blocked");
        Record("sharedArrival",new {waitProgress=q.train.progress,entryProgress=RailwayNetwork.StationEntryProgress(q),outsidePositionError=offset.magnitude,blockedSeconds=100,cargo,fuel,stopRequestPreserved=true,reservationLostWithoutRouteMutation=r.pending.failed});Save(s,"waiting");
        return "PASS reserved track competition and shared arrival; waiting/weapon save ready to reload.";
    }
    public static string ReloadWaiting()
    {
        var s=Sandbox();CheckReload(s);var n=s.Content.Railway.Network;var r=n.Routes[0];var q=n.Routes[1];double fuel=q.train.fuel,cargo=q.train.cargo;
        var station=n.Station(q.stops[q.NextStopIndex].stationId);var bank=s.Content.Inventories.Available(station.OwnerBaseId);double stock=bank.Amount("iron");
        Check(n.Start(r,out var e),e);s.Content.Railway.Tick(11);Check(!q.train.waitingForStation&&q.train.status==TrainStatus.Stopped&&q.train.stop==1,"Queued stop request arrives after release");
        Check(q.train.fuel==fuel&&q.train.cargo==cargo-30&&bank.Amount("iron")==stock+30,"One unload, zero extra fuel");
        Record("waitingReload",new {exact=true,admitted=true,unloaded=30,fuelBefore=fuel,fuelAfter=q.train.fuel});Save(s,"admitted");return "PASS exact waiting save reload, slot release, single unload and preserved fuel.";
    }
}
