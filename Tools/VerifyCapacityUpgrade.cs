using System;
using System.Collections;
using System.Reflection;
using EternalSteam.Demo;
using UnityEngine;

public static class VerifyCapacityUpgrade
{
    public static string Main()
    {
        var sim = UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
        var f = BindingFlags.Instance | BindingFlags.NonPublic;
        var shared = typeof(HordeProgress).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
        var old = HordeProgress.Current;
        string key = "EternalSteam.CapacityTest." + Guid.NewGuid();
        var towers = (IList)typeof(HordeSimulation).GetField("towers", f).GetValue(sim);
        int originalCount = towers.Count;
        void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
        object Call(string name, params object[] args) => typeof(HordeSimulation).GetMethod(name, f).Invoke(sim, args);
        bool Place(HordeTowerKind kind)
        {
            foreach (var zone in HordeMapLayout.BuildZones(sim.MapKind))
                for (float x = zone.xMin + 2; x < zone.xMax - 1; x += 2)
                    for (float z = zone.yMin + 2; z < zone.yMax - 1; z += 2)
                        if (sim.TryPlaceTower(new Vector3(x, 0, z), Vector3.right, 22, kind)) return true;
            return false;
        }
        try
        {
            var p = new HordeProgress(key);
            shared.SetValue(null, p);
            sim.ReturnToLobby();
            Check(!sim.UpgradeTowerCapacity(HordeTowerKind.MachineGun), "Insufficient funds");
            p.AwardClear(sim.MapKind, 1, "first");
            Check(sim.UpgradeTowerCapacity(HordeTowerKind.MachineGun), "Upgrade");
            Check(p.Parts == 100 && p.TowerLimit(HordeTowerKind.MachineGun) == 5 && p.UpgradeCost(HordeTowerKind.MachineGun) == 150, "Cost and count");
            Check(new HordeProgress(key).TowerLimit(HordeTowerKind.MachineGun) == 5, "Save reload");
            sim.Deploy(1);
            Check(!sim.UpgradeTowerCapacity(HordeTowerKind.Cannon), "Battle purchase blocked");
            while (sim.TowerCountFor(HordeTowerKind.MachineGun) < 5) Check(Place(HordeTowerKind.MachineGun), "Place within quota");
            Check(!Place(HordeTowerKind.MachineGun), "Placement limit");
            Check(Place(HordeTowerKind.Arrow), "Independent type limit");
            sim.ReturnToLobby();
            for (int i = 0; i < 60; i++) p.AwardClear(sim.MapKind, 1, "funds" + i);
            while (p.TowerLimit(HordeTowerKind.MachineGun) < 16) Check(sim.UpgradeTowerCapacity(HordeTowerKind.MachineGun), "Upgrade to maximum");
            int balance = p.Parts;
            Check(!sim.UpgradeTowerCapacity(HordeTowerKind.MachineGun) && p.Parts == balance, "Max does not charge");
            p.AwardClear(sim.MapKind, 2, "unlock3");
            sim.Deploy(3);
            Check(sim.StageEnemyTotal == 5000, "Stage size");
            sim.ToggleSpawning();
            for (int i = 0; i < 5000; i++)
            {
                Call("Spawn");
                Call("Remove", 0, true);
                if (i == 4998) Check(!sim.StageCleared, "No early clear");
            }
            Call("Spawn");
            Check(sim.Spawned == 5000 && sim.Killed == 5000 && sim.StageCleared, "Stage exceeds pool and clears");
            return "PASS: insufficient funds, costs, persisted per-type limits, placement enforcement, battle lock, max cap, 5000-enemy quota with slot reuse";
        }
        finally
        {
            sim.ReturnToLobby();
            while (towers.Count > originalCount)
            {
                var tower = towers[towers.Count - 1];
                UnityEngine.Object.Destroy((GameObject)tower.GetType().GetField("root").GetValue(tower));
                towers.RemoveAt(towers.Count - 1);
            }
            shared.SetValue(null, old);
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
