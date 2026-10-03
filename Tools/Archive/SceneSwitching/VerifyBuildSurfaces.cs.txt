using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam.Demo;

public static class VerifyBuildSurfaces
{
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new Exception("Run in edit mode.");
        var reports = new List<string>();
        foreach (HordeMapKind kind in Enum.GetValues(typeof(HordeMapKind)))
        {
            string path = "Assets/EternalSteam/Scene/Demo/" + HordeMapLayout.SceneName(kind) + ".unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var renderers = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<MeshRenderer>()).ToArray();
                var ground = renderers.Single(r => r.name == "Ground").bounds;
                var banks = renderers.Where(r => r.name.Contains("Build Bank") || r.name == "Blue deployment zone").ToArray();
                if (banks.Length != HordeMapLayout.BuildZones(kind).Length) throw new Exception("Missing deployment floor: " + kind);
                int count = 0;
                foreach (Rect zone in HordeMapLayout.BuildZones(kind))
                {
                    var grid = HordeMapLayout.GridBounds(zone);
                    var bank = banks.SingleOrDefault(r => Mathf.Abs(r.bounds.center.x-zone.center.x)<0.01f && Mathf.Abs(r.bounds.center.z-zone.center.y)<0.01f);
                    if (bank == null) throw new Exception("Missing floor for zone: " + zone);
                    var bounds = bank.bounds;
                    if (Mathf.Abs(bounds.max.y - HordeMapLayout.SurfaceHeight(kind)) > 0.001f) throw new Exception("Tower/grid height mismatch: " + kind);
                    CheckRect(grid, bounds, 0, "Grid extends past blue floor");
                    CheckRect(grid, ground, 0, "Grid extends past ground");
                    for (float x = grid.xMin + HordeMapLayout.BuildMargin; x < grid.xMax; x += HordeMapLayout.BuildCellSize)
                        for (float z = grid.yMin + HordeMapLayout.BuildMargin; z < grid.yMax; z += HordeMapLayout.BuildCellSize)
                        {
                            var footprint = new Rect(x - HordeMapLayout.TowerHalfWidth, z - HordeMapLayout.TowerHalfWidth, HordeMapLayout.TowerHalfWidth * 2, HordeMapLayout.TowerHalfWidth * 2);
                            CheckRect(footprint, bounds, 0, "Unsupported tower base");
                            CheckRect(footprint, ground, 0, "Tower extends past ground");
                            count++;
                        }
                }
                reports.Add(kind + ": " + count + " cells; grid and entire tower bases supported, height aligned");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        return "PASS\n" + string.Join("\n", reports);
    }

    static void CheckRect(Rect r, Bounds b, float extra, string message)
    {
        if (r.xMin-extra < b.min.x-0.001f || r.xMax+extra > b.max.x+0.001f || r.yMin-extra < b.min.z-0.001f || r.yMax+extra > b.max.z+0.001f)
            throw new Exception(message + ": " + r);
    }
}
