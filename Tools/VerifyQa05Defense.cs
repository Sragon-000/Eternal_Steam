using System;
using System.IO;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;

public static class VerifyQa05Defense
{
    const string Root = "/tmp/eternal-qa05-defense";
    static readonly string[] Ids =
    {
        "defense.tesla", "defense.plasma_laser", "defense.vulcan_aa", "defense.rail_aa",
        "defense.arc", "defense.railgun", "defense.sky_plasma", "defense.smart_missile", "defense.emp"
    };

    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    static OpenWorldSandbox World()
    {
        Check(Application.isPlaying && Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT") == Root,
            "QA-05 B needs isolated Play");
        var s = UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        Check(s != null && s.Persistence != null && !s.Persistence.Blocked, s?.Persistence?.Status ?? "World missing");
        s.Persistence.Automatic = false;
        s.Clock.Paused = true;
        Application.runInBackground = true;
        return s;
    }

    static void Charge(PowerModule store, double amount)
    {
        var state = SavedState.Capture(store);
        state.values.Single(v => v.name == "Stored").text = amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        state.Restore(store);
    }

    static BuildingInstance Install(OpenWorldSandbox s, BuildingDefinition definition)
    {
        var world = s.Content.GroundWorld;
        var origin = s.Content.MainBase.Cell;
        var area = s.Content.MainBase.Module<IBuildArea>();
        for (int z = -30; z <= 40; z++)
            for (int x = -30; x <= 30; x++)
            {
                var cell = origin + new Vector2Int(x, z);
                var center = world.Grid.Center(cell, definition.Footprint);
                if (!area.Contains(center, (Vector2)definition.Footprint, WorldGridGeometry.Rotation)) continue;
                if (!s.Content.GroundPlacement.Add(definition, cell, out _).Success) continue;
                var result = s.Content.GroundPlacement.Confirm();
                Check(result.Success, definition.Id + ": " + result.Message);
                return world.Buildings.Single(b => b.DefinitionId == definition.Id);
            }
        throw new Exception("No valid ground for " + definition.Id);
    }

    public static string Prepare()
    {
        var s = World();
        Check(s.Content.MainBase != null && s.Content.GroundWorld.Buildings.Count(b => b.Module<WeaponRuntime>() != null) == 0,
            "Fresh main-only Play required");
        var mainLevel = s.Content.MainBase.Module<IUpgradeControl>();
        while (mainLevel.Level < 10) Check(mainLevel.TryUpgrade(out var reason), "Main upgrade: " + reason);
        Check(s.Campaign.MainLevel == 10, "Main level 10 not applied");
        var store = s.Content.MainBase.Module<PowerModule>();
        Check(store != null && store.Role == PowerRole.Storage, "Main power store missing");

        foreach (var id in Ids)
        {
            var definition = s.ContentCatalog.Buildings.Single(d => d.Id == id);
            var building = Install(s, definition);
            var weapon = building.Module<WeaponRuntime>();
            var upgrade = building.Module<IUpgradeControl>();
            var consumer = building.Module<PowerModule>();
            Check(weapon != null && upgrade != null && consumer?.Role == PowerRole.Consumer,
                id + " missing active weapon, upgrade or consumer");

            s.TargetAdapter.Reset();
            s.Enemies.Reset();
            bool air = definition.Modules.OfType<WeaponModuleDefinition>().Single().Targets == TargetKind.Air;
            var point = building.Position + Vector3.forward * 5;
            Check(s.Enemies.TrySpawn(point, point, 0, 10000, air), id + " enemy spawn failed");
            s.Enemies.MoveAndIndex(0);
            Charge(store, 0);
            s.Content.Power.Tick(1);
            Check(!consumer.AllowsCombat, id + " attacked without supply");
            building.Tick(1);
            Check(s.Enemies.GetEnemy(0).health == 10000 && s.Enemies.GetEnemy(0).movementPenalty == 0,
                id + " fired while unpowered");

            Charge(store, 100);
            s.Content.Power.Tick(1);
            Check(consumer.AllowsCombat, id + " did not resume with supply");
            for (int tick = 0; tick < 120; tick++) building.Tick(.1f);
            if (id == "defense.emp")
                Check(s.Enemies.GetEnemy(0).health == 10000 && s.Enemies.GetEnemy(0).movementPenalty > 0,
                    "EMP did not apply zero-damage stun");
            else Check(s.Enemies.GetEnemy(0).health < 10000, id + " did not damage in live scene");

            s.TargetAdapter.Reset();
            s.Enemies.Reset();
            building.Tick(6);
            Check(!weapon.HasPendingExecution, id + " left in-flight attack after target reset");
            float baseDamage = weapon.Damage, baseRange = weapon.Range, baseInterval = weapon.Interval;
            Check(s.Persistence.Upgrades.TryUpgrade(building, out var upgradeReason), id + " first upgrade: " + upgradeReason);
            Check(upgrade.Level == 2 && (id == "defense.emp" ? weapon.Damage == 0 : weapon.Damage > baseDamage)
                && weapon.Range > baseRange && weapon.Interval < baseInterval,
                id + " first upgrade did not scale combat values");
            while (upgrade.Level < upgrade.MaximumLevel)
                Check(s.Persistence.Upgrades.TryUpgrade(building, out upgradeReason), id + " upgrade: " + upgradeReason);
            Check(!s.Persistence.Upgrades.TryUpgrade(building, out _) && upgrade.Level == upgrade.MaximumLevel,
                id + " exceeded maximum upgrade");
        }

        s.Clock.SetPhase(DayPhase.Day);
        Check(s.Enemies.Alive == 0, "Enemy remains before defense save");
        Check(s.Persistence.CanSave(out var saveReason), "Unsafe defense save: " + saveReason);
        Check(s.Persistence.Save(), s.Persistence.Status);
        Directory.CreateDirectory(Root);
        File.WriteAllText(Path.Combine(Root, "expected.json"), JsonUtility.ToJson(s.Persistence.Capture()));
        s.Persistence.ContinueSaved();
        return "PASS nine live defenses: install, no-power pause, powered attack/status, first upgrade, own maximum, safe v2 save; reload requested";
    }

    public static string Reloaded()
    {
        var s = World();
        var expected = JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText(Path.Combine(Root, "expected.json")));
        var actual = s.Persistence.Capture();
        Check(actual.version == 2 && actual.runId == expected.runId, "QA-05 B v2 run not restored");
        foreach (var id in Ids)
        {
            var before = expected.buildings.Single(b => b.definition == id);
            var after = actual.buildings.Single(b => b.definition == id);
            Check(after.id == before.id && after.owner == before.owner && after.cell == before.cell,
                id + " position or ownership changed on reload");
            var b = s.Content.GroundWorld.Buildings.Single(x => x.DefinitionId == id);
            var level = b.Module<IUpgradeControl>();
            Check(level.Level == level.MaximumLevel && b.Module<WeaponRuntime>() != null && b.Module<PowerModule>() != null,
                id + " combat, power or maximum level not restored");
        }
        Check(s.Persistence.CanSave(out var reason), "Reloaded state cannot save: " + reason);
        return "PASS nine live defense definitions and maximum upgrade levels restored from isolated v2 save";
    }
}
