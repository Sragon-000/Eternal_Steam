using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;

public static class VerifyA5ThreeBasePlay
{
    const string Root = "/tmp/eternal-a5-threebase-live-v4";
    static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    static OpenWorldSandbox World()
    {
        Check(Application.isPlaying && Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT") == Root, "Isolated Play required");
        var s = UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        Check(s != null && s.GetComponent<OpenWorldInput>().Edits != null && !s.Persistence.Blocked, "World ready");
        s.Persistence.Automatic = false;
        s.Clock.Paused = true;
        return s;
    }

    public static string Prepare()
    {
        var s = World();
        var c = s.Content;
        var input = s.GetComponent<OpenWorldInput>();
        input.Cancel();
        Check(c.GroundWorld.Buildings.Count == 1, "Fresh main-base fixture required");
        var mainId = c.MainBase.Module<IBaseIdentity>().BaseId;
        var subDef = s.ContentCatalog.Buildings.Single(d => d.Id == "installation.nexus");
        BuildingInstance PlaceSub(int minX, int maxX, int firstZ = 20, int lastZ = 12)
        {
            for (int z = firstZ; z >= lastZ; z--)
                for (int x = minX; x <= maxX; x++)
                    if (c.GroundPlacement.Add(subDef, new Vector2Int(x, z), out _).Success)
                    {
                        var result = c.GroundPlacement.Confirm();
                        Check(result.Success, result.Message);
                        return c.GroundWorld.Buildings.Last();
                    }
            throw new Exception("Could not place sub base in target area");
        }
        var subB = PlaceSub(-5, 10);
        var subC = PlaceSub(10, 10, 14, 14);
        var bId = subB.Module<IBaseIdentity>().BaseId;
        var cId = subC.Module<IBaseIdentity>().BaseId;
        c.Bases.Refresh();
        var station = s.RailwayHud.Station;
        var track = s.RailwayHud.Track;
        var relative = new List<Vector2Int>();
        for (int i = 0; i < 2; i++)
        {
            for (int x = i * 6 + 2; x <= i * 6 + 5; x++) relative.Add(new Vector2Int(x, 1));
            relative.Add(new Vector2Int(i * 6 + 5, 0));
        }
        for (int y = -1; y >= -6; y--) relative.Add(new Vector2Int(13, y));
        for (int x = 12; x >= 1; x--) relative.Add(new Vector2Int(x, -6));
        for (int y = -5; y <= -1; y++) relative.Add(new Vector2Int(1, y));
        var origin = new Vector2Int(-3, 12);
        Check(Enumerable.Range(0, 3).All(i => c.GroundPlacement.Validate(new PlacementRequest(-1 - i, station, origin + new Vector2Int(i * 6, 0))).Success)
            && relative.All(p => c.GroundPlacement.Validate(new PlacementRequest(-4, track, origin + p)).Success),
            "Three-station closed route fits terrain");

        BuildingInstance Install(BuildingDefinition definition, Vector2Int cell, string owner)
        {
            Check(c.Bases.Select(owner), "Select owner base");
            Check(c.GroundPlacement.Add(definition, cell, out var request).Success, "Install " + definition.Id + " at " + cell);
            var result = c.GroundPlacement.Confirm();
            Check(result.Success, result.Message);
            var built = c.GroundWorld.Buildings.Last();
            Check(built.OwnerBaseId == owner, "Stable building ownership");
            return built;
        }
        var a = Install(station, origin, mainId);
        var b = Install(station, origin + new Vector2Int(6, 0), bId);
        var third = Install(station, origin + new Vector2Int(12, 0), cId);
        foreach (var p in relative) Install(track, origin + p, mainId);
        c.Railway.Refresh();
        var network = c.Railway.Network;
        var stops = new[] {
            new RailStop {stationId = a.PersistentId, arrival = 0, departure = 2},
            new RailStop {stationId = b.PersistentId, arrival = 3, departure = 2},
            new RailStop {stationId = third.PersistentId, arrival = 3, departure = 0}
        };
        Check(network.Commit(Guid.NewGuid().ToString("N"), stops, out var route, out var error), error);
        var bankA = c.Inventories.Available(mainId);
        var bankB = c.Inventories.Available(bId);
        var bankC = c.Inventories.Available(cId);
        Check(bankA.Deposit("iron", 300) == 300 && bankA.Deposit("coal", 50) == 50, "Source stock and coal capacity");
        Check(bankB.Amount("iron") == 0 && bankC.Amount("iron") == 0, "Destinations initially empty");
        Check(bankB.Capacity("iron") >= 30 && bankC.Capacity("iron") >= 40, "Destination storage capacity");

        var hud = s.RailwayHud;
        hud.Execute("open");
        hud.Execute("row:0");
        Check(hud.SelectedStopIndex == 0, "Route selected");
        void Configure(int index, double load, double unload)
        {
            hud.Execute("station-row:" + index);
            Check(hud.SelectedStopIndex == index, "HUD selected station " + index);
            hud.Resource.SetTextWithoutNotify("iron");
            hud.Load.SetTextWithoutNotify(load.ToString());
            hud.Unload.SetTextWithoutNotify(unload.ToString());
            Check(hud.CanExecute("configure", out var why), why);
            hud.Execute("configure");
            Check(route.stops[index].load == load && route.stops[index].unload == unload, "HUD saved stop quantities");
        }
        Configure(0, 100, 0);
        Configure(1, 0, 30);
        Configure(2, 0, 40);
        hud.Execute("station-row:0");
        Check(hud.CanExecute("fuel", out error), error);
        hud.Execute("fuel");
        Check(route.train.fuel == 50 && bankA.Amount("coal") == 0, "HUD refuel transfers source coal");
        Check(hud.CanExecute("start", out error), error);
        hud.Execute("start");
        Check(route.train.status == TrainStatus.Dwelling && route.train.cargo == 100 && bankA.Amount("iron") == 200, "A source load exactly 100");
        c.Railway.Tick(30, false);
        Check(route.train.stop == 2 && route.train.status == TrainStatus.Dwelling, "Arrived at C");
        Check(bankA.Amount("iron") == 200 && bankB.Amount("iron") == 30 && bankC.Amount("iron") == 40 && route.train.cargo == 30, "ACC-007 three-base distribution and remaining cargo");
        Check(bankA.Amount("iron") + bankB.Amount("iron") + bankC.Amount("iron") + route.train.cargo == 300, "Cargo conservation");
        network.Stop(route);
        hud.Execute("close");
        s.Clock.SetPhase(DayPhase.Day);
        s.Enemies.Reset();
        Check(s.Persistence.Save(), s.Persistence.Status);
        File.WriteAllText(Root + "/expected.json", JsonUtility.ToJson(s.Persistence.Capture()));
        s.Persistence.ContinueSaved();
        return "PASS actual three-base HUD configuration, fuel, 100 load/30+40 unload/30 cargo; isolated save and reload requested";
    }

    public static string Reloaded()
    {
        var s = World();
        var expected = JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText(Root + "/expected.json"));
        var actual = s.Persistence.Capture();
        Check(JsonUtility.ToJson(expected.railway) == JsonUtility.ToJson(actual.railway), "Railway exact reload");
        Check(expected.inventories.Count == actual.inventories.Count && !expected.inventories.Where((r, i) => JsonUtility.ToJson(r) != JsonUtility.ToJson(actual.inventories[i])).Any(), "Base inventories exact reload");
        var route = s.Content.Railway.Network.Routes.Single();
        var banks = route.stops.Select(stop => s.Content.Inventories.Available(s.Content.Railway.Network.Station(stop.stationId).OwnerBaseId)).ToArray();
        Check(banks[0].Amount("iron") == 200 && banks[1].Amount("iron") == 30 && banks[2].Amount("iron") == 40 && route.train.cargo == 30, "Reloaded three-base stock and cargo");
        Check(s.RailwayHud.OpenFromStation(route.stops[2].stationId), "Open C station context");
        var hud = s.RailwayHud;
        Check(hud.RowButtons[0].interactable, "Saved route row enabled");
        hud.RowButtons[0].onClick.Invoke();
        Check(hud.SelectedStopIndex == 2, "C station context selects stop 3");
        UnityEngine.UI.Button Button(string command) => hud.Commands.First(x => x.Command == command && x.Button.gameObject.activeInHierarchy).Button;
        hud.Resource.SetTextWithoutNotify("iron");
        hud.Load.SetTextWithoutNotify("0");
        hud.Unload.SetTextWithoutNotify("45");
        hud.SendMessage("Update");
        Check(Button("configure").interactable, "Saved configure button enabled");
        Button("configure").onClick.Invoke();
        Check(route.stops[2].unload == 45, "Saved configure callback updates C target");
        Check(banks[2].Deposit("coal", 10) == 10, "C coal capacity");
        hud.SendMessage("Update");
        Check(Button("fuel").interactable, "Saved fuel button enabled");
        var fuel = route.train.fuel;
        Button("fuel").onClick.Invoke();
        Check(route.train.fuel == fuel + 10 && banks[2].Amount("coal") == 0, "Saved fuel callback transfers once");
        hud.SendMessage("Update");
        Check(Button("start").interactable, "Saved start button enabled");
        Button("start").onClick.Invoke();
        Check(route.train.status == TrainStatus.Dwelling && banks[2].Amount("iron") == 40 && route.train.cargo == 30, "Saved start callback does not repeat C service");
        return "PASS actual v2 reload preserves three base inventories/route/cargo; saved route, configure, fuel and start button callbacks";
    }
}
