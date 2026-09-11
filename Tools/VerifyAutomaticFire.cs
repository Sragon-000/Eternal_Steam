using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using EternalSteam.Demo;

public static class VerifyAutomaticFire
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static string Main()
    {
        var s = UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
        if (!Application.isPlaying || s == null) throw new Exception("Run in Play mode.");
        var original = s.MapKind;
        try
        {
            s.ClearTowers();
            s.SelectTower(HordeTowerKind.MachineGun);
            s.ConfigureMap(HordeMapKind.Lane);
            s.ResetEnemies();
            Check(s.BeginPlacement(new Vector3(-10, 0, -8)), "Position selection");
            Check(s.ConfirmPlacement(Vector3.forward, 6), "Near setting saved");
            Check(s.TryPlaceTower(new Vector3(10, 0, -8), Vector3.forward, 26), "Far setting saved");
            var towers = (IList)typeof(HordeSimulation).GetField("towers", Private).GetValue(s);
            float lastWidth = float.MaxValue;
            for (float range = 6; range <= 26; range++)
            {
                float width = 2 * range * Mathf.Sin(HordeSimulation.SpreadHalfAngle(range) * Mathf.Deg2Rad);
                Check(width < lastWidth, "Farther settings must be physically narrower");
                lastWidth = width;
            }
            Attack(s);
            foreach (var t in towers)
                Check(!Ray(t).enabled, "Deployment must stay quiet");
            s.ToggleSpawning();
            Attack(s);
            Check(s.Alive == 0 && s.Killed == 0, "Empty arena");
            foreach (var t in towers)
            {
                var type = t.GetType();
                float range = (float)type.GetField("range").GetValue(t);
                var head = (Transform)type.GetField("head").GetValue(t);
                var ray = Ray(t);
                    Check(ray.enabled, "Fires without enemies");
                    Check(Mathf.Abs((ray.GetPosition(1) - head.position).magnitude - range) < 0.001f, "Full configured range");
                    Check(Vector3.Angle(head.forward, ray.GetPosition(1) - head.position) <= HordeSimulation.SpreadHalfAngle(range) + 0.01f, "Bullet inside cone");
            }
            var first = Ray(towers[0]).GetPosition(1);
            Attack(s);
            Check((first - Ray(towers[0]).GetPosition(1)).sqrMagnitude > 0.0001f, "Spread varies between shots");
            Check((int)towers[0].GetType().GetField("shot").GetValue(towers[0]) == 2, "Exactly one shot per fire interval");
            s.ResetEnemies();
            foreach (var t in towers)
                Check(!Ray(t).enabled, "Reset clears fire");
            return "PASS: saved ranges, monotonically narrower spread, quiet deployment, continuous empty-arena firing, single full-length bullet, cone bounds, varying spray, reset.";
        }
        finally
        {
            s.ClearTowers(); s.ConfigureMap(original); s.ResetEnemies();
            foreach (var p in HordeMapLayout.StartingTowers(original)) s.TryPlaceTower(p);
        }
    }
    static LineRenderer Ray(object tower) => (LineRenderer)tower.GetType().GetField("tracer").GetValue(tower);
    static void Attack(HordeSimulation s) => typeof(HordeSimulation).GetMethod("UpdateTowers", Private).Invoke(s, new object[] { 1f });
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
