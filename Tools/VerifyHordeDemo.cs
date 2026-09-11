using System;
using System.Reflection;
using UnityEngine;
using EternalSteam.Demo;

public static class VerifyHordeDemo
{
    public static string Main()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Run in Play mode.");
        var s = UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
        s.ClearTowers();
        s.ResetEnemies();
        Assert(!s.TryPlaceTower(Vector3.zero), "Path placement rejected");
        Assert(!s.TryPlaceTower(new Vector3(100, 0, 8)), "Outside placement rejected");
        Assert(s.TryPlaceTower(new Vector3(-22, 0, 7)), "Valid bank placement accepted");
        Assert(!s.TryPlaceTower(new Vector3(-22, 0, 7)), "Overlapping placement rejected");
        for (int z = -11; z <= 11; z += 2)
            for (int x = -22; x <= 22; x += 2)
                s.TryPlaceTower(new Vector3(x, 0, z));
        Assert(s.TowerCount == 64, "Tower cap holds");
        Assert(!s.TryPlaceTower(new Vector3(22, 0, 11)), "Additional tower rejected at cap");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(HordeSimulation);
        var spawn = type.GetMethod("Spawn", flags);
        var index = type.GetMethod("MoveAndIndex", flags);
        var attack = type.GetMethod("UpdateTowers", flags);
        for (int i = 0; i < HordeSimulation.Capacity; i++) spawn.Invoke(s, null);
        Assert(s.Alive == 4000 && s.AtCapacity, "Enemy capacity reached");
        index.Invoke(s, new object[] { 0f });
        attack.Invoke(s, new object[] { 1f });
        Assert(s.Killed > 0 && !s.AtCapacity, "Turrets kill targets and release slots");
        Assert(s.Spawned == s.Alive + s.Killed + s.Escaped, "Accounting after attack");
        index.Invoke(s, new object[] { 100f });
        Assert(s.Alive == 0, "Exit removes all remaining enemies");
        int kills = s.Killed;
        attack.Invoke(s, new object[] { 1f });
        Assert(s.Killed == kills, "No attacks against removed targets");
        Assert(s.Spawned == s.Killed + s.Escaped, "Accounting after exit");
        spawn.Invoke(s, null);
        Assert(s.Alive == 1, "Freed slot reused");
        s.ResetEnemies();
        Assert(s.Alive == 0 && s.Spawned == 0 && s.Killed == 0 && s.Escaped == 0, "Reset clears counters");
        s.ClearTowers();
        s.TryPlaceTower(new Vector3(-12, 0, -8));
        s.TryPlaceTower(new Vector3(-4, 0, 8));
        s.TryPlaceTower(new Vector3(4, 0, -8));
        s.TryPlaceTower(new Vector3(12, 0, 8));
        s.SetSpawnRate(300);
        return "PASS: placement restrictions, 64-tower limit, 4000-enemy limit, firing, slot reuse, exit cleanup, counter conservation and reset.";
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
    }
}
