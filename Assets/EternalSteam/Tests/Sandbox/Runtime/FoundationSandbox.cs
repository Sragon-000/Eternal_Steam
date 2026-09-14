using System;
using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam
{
    public sealed class FoundationSandbox : MonoBehaviour
    {
        public BuildingCatalog Catalog;
        public Material SurfaceMaterial;
        public Material LineMaterial;
        public Camera ViewCamera;
        public SandboxSession Session { get; private set; }
        public UnityBuildingFactory Factory { get; private set; }
        public IReadOnlyList<BuildingDefinition> Available => available;
        readonly List<BuildingDefinition> available = new();
        readonly List<SampleTarget> targets = new();
        TargetRegistry registry;
        Transform sceneRoot;
        float combatTime;
        public int Remaining { get; private set; }
        public int Escaped { get; private set; }
        public string Error { get; private set; }

        void Awake() => ResetSample();
        public void ResetSample()
        {
            Session?.Dispose();
            Session = null;
            if (sceneRoot != null) { sceneRoot.gameObject.SetActive(false); Destroy(sceneRoot.gameObject); }
            targets.Clear(); available.Clear();
            var errors = Catalog == null ? new List<string> { "Catalog is missing." } : Catalog.Validate();
            if (SurfaceMaterial == null || LineMaterial == null || ViewCamera == null) errors.Add("Sample view references are missing.");
            if (errors.Count > 0) { Error = string.Join("\n", errors); Debug.LogError(Error); return; }
            Error = null;
            available.AddRange(Catalog.Buildings);
            registry = new TargetRegistry();
            sceneRoot = new GameObject("Sample runtime").transform;
            sceneRoot.SetParent(transform);
            var grid = new BuildGrid(new RectInt(-6, -5, 12, 5), 2);
            Factory = new UnityBuildingFactory(sceneRoot, new BuildingServices(registry), grid.CellSize, LineMaterial);
            Session = new SandboxSession(new BuildingWorld(grid, Factory), new PreparationOnly());
            for (int z = grid.Bounds.yMin; z < grid.Bounds.yMax; z++)
                for (int x = grid.Bounds.xMin; x < grid.Bounds.xMax; x++)
                {
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.name = "Build cell"; tile.transform.SetParent(sceneRoot);
                    tile.transform.position = grid.Center(new Vector2Int(x,z), Vector2Int.one) - Vector3.up * 0.15f;
                    tile.transform.localScale = new Vector3(1.92f, 0.2f, 1.92f);
                    tile.GetComponent<Renderer>().sharedMaterial = SurfaceMaterial;
                    var props = new MaterialPropertyBlock(); props.SetColor("_BaseColor", new Color(0.09f,0.27f,0.22f)); tile.GetComponent<Renderer>().SetPropertyBlock(props);
                }
            combatTime = 0; Remaining = Escaped = 0;
        }
        public bool StartCombat()
        {
            if (Session == null || !Session.StartCombat()) return false;
            for (int i = 0; i < 8; i++)
            {
                var position = new Vector3((i % 4 - 1.5f) * 3, i < 4 ? 0.7f : 2.5f, i < 4 ? 4 : 8);
                var target = new SampleTarget { Position = position, Kind = i < 4 ? TargetKind.Ground : TargetKind.Air };
                target.Handle = registry.Register(i, position, target.Kind, target);
                target.View = GameObject.CreatePrimitive(i < 4 ? PrimitiveType.Capsule : PrimitiveType.Sphere);
                target.View.transform.SetParent(sceneRoot); target.View.transform.position = position;
                target.View.GetComponent<Renderer>().sharedMaterial = SurfaceMaterial;
                var props = new MaterialPropertyBlock(); props.SetColor("_BaseColor", i < 4 ? new Color(1,0.45f,0.3f) : new Color(0.7f,0.55f,1));
                target.View.GetComponent<Renderer>().SetPropertyBlock(props);
                targets.Add(target);
            }
            Remaining = targets.Count;
            return true;
        }
        void Update()
        {
            if (Session == null || Session.Phase != SandboxPhase.Combat) return;
            combatTime += Time.deltaTime;
            Session.Tick(Time.deltaTime);
            Remaining = 0;
            foreach (var target in targets)
            {
                if (target.Resolved) continue;
                if (!target.Alive || combatTime >= 30)
                {
                    if (target.Alive) Escaped++;
                    target.Resolved = true;
                    registry.Unregister(target.Handle);
                    target.View.SetActive(false);
                }
                else Remaining++;
            }
            if (Remaining == 0) Session.CompleteCombat();
        }
        void OnDestroy() { Session?.Dispose(); }
        sealed class SampleTarget : IDamageReceiver
        {
            public float Health = 30;
            public Vector3 Position;
            public TargetKind Kind;
            public TargetHandle Handle;
            public GameObject View;
            public bool Resolved;
            public bool Alive => Health > 0 && !Resolved;
            public void ApplyDamage(float amount) { if (Alive) Health = Mathf.Max(0, Health - amount); }
        }
    }
}
