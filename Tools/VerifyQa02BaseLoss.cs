using System;
using System.IO;
using System.Linq;
using System.Globalization;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;

public static class VerifyQa02BaseLoss
{
    const string Root="/tmp/eternal-railway-a1";
    const string SnapshotPath="/tmp/eternal-qa02-base-loss.json";
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
    public static string DestroySubAndSave()
    {
        var s=World();var c=s.Content;var n=c.Railway.Network;var route=n.Routes.Single();
        var main=c.GroundWorld.Buildings.Single(b=>b.Module<IBaseRole>()?.Role==BaseRole.Main);
        var sub=c.GroundWorld.Buildings.Single(b=>b.Module<IBaseRole>()?.Role==BaseRole.Sub);
        var mainId=main.Module<IBaseIdentity>().BaseId;var subId=sub.Module<IBaseIdentity>().BaseId;
        var mainBank=c.Inventories.Available(mainId);var subBank=c.Inventories.Available(subId);
        Check(mainBank!=null&&subBank!=null&&route.train.cargo>0,"Two-base cargo fixture required");
        subBank.Deposit("iron",17);
        StorePower(main.Module<PowerModule>(),20);StorePower(sub.Module<PowerModule>(),30);
        var mainIron=mainBank.Amount("iron");var subIron=subBank.Amount("iron");
        var cargo=route.train.cargo;var fuel=route.train.fuel;
        sub.Module<HealthModule>().ApplyDamage(100000);
        c.Power.Refresh();c.Railway.Refresh();
        Check(sub.Disposed&&!c.Bases.Bases.ContainsKey(subId)&&c.Inventories.Available(subId)==null,"Lost base must be unavailable");
        Check(mainBank.Amount("iron")==mainIron&&c.Inventories.Ensure(subId).Amount("iron")==subIron,"Base stocks must not merge or disappear");
        Check(main.Module<PowerModule>().Stored==20,"Main stored power changed after sub loss");
        Check(route.train.status==TrainStatus.RouteError&&route.train.cargo==cargo&&route.train.fuel==fuel,"Route error must retain cargo and paid fuel");
        s.Clock.SetPhase(DayPhase.Day);s.Enemies.Reset();
        Check(s.Persistence.Save(),s.Persistence.Status);
        File.WriteAllText(SnapshotPath,JsonUtility.ToJson(s.Persistence.Capture()));
        s.Persistence.ContinueSaved();
        return $"PASS sub loss: main iron {mainIron}, retired iron {subIron}, main power 20, cargo {cargo}, fuel {fuel}; saved and reloading";
    }
    public static string Reloaded()
    {
        var s=World();var expected=JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText(SnapshotPath));
        var actual=s.Persistence.Capture();expected.savedUtc=actual.savedUtc;
        Check(JsonUtility.ToJson(expected)==JsonUtility.ToJson(actual),"Base loss snapshot changed on reload");
        Check(s.Content.Bases.Bases.Count==1&&s.Content.Railway.Network.Routes.Single().train.status==TrainStatus.RouteError,"Lost base or route error not restored");
        return "PASS lost-base stocks, power, route, cargo and fuel exact save/reload";
    }
}
