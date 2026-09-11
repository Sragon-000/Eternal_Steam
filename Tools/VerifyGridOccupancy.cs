using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using EternalSteam.Demo;

public static class VerifyGridOccupancy
{
    public static string Main()
    {
        if (!Application.isPlaying) throw new Exception("Run in Play mode.");
        var s = UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
        var original = s.MapKind;
        try
        {
            foreach (HordeMapKind map in Enum.GetValues(typeof(HordeMapKind)))
            {
                s.ClearTowers(); s.ConfigureMap(map); s.ResetEnemies();
                var grid = HordeMapLayout.GridBounds(HordeMapLayout.BuildZones(map)[0]);
                var start = new Vector3(grid.xMin + HordeMapLayout.BuildMargin, 0, grid.yMin + HordeMapLayout.BuildMargin);
                for (int z = 0; z < 2; z++)
                    for (int x = 0; x < 3; x++)
                    {
                        var p = start + new Vector3(x, 0, z) * HordeMapLayout.BuildCellSize;
                        Check(s.TryPlaceTower(p, Vector3.forward, 12, (HordeTowerKind)x), "Adjacent cell rejected: " + map + " " + p);
                        Check(!s.TryPlaceTower(p + new Vector3(0.3f, 0, 0.3f), Vector3.forward, 12), "Duplicate in same cell accepted");
                    }
                Check(s.TowerCount == 6, "Six adjacent cells should fit");
                var towers = (IList)typeof(HordeSimulation).GetField("towers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s);
                var bases = new List<Bounds>();
                foreach (var tower in towers)
                {
                    var root = (GameObject)tower.GetType().GetField("root").GetValue(tower);
                    var b = root.transform.Find("Base").GetComponent<Renderer>().bounds;
                    Check(b.size.x > 1.35f && b.size.x < HordeMapLayout.BuildCellSize, "Base must be larger but fit one cell");
                    Check(Mathf.Abs(b.min.y - HordeMapLayout.SurfaceHeight(map)) < 0.001f, "Base height does not meet floor");
                    foreach (var other in bases) Check(!b.Intersects(other), "Adjacent bases overlap");
                    bases.Add(b);
                }
            }
            return "PASS: six adjacent cells on each map, all three tower types, same-cell rejection, enlarged bases fit without overlap and meet floor.";
        }
        finally
        {
            s.ClearTowers(); s.ConfigureMap(original); s.ResetEnemies(); s.SelectTower(HordeTowerKind.MachineGun);
            int i = 0;
            foreach (var p in HordeMapLayout.StartingTowers(original)) s.TryPlaceTower(p, HordeMapLayout.DefaultDirection(original, p), HordeSimulation.Range, (HordeTowerKind)(i++ % 3));
        }
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
