using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;

// Uses the actual simulation and purchase/edit commands. No inventory, energy,
// clock phase, enemy health or power storage injections are permitted here.
public static class VerifyFirstPlayableLoop
{
    const string Root = "/tmp/eternal-first-loop-20260930";
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    static OpenWorldSandbox World()
    {
        Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")==Root,"Isolated Play required");
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        Check(s!=null&&!s.Persistence.Blocked&&!s.Content.InfiniteResources,"World unavailable or infinite mode enabled");
        s.Persistence.Automatic=false;s.enabled=false;
        Check(!s.Clock.Paused&&!s.GetComponent<OpenWorldInput>().IsEditing,"Simulation must run normally outside edits");
        return s;
    }
    static BuildingInstance Install(OpenWorldSandbox s,string id,string owner,Vector2Int? exact=null,int minZ=15,int maxZ=35)
    {
        var c=s.Content;var input=s.GetComponent<OpenWorldInput>();var definition=s.ContentCatalog.Buildings.Single(d=>d.Id==id);
        Check(c.Bases.Select(owner),"Owner unavailable: "+id);input.BeginEditing();
        bool Add(Vector2Int cell){
            if(definition.Placement.RequiresOwnerBase||definition.Placement.RequiresOperationalArea){var center=c.GroundWorld.Grid.Center(cell,definition.Footprint);if(!c.Bases.Covers(center,(Vector2)definition.Footprint,WorldGridGeometry.Rotation,owner))return false;}
            var point=c.GroundWorld.Grid.Center(cell,Vector2Int.one);point.y=s.Ground.SampleHeight(point)+s.Ground.transform.position.y;
            return input.Edits.AddContent(definition,point,WorldGridGeometry.Rotation*Vector3.forward,out _);
        }
        bool added=false;
        if(exact.HasValue)added=Add(exact.Value);
        else for(int z=minZ;z<=maxZ&&!added;z++)for(int x=-8;x<=12&&!added;x++)added=Add(new Vector2Int(x,z));
        Check(added,"No valid placement: "+id);Check(input.Confirm(),"Confirm: "+s.Message);
        return c.GroundWorld.Buildings.Last();
    }
    static void Upgrade(OpenWorldSandbox s,BuildingInstance b,int level)
    {while(b.Module<IUpgradeControl>().Level<level)Check(s.Persistence.Upgrades.TryUpgrade(b,out var why),why);}
    public static string Prepare()
    {
        var s=World();var c=s.Content;Check(c.GroundWorld.Buildings.Count==1,"Fresh main-only world required");
        var main=c.MainBase.Module<IBaseIdentity>().BaseId;Upgrade(s,c.MainBase,3);
        var sub=Install(s,"installation.nexus",main,new Vector2Int(-5,20));var other=sub.Module<IBaseIdentity>().BaseId;
        Upgrade(s,sub,3);c.Bases.Refresh();
        Install(s,"resource.iron",main);Install(s,"resource.iron",main);Install(s,"railway.coal",main);
        Install(s,"resource.iron",other,minZ:21);
        foreach(var owner in new[]{main,other})
        {
            Install(s,"resource.power_generator",owner);Install(s,"resource.power_generator",owner);
            Upgrade(s,Install(s,"defense.arc",owner),3);
            Upgrade(s,Install(s,"defense.plasma_laser",owner),3);
        }
        c.Bases.Select(main);
        return State(s,"Prepared main/sub Lv3, natural iron/coal production and powered defenses via edit/purchase commands");
    }
    public static string BuildRailway()
    {
        var s=World();var c=s.Content;var main=c.MainBase.Module<IBaseIdentity>().BaseId;
        var sub=c.Bases.Bases.Values.Single(b=>b.Nexus.Module<IBaseRole>().Role==BaseRole.Sub).Nexus.Module<IBaseIdentity>().BaseId;
        var origin=new Vector2Int(-3,12);
        var a=Install(s,"railway.station",main,origin);
        var b=Install(s,"railway.station",sub,origin+new Vector2Int(6,0));
        var cells=new List<Vector2Int>();for(int x=2;x<=5;x++)cells.Add(new Vector2Int(x,1));cells.Add(new Vector2Int(5,0));
        for(int y=-1;y>=-6;y--)cells.Add(new Vector2Int(7,y));for(int x=6;x>=1;x--)cells.Add(new Vector2Int(x,-6));for(int y=-5;y<=-1;y++)cells.Add(new Vector2Int(1,y));
        double before=c.Inventories.Available(main).Amount("iron");
        foreach(var p in cells)Install(s,"railway.track",main,origin+p);
        Check(c.Inventories.Available(main).Amount("iron")==before-cells.Count,"Rail construction must charge produced iron");
        return StartRailway();
    }
    public static string StartRailway()
    {
        var s=World();var c=s.Content;var stations=c.GroundWorld.Buildings.Where(v=>v.DefinitionId=="railway.station").ToArray();var a=stations[0];var b=stations[1];
        c.Railway.Refresh();var n=c.Railway.Network;
        Check(n.Commit(Guid.NewGuid().ToString("N"),new[]{new RailStop{stationId=a.PersistentId,arrival=2,departure=1},new RailStop{stationId=b.PersistentId,arrival=0,departure=2}},out var r,out var why),why);
        var hud=s.RailwayHud;hud.Execute("open");hud.Execute("row:0");
        void Configure(int index,string load,string unload){hud.Execute("station-row:"+index);hud.Resource.SetTextWithoutNotify("iron");hud.Load.SetTextWithoutNotify(load);hud.Unload.SetTextWithoutNotify(unload);Check(hud.CanExecute("configure",out var reason),reason);hud.Execute("configure");}
        Configure(0,"30","0");Configure(1,"0","30");hud.Execute("station-row:0");
        Check(hud.CanExecute("fuel",out why),why);hud.Execute("fuel");Check(r.train.fuel>0,"Naturally produced coal must fuel train");
        Check(hud.CanExecute("start",out why),why);hud.Execute("start");Check(r.train.status==TrainStatus.Dwelling&&r.train.cargo==30,"Start should load produced iron");
        return State(s,"Rail paid from produced iron; HUD cargo/fuel/start accepted");
    }
    public static string VerifyFirstDelivery()
    {
        var s=World();var r=s.Content.Railway.Network.Routes.Single();
        var destination=s.Content.Railway.Network.Station(r.stops[1].stationId);
        var bank=s.Content.PaymentBankFor(destination);double before=bank.Amount("iron");
        for(int i=0;i<30&&r.train.stop==0;i++)s.AdvanceSimulation(1);
        Check(r.train.stop==1&&r.train.cargo==0&&bank.Amount("iron")>=before+30,"First 30 iron delivery failed");
        return State(s,"PASS first 30 produced iron delivered to separate base inventory");
    }
    public static string Advance(int seconds)
    {var s=World();for(int i=0;i<seconds&&!s.Content.Defeated&&s.Assault.Energy.Stage!=MapStage.AwaitingCraft;i++)s.AdvanceSimulation(1);return State(s,"Advanced actual simulation");}
    public static string Reloaded()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        Check(Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")==Root&&!s.Persistence.Blocked,"Isolated valid reload required");
        s.Persistence.Automatic=false;s.enabled=false;
        var expected=JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText(Path.Combine(Root,"cleared.json")));
        var actual=s.Persistence.Capture();
        Check(actual.runId==expected.runId&&actual.energy.PerfectOrb&&actual.energy.Stage==MapStage.Cleared,"Clear/orb not restored");
        Check(actual.buildings.Count==expected.buildings.Count&&actual.railway.routes.Count==1,"Buildings or railway not restored");
        foreach(var bank in expected.inventories){var current=actual.inventories.Single(v=>v.baseId==bank.baseId);foreach(var stock in bank.stocks)Check(current.stocks.Single(v=>v.id==stock.id).amount==stock.amount,"Restored stock changed");}
        Check(s.Enemies.Alive==0,"Enemies restored after clear");Check(s.Persistence.CanSave(out var why),why);
        return "PASS same run, completed orb, buildings, route and exact base inventories restored after clear";
    }
    static string State(OpenWorldSandbox s,string note)
    {
        var main=s.Content.PaymentBankFor(s.Content.MainBase);var n=s.Content.Railway.Network;
        return note+" | day="+s.Clock.Day+" phase="+s.Clock.Phase+" remaining="+s.Clock.RemainingSeconds.ToString("0.0")+" energy="+(100d*s.Assault.Energy.Earned/s.Assault.Energy.Target).ToString("0.0")+" stage="+s.Assault.Energy.Stage+" bossHp="+s.Assault.BossHealth+" enemies="+s.Enemies.Alive+" killed="+s.Enemies.Killed+" mainHp="+s.Content.MainBase.Module<HealthModule>().Current+" iron="+main.Amount("iron")+" coal="+main.Amount("coal")+" train="+(n.Routes.Count==0?"none":n.Routes[0].train.status+" stop="+n.Routes[0].train.stop+" cargo="+n.Routes[0].train.cargo+" fuel="+n.Routes[0].train.fuel);
    }
    public static string Finish()
    {
        var s=World();Check(!s.Content.Defeated&&s.Assault.Energy.Stage==MapStage.AwaitingCraft,"Natural boss defeat required");
        var hud=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();int killed=s.Enemies.Killed;long earned=s.Assault.Energy.Earned;
        hud.Execute("orb-craft");
        Check(s.Enemies.Alive==0&&s.Enemies.Killed==killed&&s.Assault.Energy.Earned==earned,"Clear must despawn without kill/energy reward");
        Check(s.Assault.Energy.Stage==MapStage.Cleared&&s.Assault.Energy.PerfectOrb,"HUD orb craft failed");
        // Let already launched projectiles and chain executions settle after map completion.
        for(int i=0;i<12;i++)s.AdvanceSimulation(1);
        Check(!s.Content.Defeated&&s.Enemies.Alive==0,"Residual enemies did not clear");
        var r=s.Content.Railway.Network.Routes.Single();s.Content.Railway.Network.Stop(r);
        for(int i=0;i<100&&r.train.status==TrainStatus.StopRequested;i++)s.AdvanceSimulation(1);
        Check(s.Persistence.CanSave(out var why),why);Check(s.Persistence.Save(),s.Persistence.Status);
        File.WriteAllText(Path.Combine(Root,"cleared.json"),JsonUtility.ToJson(s.Persistence.Capture(),true));
        return State(s,"PASS natural production, paid railway, transportation, night defense, boss defeat, HUD orb craft and clear save");
    }
}
