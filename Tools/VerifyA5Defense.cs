using System;
using System.IO;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;

public static class VerifyA5Defense
{
    const string Root = "/tmp/eternal-a5-defense-live";
    const string Expected = Root + "/defense-expected.json";
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    static OpenWorldSandbox World()
    {
        Check(Application.isPlaying && Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT") == Root, "Isolated Play required");
        var sandbox = UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        sandbox.Persistence.Automatic = false;
        sandbox.Clock.Paused = true;
        Check(!sandbox.Persistence.Blocked, sandbox.Persistence.Status);
        return sandbox;
    }

    public static string Prepare()
    {
        var sandbox = World();
        var railway = sandbox.Content.Railway;
        var defense = railway.Defense;
        Check(defense != null, "Train defense controller connected to live scene");
        var route = railway.Network.Routes.Single();
        Check(route.train.status == TrainStatus.Stopped && sandbox.Campaign.MainLevel == 1, "Fresh stopped route at main level 1");
        var arc = sandbox.ContentCatalog.Buildings.Single(d => d.Id == "defense.arc");
        Check(!defense.Choices(route).Contains(arc), "Main level 1 keeps arc locked");
        Check(!defense.Install(route, arc, out _), "Locked arc cannot be installed");
        Check(sandbox.Content.MainBase.Module<IUpgradeControl>().TryUpgrade(out var reason), reason);
        Check(sandbox.Campaign.MainLevel == 2 && defense.Choices(route).Contains(arc), "Main level 2 unlocks arc");

        var station = railway.Network.Station(route.stops[route.train.stop].stationId);
        var bank = sandbox.Content.Inventories.Available(station.OwnerBaseId);
        var beforeIron = bank.Amount("iron");
        Check(defense.Install(route, arc, out reason), reason);
        Check(Math.Abs(bank.Amount("iron") - beforeIron + TrainArmament.InstallIron) < .001, "Installation charged once");
        var mounted = defense.Mounted.Single();
        var upgrade = mounted.Building.Module<IUpgradeControl>();
        Check(upgrade.Level == 1 && route.train.armament.definition == arc.Id, "Mounted arc active");
        Check(defense.Upgrade(route, out reason), reason);
        Check(upgrade.Level == 2 && Math.Abs(bank.Amount("iron") - beforeIron + TrainArmament.InstallIron + 5) < .001, "Upgrade paid once");
        var paid = bank.Amount("iron");
        Check(!defense.Upgrade(route, out _) && bank.Amount("iron") == paid, "Main cap rejects second upgrade without debit");

        Check(railway.Network.Configure(route, "iron", 0, 0, 0, out reason), reason);
        Check(railway.Network.Refuel(route, 50, out reason), reason);
        Check(railway.Network.Start(route, out reason), reason);
        Check(railway.Network.UpdateRoute(route, route.stops.Select(s => s.Copy()).ToArray(), route.revision, out reason), reason);
        Check(railway.Network.HasPending(route), "Reservation active alongside mounted defense");
        sandbox.Clock.SetPhase(DayPhase.Day);
        Check(sandbox.Persistence.Save(), sandbox.Persistence.Status);
        File.WriteAllText(Expected, JsonUtility.ToJson(sandbox.Persistence.Capture()));
        sandbox.Persistence.ContinueSaved();
        return "PASS live unlock/install/iron10/upgrade/iron5/cap rejection and pending route plus mounted weapon saved; reload requested";
    }

    public static string Reloaded()
    {
        var sandbox = World();
        var expected = JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText(Expected));
        var actual = sandbox.Persistence.Capture();
        Check(actual.version == 2 && actual.railway.routes.Count == 1, "Railway v2 restored");
        Check(JsonUtility.ToJson(expected.railway) == JsonUtility.ToJson(actual.railway), "Pending route and mounted weapon exact reload");
        Check(expected.inventories.Count == actual.inventories.Count && !expected.inventories.Where((r, i) => JsonUtility.ToJson(r) != JsonUtility.ToJson(actual.inventories[i])).Any(), "Base inventories exact reload");
        var route = sandbox.Content.Railway.Network.Routes.Single();
        Check(sandbox.Content.Railway.Network.HasPending(route), "Pending route remains active");
        var mounted = sandbox.Content.Railway.Defense.Mounted.Single();
        Check(mounted.Building.Module<IUpgradeControl>().Level == 2 && route.train.armament.definition == "defense.arc", "Mounted weapon rehydrated at level 2");
        var invalid = JsonUtility.FromJson<SingleMapSnapshot>(JsonUtility.ToJson(actual));
        invalid.railway.routes[0].train.armament.modules.Single(m => m.type == typeof(PerformanceUpgrade).FullName).values.Single(v => v.name == "Level").text = "3";
        bool rejected = false;
        try { sandbox.Persistence.Validate(invalid); } catch (ArgumentException) { rejected = true; }
        Check(rejected && mounted.Building.Module<IUpgradeControl>().Level == 2, "Over-cap mounted save rejected without changing live state");
        return "PASS actual v2 reload preserves pending route, mounted arc level 2 and base inventories; over-cap mounted snapshot rejected";
    }
}
