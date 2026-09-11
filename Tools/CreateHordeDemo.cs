using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam.Demo;

public static class CreateHordeDemo
{
    public static string Main()
    {
        const string folder = "Assets/HordeDemo";
        const string scenePath = folder + "/Scenes/HordeDemo.unity";
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Active scene has unsaved changes; save it before generating a demo.");
        if (File.Exists(scenePath)) throw new InvalidOperationException("Demo already exists; open it rather than overwriting it.");
        if (!AssetDatabase.IsValidFolder(folder + "/Materials")) AssetDatabase.CreateFolder(folder, "Materials");
        if (!AssetDatabase.IsValidFolder(folder + "/Scenes")) AssetDatabase.CreateFolder(folder, "Scenes");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var ground = Material("Ground", new Color(0.075f, 0.11f, 0.16f));
        var lane = Material("Lane", new Color(0.16f, 0.20f, 0.26f));
        var bank = Material("Bank", new Color(0.10f, 0.27f, 0.34f));
        var enemy = Material("Enemy", new Color(1f, 0.24f, 0.12f));
        enemy.enableInstancing = true;
        var tower = Material("Tower", new Color(0.2f, 0.82f, 0.95f));
        var barrel = Material("Barrel", new Color(0.16f, 0.24f, 0.31f));
        var tracer = Material("Tracer", new Color(1f, 0.83f, 0.25f), true);
        var valid = Material("ValidPlacement", new Color(0.2f, 0.95f, 0.67f), true);
        var invalid = Material("InvalidPlacement", new Color(1f, 0.2f, 0.2f), true);
        var markings = Material("Markings", new Color(0.32f, 0.46f, 0.55f), true);
        Box("Ground", new Vector3(0, -0.25f, 0), new Vector3(54, 0.4f, 26), ground);
        Box("Enemy Lane", new Vector3(0, -0.025f, 0), new Vector3(52, 0.05f, 12), lane);
        Box("North Build Bank", new Vector3(0, -0.02f, 9), new Vector3(48, 0.06f, 6), bank);
        Box("South Build Bank", new Vector3(0, -0.02f, -9), new Vector3(48, 0.06f, 6), bank);
        Box("Spawn Line", new Vector3(-25, 0.03f, 0), new Vector3(0.15f, 0.07f, 12), invalid);
        Box("Exit Line", new Vector3(25, 0.03f, 0), new Vector3(0.15f, 0.07f, 12), valid);
        for (int x = -22; x <= 22; x += 4)
            Box("Lane Marker", new Vector3(x, 0.02f, 0), new Vector3(1.3f, 0.04f, 0.06f), markings);
        var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 38, -29);
        camera.transform.LookAt(Vector3.zero);
        camera.orthographic = true;
        camera.orthographicSize = 20;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 150;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.025f, 0.04f, 0.065f);
        var light = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.8f;
        light.transform.rotation = Quaternion.Euler(50, -35, 0);
        light.shadows = LightShadows.Soft;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.65f, 0.72f, 0.82f);
        var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        var mesh = UnityEngine.Object.Instantiate(capsule.GetComponent<MeshFilter>().sharedMesh);
        UnityEngine.Object.DestroyImmediate(capsule);
        AssetDatabase.CreateAsset(mesh, folder + "/EnemyCapsule.asset");
        var simulation = new GameObject("Horde Simulation").AddComponent<HordeSimulation>();
        simulation.Configure(camera, mesh, enemy, tower, barrel, tracer, valid, invalid);
        var settings = ScriptableObject.CreateInstance<PanelSettings>();
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = new Vector2Int(1600, 900);
        settings.match = 0.5f;
        AssetDatabase.CreateAsset(settings, folder + "/UI/HordePanelSettings.asset");
        var hudObject = new GameObject("Demo HUD");
        var document = hudObject.AddComponent<UIDocument>();
        document.panelSettings = settings;
        document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folder + "/UI/HordeHud.uxml");
        hudObject.AddComponent<HordeHud>().Configure(simulation);
        EditorUtility.SetDirty(simulation);
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = simulation.gameObject;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(Vector3.zero, camera.transform.rotation, 35);
        return "Created " + scenePath + ". Ready for Play.";
    }

    static Material Material(string name, Color color, bool unlit = false)
    {
        var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("URP shader missing.");
        var material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        if (!unlit) material.SetFloat("_Smoothness", 0.2f);
        AssetDatabase.CreateAsset(material, "Assets/HordeDemo/Materials/" + name + ".mat");
        return material;
    }

    static void Box(string name, Vector3 position, Vector3 scale, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
    }
}
