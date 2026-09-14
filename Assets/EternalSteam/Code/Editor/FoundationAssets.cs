using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace EternalSteam.Editor
{
    public static class FoundationAssets
    {
        public const string Root = "Assets/EternalSteam";
        public const string ScenePath = Root + "/Scene/Tests/FoundationSandbox.unity";
        static readonly Dictionary<string, string> SamplePaths = new()
        {
            { "SampleLine.mat", "Assets/EternalSteam/Shared/Materials/Foundation/SampleLine.mat" },
            { "SampleSurface.mat", "Assets/EternalSteam/Shared/Materials/Foundation/SampleSurface.mat" },
            { "SamplePanel.asset", "Assets/EternalSteam/Settings/UI/SamplePanel.asset" },
            { "SampleCatalog.asset", "Assets/EternalSteam/Settings/Catalogs/SampleCatalog.asset" },
            { "Health.asset", "Assets/EternalSteam/Content/Buildings/Common/Data/Health.asset" },
            { "PlainView.prefab", "Assets/EternalSteam/Content/Buildings/Common/Prefabs/PlainView.prefab" },
            { "NearestInstantAttack.asset", "Assets/EternalSteam/Content/Buildings/BasicTower/Data/NearestInstantAttack.asset" },
            { "SingleTower.asset", "Assets/EternalSteam/Content/Buildings/BasicTower/Data/SingleTower.asset" },
            { "TowerView.prefab", "Assets/EternalSteam/Content/Buildings/BasicTower/Prefabs/TowerView.prefab" },
            { "DurableBuilding.asset", "Assets/EternalSteam/Content/Buildings/DurableBuilding/Data/DurableBuilding.asset" },
            { "EmptyBuilding.asset", "Assets/EternalSteam/Content/Buildings/EmptyBuilding/Data/EmptyBuilding.asset" }
        };
        [MenuItem("Eternal Steam/Foundation/Create Sample Assets")]
        public static void Create()
        {
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                throw new InvalidOperationException("Open or save a scene before creating sample assets.");
            if (File.Exists(ScenePath) || SamplePaths.Values.Any(File.Exists))
                throw new InvalidOperationException("Sample already exists. Existing assets were not overwritten.");
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            foreach (var path in SamplePaths.Values) Directory.CreateDirectory(Path.GetDirectoryName(path));
            AssetDatabase.Refresh();
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) throw new InvalidOperationException("URP Unlit shader is missing.");
            var surface = new Material(shader) { name = "Sample Surface" };
            surface.SetColor("_BaseColor", Color.white);
            surface.SetFloat("_Surface", 1);
            surface.SetFloat("_Blend", 0);
            surface.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            surface.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            surface.SetFloat("_ZWrite", 0);
            surface.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            surface.SetOverrideTag("RenderType", "Transparent");
            surface.renderQueue = (int)RenderQueue.Transparent;
            Save(surface, "SampleSurface.mat");
            var lineShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var line = new Material(lineShader != null ? lineShader : shader) { name = "Sample Line" };
            line.SetColor("_BaseColor", Color.white);
            Save(line, "SampleLine.mat");
            var plainPrefab = Prefab("PlainView", surface, false);
            var towerPrefab = Prefab("TowerView", surface, true);
            var health = ScriptableObject.CreateInstance<HealthModuleDefinition>();
            health.Maximum = 100; Save(health, "Health.asset");
            var attack = ScriptableObject.CreateInstance<AttackModuleDefinition>();
            Save(attack, "NearestInstantAttack.asset");
            var plain = Definition("sample.empty", "기능 없는 건물", plainPrefab, new Vector2Int(1,1));
            Save(plain, "EmptyBuilding.asset");
            var wall = Definition("sample.durable", "내구도 건물", plainPrefab, new Vector2Int(2,1));
            wall.Modules.Add(health); Save(wall, "DurableBuilding.asset");
            var tower = Definition("sample.tower", "단일 공격 포탑", towerPrefab, new Vector2Int(1,1));
            tower.Category = BuildingCategory.Defense; tower.Modules.Add(health); tower.Modules.Add(attack);
            Save(tower, "SingleTower.asset");
            var catalog = ScriptableObject.CreateInstance<BuildingCatalog>();
            catalog.Buildings.AddRange(new[] { plain, wall, tower }); Save(catalog, "SampleCatalog.asset");
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize; panel.referenceResolution = new Vector2Int(1280, 800);
            // Copy the already-authored project theme/text settings rather than relying on an implicit default.
            var existingPanel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/EternalSteam/Settings/UI/HordePanelSettings.asset");
            if (existingPanel != null) { panel.themeStyleSheet = existingPanel.themeStyleSheet; panel.textSettings = existingPanel.textSettings; }
            Save(panel, "SamplePanel.asset");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var cameraObject = new GameObject("Foundation Camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 32, -18);
                camera.transform.rotation = Quaternion.Euler(65, 0, 0);
                camera.orthographic = true; camera.orthographicSize = 17;
                camera.rect = new Rect(0.27f, 0, 0.73f, 1);
                camera.backgroundColor = new Color(0.025f,0.045f,0.06f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                cameraObject.tag = "MainCamera";
                var root = new GameObject("Foundation Sandbox");
                var sample = root.AddComponent<FoundationSandbox>();
                sample.Catalog = catalog; sample.SurfaceMaterial = surface; sample.LineMaterial = line; sample.ViewCamera = camera;
                var uiObject = new GameObject("Foundation UI");
                var document = uiObject.AddComponent<UIDocument>();
                document.panelSettings = panel;
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/EternalSteam/Shared/UI/Foundation/FoundationHud.uxml");
                var input = root.AddComponent<SandboxInput>(); input.Sample = sample; input.Document = document;
                var presentation = root.AddComponent<SandboxPresentation>(); presentation.Sample = sample; presentation.Input = input;
                var hud = uiObject.AddComponent<FoundationHud>(); hud.Sample = sample; hud.Input = input;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Foundation sample created: " + ScenePath);
        }
        static void Save(UnityEngine.Object asset, string name) => AssetDatabase.CreateAsset(asset, SamplePaths[name]);
        static BuildingDefinition Definition(string id, string title, GameObject prefab, Vector2Int footprint)
        {
            var definition = ScriptableObject.CreateInstance<BuildingDefinition>();
            definition.Id = id; definition.DisplayName = title; definition.ViewPrefab = prefab; definition.Footprint = footprint;
            return definition;
        }
        static GameObject Prefab(string name, Material material, bool tower)
        {
            var root = new GameObject(name);
            try
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = "Body";
                body.transform.SetParent(root.transform); body.transform.localPosition = Vector3.up * 0.6f;
                body.transform.localScale = new Vector3(1.5f,1.2f,1.5f);
                body.GetComponent<Renderer>().sharedMaterial = material;
                if (tower)
                {
                    var pivot = new GameObject("AttackPivot"); pivot.transform.SetParent(root.transform, false);
                    var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    barrel.name = "Barrel";
                    barrel.transform.SetParent(pivot.transform); barrel.transform.localPosition = new Vector3(0,1.1f,0.65f);
                    barrel.transform.localScale = new Vector3(0.3f,0.3f,1.3f);
                    barrel.GetComponent<Renderer>().sharedMaterial = material;
                }
                return PrefabUtility.SaveAsPrefabAsset(root, SamplePaths[name + ".prefab"]);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }

    [CustomEditor(typeof(BuildingCatalog))]
    public sealed class CatalogInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            foreach (var error in ((BuildingCatalog)target).Validate()) EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
    [CustomEditor(typeof(BuildingDefinition))]
    public sealed class BuildingInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            foreach (var error in ((BuildingDefinition)target).Validate()) EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }
    [InitializeOnLoad]
    public static class FoundationPlayGuard
    {
        static FoundationPlayGuard() => EditorApplication.playModeStateChanged += Check;
        static void Check(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.ExitingEditMode) return;
            foreach (var sample in UnityEngine.Object.FindObjectsByType<FoundationSandbox>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var errors = sample.Catalog == null ? new List<string> { "Catalog missing." } : sample.Catalog.Validate();
                if (sample.SurfaceMaterial == null || sample.LineMaterial == null || sample.ViewCamera == null) errors.Add("Sample view references missing.");
                if (errors.Count == 0) continue;
                Debug.LogError("Foundation configuration invalid:\n" + string.Join("\n", errors), sample);
                EditorApplication.isPlaying = false;
            }
        }
    }
}
