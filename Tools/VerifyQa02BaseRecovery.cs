using System;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;

public static class VerifyQa02BaseRecovery
{
    const string Root="/tmp/eternal-railway-a1";
    const string SnapshotPath="/tmp/eternal-qa02-base-recovery.json";
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static OpenWorldSandbox World()
    {
        Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")==Root,"Isolated Play required");
        var world=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        Check(world!=null&&world.Persistence!=null&&!world.Persistence.Blocked,"World save unavailable");
        world.Persistence.Automatic=false;world.Clock.Paused=true;return world;
    }
    static void StorePower(PowerModule module,double amount)
    {
        var saved=SavedState.Capture(module);saved.values.Single(value=>value.name=="Stored").text=amount.ToString(CultureInfo.InvariantCulture);saved.Restore(module);
    }
    public static string Recover()
    {
        var s=World();var c=s.Content;var route=c.Railway.Network.Routes.Single();
        var main=c.GroundWorld.Buildings.Single(b=>b.Module<IBaseRole>()?.Role==BaseRole.Main);
        var sub=c.GroundWorld.Buildings.Single(b=>b.Module<IBaseRole>()?.Role==BaseRole.Sub);
        var subId=sub.Module<IBaseIdentity>().BaseId;var mainId=main.Module<IBaseIdentity>().BaseId;
        var station=c.GroundWorld.Buildings.Single(b=>b.Module<RailFacility>()?.Kind==RailFacilityKind.Station&&b.OwnerBaseId==subId);
        Check(route.train.status==TrainStatus.Stopped&&route.stops[route.train.stop].stationId!=station.PersistentId,"Unoccupied sub station required");
        StorePower(main.Module<PowerModule>(),20);StorePower(sub.Module<PowerModule>(),30);
        var mainIron=c.Inventories.Available(mainId).Amount("iron");var subIron=c.Inventories.Available(subId).Amount("iron");
        var cargo=route.train.cargo;var fuel=route.train.fuel;var count=c.GroundWorld.Buildings.Count;var occupied=c.GroundWorld.Grid.OccupiedCount;
        var input=s.GetComponent<OpenWorldInput>();Check(input.Edits!=null,"Input Start pending");input.Cancel();input.BeginEditing();
        Check(input.Edits.ToggleGroundRecovery(sub,out var reason),reason);
        var first=input.Edits.Confirm();Check(first.Success,first.Message);input.Cancel();c.Power.Refresh();c.Railway.Refresh();
        Check(sub.Disposed&&!station.Disposed&&c.GroundWorld.Buildings.Count==count-1&&c.GroundWorld.Grid.OccupiedCount==occupied-9,"Only selected sub must be removed");
        Check(c.Inventories.Available(mainId).Amount("iron")==mainIron&&c.Inventories.Ensure(subId).Amount("iron")==subIron&&route.train.cargo==cargo&&route.train.fuel==fuel,"Sub recovery changed stocks or train payload");
        Check(main.Module<PowerModule>().Stored==20,"Sub recovery changed main stored power");
        input.BeginEditing();Check(input.Edits.ToggleGroundRecovery(station,out reason),reason);
        var second=input.Edits.Confirm();Check(second.Success,second.Message);input.Cancel();c.Power.Refresh();c.Railway.Refresh();
        Check(sub.Disposed&&station.Disposed&&c.Bases.Bases.Count==1&&c.Inventories.Available(subId)==null,"Sub and station not removed in order");
        Check(c.Inventories.Ensure(subId).Amount("iron")==subIron&&c.Inventories.Available(mainId).Amount("iron")==mainIron,"Recovery merged or lost stocks");
        Check(main.Module<PowerModule>().Stored==20,"Station recovery changed main stored power");
        Check(route.train.status==TrainStatus.RouteError&&route.train.cargo==cargo&&route.train.fuel==fuel,"Recovery changed train payload or fuel");
        s.Clock.SetPhase(DayPhase.Day);s.Enemies.Reset();
        Check(s.Persistence.Save(),s.Persistence.Status);
        File.WriteAllText(SnapshotPath,JsonUtility.ToJson(s.Persistence.Capture()));s.Persistence.ContinueSaved();
        return $"PASS removed={count-c.GroundWorld.Buildings.Count}; stock={mainIron}/{subIron}; cargo={cargo}; fuel={fuel}; saved and reloading";
    }
    public static string Reloaded()
    {
        var s=World();var expected=JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText(SnapshotPath));var actual=s.Persistence.Capture();expected.savedUtc=actual.savedUtc;
        Check(JsonUtility.ToJson(expected)==JsonUtility.ToJson(actual),"Recovered base snapshot changed on reload");
        Check(s.Content.Bases.Bases.Count==1&&s.Content.Railway.Network.Routes.Single().train.status==TrainStatus.RouteError,"Recovered base or broken route changed");
        return "PASS base then station recovery, stocks and railway exact reload";
    }
}
