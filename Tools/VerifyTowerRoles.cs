using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using EternalSteam.Demo;

public static class VerifyTowerRoles
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static HordeSimulation s;
    static Array enemies;
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Run in Play mode.");
        s = UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
        var original = s.MapKind;
        enemies = (Array)typeof(HordeSimulation).GetField("enemies", Private).GetValue(s);
        try
        {
            s.ConfigureMap(HordeMapKind.Lane);
            foreach (var kind in new[] { HordeTowerKind.Cannon, HordeTowerKind.Frost })
            {
                s.ClearTowers(); s.ResetEnemies(); s.SelectTower(kind);
                Check(s.TryPlaceTower(new Vector3(0, 0, -8), Vector3.forward, 12), "Place selected type");
                var tower = ((IList)typeof(HordeSimulation).GetField("towers", Private).GetValue(s))[0];
                Check((HordeTowerKind)tower.GetType().GetField("kind").GetValue(tower) == kind, "Selection saved on tower");
                s.ToggleSpawning(); Invoke("UpdateTowers", 1f);
                var tracer = (LineRenderer)tower.GetType().GetField("tracer").GetValue(tower);
                var head = (Transform)tower.GetType().GetField("head").GetValue(tower);
                var direction = (tracer.GetPosition(1) - head.position).normalized;
                s.ResetEnemies();
                var center = new Vector3(0, 0.55f, -8) + direction * 6;
                AddEnemy(0, center); AddEnemy(1, center + Vector3.right); AddEnemy(2, center + Vector3.left);
                AddEnemy(3, center + Vector3.right * 8);
                if (kind == HordeTowerKind.Frost)
                {
                    AddEnemy(4, new Vector3(0, 0.55f, 2)); // Behind the front enemies, still in the cone.
                    AddEnemy(5, new Vector3(0, 0.55f, -11)); // Behind the turret.
                    AddEnemy(6, new Vector3(0, 0.55f, 5)); // Beyond the configured 12-unit range.
                }
                Invoke("MoveAndIndex", 0f);
                s.ToggleSpawning(); Invoke("UpdateTowers", 1f);
                if (kind == HordeTowerKind.Cannon)
                    Check(s.Killed == 3 && Alive(3), "Cannon kills cluster but not outside blast");
                else
                {
                    Check(s.Killed == 0 && s.Alive == 7, "Frost deals no lethal damage");
                    Check((float)Get(0, "slowTime") == 2.5f && (float)Get(4, "slowTime") == 2.5f, "Frost pierces front enemies to reach distant enemies");
                    Check((float)Get(3, "slowTime") == 0 && (float)Get(5, "slowTime") == 0 && (float)Get(6, "slowTime") == 0, "Frost excludes sides, rear and beyond range");
                    var before = (Vector3)Get(0, "position");
                    Invoke("MoveAndIndex", 0.5f);
                    Check(Mathf.Abs(Vector3.Distance(before, (Vector3)Get(0, "position")) - 0.4f) < 0.001f, "60 percent slow changes movement");
                    Invoke("MoveAndIndex", 3f);
                    Check((float)Get(0, "slowTime") == 0, "Slow expires");
                }
            }
            s.ClearTowers(); s.ResetEnemies();
            AddEnemy(0, Vector3.zero); Set(0, "destination", Vector3.zero);
            Invoke("MoveAndIndex", 0f);
            Check(s.Health == HordeSimulation.MaxHealth - 1 && s.Escaped == 1, "One escape costs one health");
            s.ResetEnemies();
            for (int i = 0; i < HordeSimulation.MaxHealth + 1; i++) { AddEnemy(i, Vector3.zero); Set(i, "destination", Vector3.zero); }
            s.ToggleSpawning(); Invoke("MoveAndIndex", 0f);
            Check(s.Defeated && !s.Spawning && s.Health == 0 && s.Alive == 1, "Defeat stops movement immediately");
            Invoke("MoveAndIndex", 5f); Invoke("Spawn"); s.ToggleSpawning();
            Check(s.Alive == 1 && !s.Spawning && s.Escaped == HordeSimulation.MaxHealth, "No progress after defeat");
            Check(!s.BeginPlacement(new Vector3(0, 0, -8)), "Placement blocked after defeat");
            s.ResetEnemies();
            Check(!s.Defeated && !s.WaveStarted && s.Health == HordeSimulation.MaxHealth && s.Alive == 0, "Restart restores full health and deployment");
            return "PASS: tower selection, cannon area kill/exclusion, frost cone penetration/front/side/rear/range checks, slow/no damage/expiry, escape damage, defeat freeze, restart.";
        }
        finally
        {
            s.ClearTowers(); s.ConfigureMap(original); s.ResetEnemies(); s.SelectTower(HordeTowerKind.MachineGun);
            int i = 0;
            foreach (var p in HordeMapLayout.StartingTowers(original)) s.TryPlaceTower(p, HordeMapLayout.DefaultDirection(original, p), HordeSimulation.Range, (HordeTowerKind)(i++ % 3));
        }
    }
    static void AddEnemy(int id, Vector3 p) { Invoke("Spawn"); Set(id, "position", p); Set(id, "destination", p + Vector3.right * 100); Set(id, "speed", 2f); }
    static object Get(int id, string field) { var e = enemies.GetValue(id); return e.GetType().GetField(field).GetValue(e); }
    static void Set(int id, string field, object value) { var e = enemies.GetValue(id); e.GetType().GetField(field).SetValue(e, value); enemies.SetValue(e, id); }
    static bool Alive(int id) => (bool)Get(id, "alive");
    static void Invoke(string name, params object[] args) => typeof(HordeSimulation).GetMethod(name, Private).Invoke(s, args);
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
