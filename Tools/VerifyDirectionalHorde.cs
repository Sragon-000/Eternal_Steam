using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using EternalSteam.Demo;

public static class VerifyDirectionalHorde
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Run in Play mode.");
        var s = UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
        var original = s.MapKind;
        try
        {
            s.ConfigureMap(HordeMapKind.Lane);
            s.SelectTower(HordeTowerKind.MachineGun);
            s.ClearTowers();
            s.ResetEnemies();
            Check(!s.Spawning && !s.WaveStarted && s.Alive == 0, "Starts in deployment");
            Check(s.BeginPlacement(new Vector3(0, 0, -8)), "First click accepts position");
            Check(s.TowerCount == 0 && s.ChoosingDirection, "First click does not install");
            s.CancelPlacement();
            Check(s.TowerCount == 0 && !s.ChoosingDirection, "Cancel consumes no tower");
            Check(!s.BeginPlacement(Vector3.zero), "Lane is not buildable");
            s.BeginPlacement(new Vector3(0, 0, -8));
            Check(!s.ConfirmPlacement(Vector3.zero), "Zero direction rejected");
            Check(s.ConfirmPlacement(Vector3.forward), "Second click locks direction");
            Check(!s.ConfirmPlacement(Vector3.forward), "No duplicate confirmation");
            var towers = (IList)typeof(HordeSimulation).GetField("towers", Private).GetValue(s);
            var tower = towers[0];
            var head = (Transform)tower.GetType().GetField("head").GetValue(tower);
            s.ToggleSpawning();
            Invoke(s, "UpdateTowers", 1f);
            var tracer = (LineRenderer)tower.GetType().GetField("tracer").GetValue(tower);
            var shotDirection = (tracer.GetPosition(1) - head.position).normalized;
            s.ResetEnemies();
            var enemies = (Array)typeof(HordeSimulation).GetField("enemies", Private).GetValue(s);
            SetEnemy(s, enemies, 0, new Vector3(0, 0.55f, -12)); // Behind
            SetEnemy(s, enemies, 1, new Vector3(10, 0.55f, -6)); // Outside spread
            SetEnemy(s, enemies, 2, new Vector3(0, 0.55f, 16)); // Beyond range
            SetEnemy(s, enemies, 3, new Vector3(0, 0.55f, -8) + shotDirection * 10);
            SetEnemy(s, enemies, 4, new Vector3(0, 0.55f, -8) + shotDirection * 5);
            Invoke(s, "MoveAndIndex", 0f);
            s.ToggleSpawning();
            Invoke(s, "UpdateTowers", 1f);
            Check(!Alive(enemies, 4) && Alive(enemies, 3), "One bullet hits nearest enemy only");
            tower.GetType().GetField("shot").SetValue(tower, 0);
            Invoke(s, "UpdateTowers", 1f);
            Invoke(s, "UpdateTowers", 1f);
            Check(s.Killed == 2 && Alive(enemies, 0) && Alive(enemies, 1) && Alive(enemies, 2), "Behind, side and distant enemies ignored");
            Check(Vector3.Dot(head.forward, Vector3.forward) > 0.9999f, "Head does not track targets");
            Check(s.Spawning && s.WaveStarted, "Start wave enabled explicitly");
            s.ResetEnemies();
            Check(!s.Spawning && !s.WaveStarted && s.TowerCount == 1, "Reset returns to deployment and keeps tower");
            foreach (HordeMapKind kind in Enum.GetValues(typeof(HordeMapKind)))
            {
                s.ClearTowers();
                s.ConfigureMap(kind);
                s.ResetEnemies();
                foreach (var p in HordeMapLayout.StartingTowers(kind)) Check(s.TryPlaceTower(p), "Default tower in build zone: " + kind);
                Check(!s.TryPlaceTower(new Vector3(100, 0, 100)), "Off-map rejected");
                for (int i = 0; i < HordeSimulation.Capacity + 1; i++) Invoke(s, "Spawn");
                Check(s.Alive == HordeSimulation.Capacity, "Capacity guarded: " + kind);
                Invoke(s, "MoveAndIndex", 100f);
                Check(s.Defeated && s.Escaped == HordeSimulation.MaxHealth && s.Alive == HordeSimulation.Capacity - HordeSimulation.MaxHealth, "Routes reach exit and stop on defeat: " + kind);
                Check(s.Spawned == s.Alive + s.Killed + s.Escaped, "Conserved counters: " + kind);
            }
            return "PASS: two-stage placement/cancel, fixed direction, nearest hit, behind/side/range exclusions, deployment/start/reset, all map build zones/routes/capacity.";
        }
        finally
        {
            s.ClearTowers();
            s.ConfigureMap(original);
            s.ResetEnemies();
            foreach (var p in HordeMapLayout.StartingTowers(original)) s.TryPlaceTower(p);
        }
    }

    static void SetEnemy(HordeSimulation s, Array array, int index, Vector3 position)
    {
        Invoke(s, "Spawn");
        object enemy = array.GetValue(index);
        enemy.GetType().GetField("position").SetValue(enemy, position);
        array.SetValue(enemy, index);
    }
    static bool Alive(Array array, int index) => (bool)array.GetValue(index).GetType().GetField("alive").GetValue(array.GetValue(index));
    static void Invoke(HordeSimulation s, string name, params object[] args) => typeof(HordeSimulation).GetMethod(name, Private).Invoke(s, args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception("FAIL: " + message); }
}
