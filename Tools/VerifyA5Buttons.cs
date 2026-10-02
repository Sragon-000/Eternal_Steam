using System;
using System.Linq;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;
using UnityEngine;

public static class VerifyA5Buttons
{
    const string Root = "/tmp/eternal-a5-buttons-live";
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }

    public static string Run()
    {
        Check(Application.isPlaying && Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT") == Root, "Isolated Play required");
        var s = UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        Check(s != null && s.GetComponent<OpenWorldInput>().Edits != null && !s.Persistence.Blocked, "World ready");
        s.Persistence.Automatic = false;
        s.Clock.Paused = true;
        var network = s.Content.Railway.Network;
        var route = network.Routes.Single();
        if (route.train.status == TrainStatus.Dwelling) network.Stop(route);
        Check(route.train.status == TrainStatus.Stopped, "Stopped route");
        var station = network.Station(route.stops[route.train.stop].stationId);
        var bank = s.Content.Inventories.Available(station.OwnerBaseId);
        var defense = s.Content.Railway.Defense;
        var hud = s.RailwayHud;
        hud.Execute("open");
        hud.Execute("row:0");
        Check(hud.CanExecute("fuel", out _), "Fuel initially available");
        route.train.fuel = 0;
        bank.Withdraw("coal", double.MaxValue);
        Check(!hud.CanExecute("fuel", out _), "No coal disables fuel");
        hud.Execute("fuel");
        Check(route.train.fuel == 0, "Disabled fuel callback does not refuel");
        bank.Deposit("coal", 10);
        Check(hud.CanExecute("fuel", out _), "Coal enables fuel");
        hud.Execute("fuel");
        Check(route.train.fuel == 10 && bank.Amount("coal") == 0, "Fuel transfers available coal once");

        hud.Execute("defense");
        Check(!hud.CanExecute("weapon-upgrade", out _), "Main level cap disables upgrade");
        Check(!hud.CanExecute("weapon-install", out _), "Mounted weapon disables install");
        Check(hud.CanExecute("weapon-remove", out _), "Mounted weapon can be removed");
        hud.Execute("weapon-remove");
        Check(string.IsNullOrEmpty(route.train.armament.definition), "Remove callback acts");
        bank.Withdraw("iron", double.MaxValue);
        Check(!hud.CanExecute("weapon-install", out _), "No iron disables install");
        hud.Execute("weapon-install");
        Check(string.IsNullOrEmpty(route.train.armament.definition), "Disabled install callback does not install");
        bank.Deposit("iron", TrainArmament.InstallIron + 5);
        Check(hud.CanExecute("weapon-install", out _), "Iron enables install");
        hud.Execute("weapon-install");
        Check(!string.IsNullOrEmpty(route.train.armament.definition) && bank.Amount("iron") == 5, "Install charges once");
        Check(hud.CanExecute("weapon-upgrade", out _), "Affordable level one can upgrade");
        hud.Execute("weapon-upgrade");
        Check(!hud.CanExecute("weapon-upgrade", out _) && bank.Amount("iron") == 0, "Level cap disables repeat with no debit");

        var power = s.Content.Bases.Bases[station.OwnerBaseId].Nexus.Module<PowerModule>();
        var state = SavedState.Capture(power);
        state.values.Single(v => v.name == "Stored").text = "0";
        state.Restore(power);
        route.train.armament.battery = 0;
        Check(!hud.CanExecute("weapon-charge", out _), "Empty storage disables charge");
        hud.Execute("weapon-charge");
        Check(route.train.armament.battery == 0, "Disabled charge callback does not transfer");
        state.values.Single(v => v.name == "Stored").text = "10";
        state.Restore(power);
        Check(hud.CanExecute("weapon-charge", out _), "Stored power enables charge");
        hud.Execute("weapon-charge");
        Check(route.train.armament.battery == 10 && power.Stored == 0, "Charge conserves power");
        return "PASS fuel stock/tank, install iron, upgrade cap, charge stored power buttons and rejected callbacks";
    }
}
