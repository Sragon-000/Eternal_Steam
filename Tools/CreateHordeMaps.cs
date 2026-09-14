using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam.Demo;

public static class CreateHordeMaps
{
    const string Folder = "Assets/EternalSteam/Scene/Demo/";
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the active scene first.");
        string original = Folder + "HordeDemo.unity";
        foreach (var kind in new[] { HordeMapKind.WideFront, HordeMapKind.Pincer })
        {
            string path = Folder + "" + HordeMapLayout.SceneName(kind) + ".unity";
            if (System.IO.File.Exists(path)) throw new InvalidOperationException("Scene already exists: " + path);
        }
        foreach (var kind in new[] { HordeMapKind.WideFront, HordeMapKind.Pincer })
        {
            var scene = EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponent<MeshRenderer>() != null) UnityEngine.Object.DestroyImmediate(root);
            var sim = UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
            sim.ConfigureMap(kind);
            EditorUtility.SetDirty(sim);
            var camera = Camera.main;
            camera.transform.position = new Vector3(0, 64, -48);
            camera.transform.LookAt(Vector3.zero);
            camera.orthographicSize = 38;
            camera.farClipPlane = 200;
            Box("Ground", new Vector3(0, -0.25f, 0), new Vector3(86, 0.4f, 64), "Ground");
            Box("Battlefield", new Vector3(0, -0.025f, 0), new Vector3(83, 0.05f, 60), "Lane");
            foreach (var rect in HordeMapLayout.BuildZones(kind))
                Box("Blue deployment zone", new Vector3(rect.center.x, 0.015f, rect.center.y), new Vector3(rect.width, 0.06f, rect.height), "Bank");
            if (kind == HordeMapKind.WideFront)
            {
                Box("Wide spawn front", new Vector3(-41, 0.06f, 0), new Vector3(0.18f, 0.08f, 40), "InvalidPlacement");
                Box("Exit", new Vector3(41, 0.06f, 0), new Vector3(0.18f, 0.08f, 38), "ValidPlacement");
                for (int z = -18; z <= 18; z += 6) Arrow(new Vector3(-35, 0.08f, z), Vector3.right);
            }
            else
            {
                Box("North spawn", new Vector3(-26, 0.06f, 29), new Vector3(28, 0.08f, 0.18f), "InvalidPlacement");
                Box("South spawn", new Vector3(-26, 0.06f, -29), new Vector3(28, 0.08f, 0.18f), "InvalidPlacement");
                Box("Exit", new Vector3(41, 0.06f, 0), new Vector3(0.18f, 0.08f, 26), "ValidPlacement");
                for (int x = -36; x <= -12; x += 8)
                {
                    Arrow(new Vector3(x, 0.08f, 25), new Vector3(1, 0, -0.45f).normalized);
                    Arrow(new Vector3(x, 0.08f, -25), new Vector3(1, 0, 0.45f).normalized);
                }
            }
            EditorSceneManager.SaveScene(scene, Folder + "" + HordeMapLayout.SceneName(kind) + ".unity");
        }
        var paths = new[] { original, Folder + "HordeWideFront.unity", Folder + "HordePincer.unity" };
        var existing = EditorBuildSettings.scenes.Where(s => !paths.Contains(s.path));
        EditorBuildSettings.scenes = paths.Select(p => new EditorBuildSettingsScene(p, true)).Concat(existing).ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(paths[1]);
        return "Created WideFront and Pincer; registered all three demo scenes for runtime switching.";
    }

    public static string DemoMaterialPath(string name)
    {
        string folder = name == "Tower" || name == "Barrel" ? "Content/Buildings/HordeTowers/Art/Materials"
            : name == "Enemy" ? "Content/Enemies/HordeEnemy/Art/Materials"
            : name == "Tracer" || name == "ValidPlacement" || name == "InvalidPlacement" ? "Shared/Materials/Demo"
            : "Content/Environments/HordeMaps/Art/Materials";
        return "Assets/EternalSteam/" + folder + "/" + name + ".mat";
    }

    static void Box(string name, Vector3 position, Vector3 scale, string material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(DemoMaterialPath(material));
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    static void Arrow(Vector3 origin, Vector3 direction)
    {
        var line = new GameObject("Advance direction").AddComponent<LineRenderer>();
        line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(DemoMaterialPath("Markings"));
        line.widthMultiplier = 0.12f;
        line.positionCount = 5;
        var tip = origin + direction * 3;
        var right = Vector3.Cross(Vector3.up, direction);
        line.SetPositions(new[] { origin, tip, tip - direction - right * 0.7f, tip, tip - direction + right * 0.7f });
    }
}
