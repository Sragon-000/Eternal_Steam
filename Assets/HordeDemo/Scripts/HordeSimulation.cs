using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace EternalSteam.Demo
{
    public sealed class HordeSimulation : MonoBehaviour
    {
        public const int Capacity = 4000;
        const int GridWidth = 28;
        const int GridHeight = 16;
        const float CellSize = 2f;
        const float Range = 10f;
        const float FireInterval = 0.065f;
        const int MaxTowers = 64;

        [SerializeField] Camera viewCamera;
        [SerializeField] Mesh enemyMesh;
        [SerializeField] Material enemyMaterial;
        [SerializeField] Material towerMaterial;
        [SerializeField] Material barrelMaterial;
        [SerializeField] Material tracerMaterial;
        [SerializeField] Material validMaterial;
        [SerializeField] Material invalidMaterial;
        [SerializeField, Range(10, 400)] int spawnRate = 100;

        struct Enemy
        {
            public Vector3 position;
            public float speed;
            public bool alive;
        }

        sealed class Tower
        {
            public GameObject root;
            public Transform head;
            public LineRenderer tracer;
            public float cooldown;
            public float tracerTime;
        }

        readonly Enemy[] enemies = new Enemy[Capacity];
        readonly int[] free = new int[Capacity];
        readonly int[] heads = new int[GridWidth * GridHeight];
        readonly int[] next = new int[Capacity];
        readonly Matrix4x4[] matrices = new Matrix4x4[500];
        readonly List<Tower> towers = new List<Tower>(MaxTowers);
        System.Random random;
        int freeCount;
        float spawnBudget;
        float smoothedDelta = 1f / 60f;
        Transform preview;
        Renderer previewRenderer;
        LineRenderer rangeRing;
        bool canPlace;
        Vector3 placement;
        int frameCount;
        float sampleSeconds;
        bool spawning = true;
        bool previousBackgroundSetting;

        public int Alive { get; private set; }
        public int Killed { get; private set; }
        public int Escaped { get; private set; }
        public int Spawned { get; private set; }
        public int TowerCount => towers.Count;
        public int SpawnRate => spawnRate;
        public bool Spawning => spawning;
        public bool AtCapacity => freeCount == 0;
        public float Fps => 1f / Mathf.Max(0.0001f, smoothedDelta);
        public bool PointerOverHud { get; set; }
        public string PlacementHint => towers.Count >= MaxTowers ? "Tower limit: 64" : "Click on the blue banks to place a turret";

        public void Configure(Camera camera, Mesh mesh, Material enemy, Material tower, Material barrel,
            Material tracer, Material valid, Material invalid)
        {
            viewCamera = camera;
            enemyMesh = mesh;
            enemyMaterial = enemy;
            towerMaterial = tower;
            barrelMaterial = barrel;
            tracerMaterial = tracer;
            validMaterial = valid;
            invalidMaterial = invalid;
        }

        void Awake()
        {
            previousBackgroundSetting = Application.runInBackground;
            Application.runInBackground = true;
        }

        void OnDestroy() => Application.runInBackground = previousBackgroundSetting;

        void Start()
        {
            ResetEnemies();
            CreatePreview();
            // A few example turrets make the firing loop visible immediately.
            TryPlaceTower(new Vector3(-12, 0, -8));
            TryPlaceTower(new Vector3(-4, 0, 8));
            TryPlaceTower(new Vector3(4, 0, -8));
            TryPlaceTower(new Vector3(12, 0, 8));
        }

        public void SetSpawnRate(int value) => spawnRate = Mathf.Clamp(value, 10, 400);
        public void ToggleSpawning() => spawning = !spawning;

        public void ResetEnemies()
        {
            random = new System.Random(731);
            Array.Clear(enemies, 0, enemies.Length);
            for (int i = 0; i < Capacity; i++) free[i] = Capacity - 1 - i;
            freeCount = Capacity;
            Alive = Killed = Escaped = Spawned = 0;
            spawnBudget = 0;
            foreach (Tower tower in towers)
            {
                tower.cooldown = 0;
                tower.tracerTime = 0;
                tower.tracer.enabled = false;
            }
        }

        public void ClearTowers()
        {
            foreach (Tower tower in towers) Destroy(tower.root);
            towers.Clear();
        }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            frameCount++;
            sampleSeconds += Time.unscaledDeltaTime;
            if (sampleSeconds >= 0.5f)
            {
                smoothedDelta = sampleSeconds / Mathf.Max(1, frameCount);
                frameCount = 0;
                sampleSeconds = 0;
            }

            if (spawning)
            {
                spawnBudget += spawnRate * dt;
                int count = Mathf.Min(Mathf.FloorToInt(spawnBudget), freeCount);
                for (int n = 0; n < count; n++) Spawn();
                spawnBudget -= count;
                if (freeCount == 0) spawnBudget = 0;
            }
            MoveAndIndex(dt);
            UpdateTowers(dt);
            UpdatePlacement();
            DrawEnemies();
        }

        void Spawn()
        {
            int id = free[--freeCount];
            enemies[id] = new Enemy
            {
                alive = true,
                position = new Vector3(-25f, 0.55f, (float)random.NextDouble() * 11.2f - 5.6f),
                speed = 1.7f + (float)random.NextDouble() * 0.8f
            };
            Alive++;
            Spawned++;
        }

        void MoveAndIndex(float dt)
        {
            Array.Fill(heads, -1);
            for (int i = 0; i < Capacity; i++)
            {
                if (!enemies[i].alive) continue;
                enemies[i].position.x += enemies[i].speed * dt;
                if (enemies[i].position.x > 25)
                {
                    Remove(i, false);
                    continue;
                }
                int cell = Cell(enemies[i].position);
                next[i] = heads[cell];
                heads[cell] = i;
            }
        }

        static int Cell(Vector3 position)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((position.x + 28) / CellSize), 0, GridWidth - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((position.z + 16) / CellSize), 0, GridHeight - 1);
            return z * GridWidth + x;
        }

        int FindTarget(Vector3 origin)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt((origin.x - Range + 28) / CellSize), 0, GridWidth - 1);
            int maxX = Mathf.Clamp(Mathf.FloorToInt((origin.x + Range + 28) / CellSize), 0, GridWidth - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt((origin.z - Range + 16) / CellSize), 0, GridHeight - 1);
            int maxZ = Mathf.Clamp(Mathf.FloorToInt((origin.z + Range + 16) / CellSize), 0, GridHeight - 1);
            float best = Range * Range;
            int target = -1;
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    for (int i = heads[z * GridWidth + x]; i >= 0; i = next[i])
                    {
                        if (!enemies[i].alive) continue;
                        Vector3 delta = enemies[i].position - origin;
                        float distance = delta.x * delta.x + delta.z * delta.z;
                        if (distance >= best) continue;
                        best = distance;
                        target = i;
                    }
            return target;
        }

        void UpdateTowers(float dt)
        {
            foreach (Tower tower in towers)
            {
                tower.tracerTime -= dt;
                tower.tracer.enabled = tower.tracerTime > 0;
                tower.cooldown -= dt;
                if (tower.cooldown > 0) continue;
                int target = FindTarget(tower.root.transform.position);
                tower.cooldown = FireInterval;
                if (target < 0) continue;
                Vector3 hit = enemies[target].position;
                Vector3 direction = hit - tower.head.position;
                direction.y = 0;
                tower.head.rotation = Quaternion.LookRotation(direction);
                tower.tracer.SetPosition(0, tower.head.position + tower.head.forward * 1.2f);
                tower.tracer.SetPosition(1, hit);
                tower.tracerTime = 0.045f;
                tower.tracer.enabled = true;
                // One-hit capsules keep this prototype focused on throughput.
                Remove(target, true);
            }
        }

        void Remove(int id, bool killed)
        {
            if (!enemies[id].alive) return;
            enemies[id].alive = false;
            free[freeCount++] = id;
            Alive--;
            if (killed) Killed++;
            else Escaped++;
        }

        void DrawEnemies()
        {
            var rp = new RenderParams(enemyMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, new Vector3(60, 5, 30))
            };
            int count = 0;
            for (int i = 0; i < Capacity; i++)
            {
                if (!enemies[i].alive) continue;
                matrices[count++] = Matrix4x4.TRS(enemies[i].position, Quaternion.identity, new Vector3(0.38f, 0.52f, 0.38f));
                if (count != matrices.Length) continue;
                Graphics.RenderMeshInstanced(rp, enemyMesh, 0, matrices, count);
                count = 0;
            }
            if (count > 0) Graphics.RenderMeshInstanced(rp, enemyMesh, 0, matrices, count);
        }

        public bool TryPlaceTower(Vector3 position)
        {
            position.y = 0;
            if (!ValidPlacement(position)) return false;
            var root = new GameObject("Turret " + (towers.Count + 1));
            root.transform.SetParent(transform);
            root.transform.position = position;
            MakePart("Base", root.transform, new Vector3(0, 0.25f, 0), new Vector3(1.35f, 0.5f, 1.35f), barrelMaterial);
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0, 0.9f, 0);
            MakePart("Cube", head, Vector3.zero, new Vector3(1, 0.8f, 1), towerMaterial);
            MakePart("Barrel", head, new Vector3(0, 0, 0.8f), new Vector3(0.28f, 0.28f, 1.1f), barrelMaterial);
            LineRenderer tracer = MakeLine("Tracer", root.transform, tracerMaterial, 0.065f, 2);
            tracer.enabled = false;
            towers.Add(new Tower { root = root, head = head, tracer = tracer, cooldown = towers.Count * 0.013f });
            return true;
        }

        bool ValidPlacement(Vector3 position)
        {
            if (towers.Count >= MaxTowers || Mathf.Abs(position.x) > 23 || Mathf.Abs(position.z) < 7 || Mathf.Abs(position.z) > 11) return false;
            foreach (Tower tower in towers)
                if ((tower.root.transform.position - position).sqrMagnitude < 2.7f) return false;
            return true;
        }

        void CreatePreview()
        {
            preview = MakePart("Placement Preview", transform, Vector3.zero, new Vector3(1, 0.8f, 1), validMaterial);
            previewRenderer = preview.GetComponent<Renderer>();
            rangeRing = MakeLine("Range", transform, validMaterial, 0.045f, 65);
        }

        void UpdatePlacement()
        {
            var mouse = Mouse.current;
            bool visible = mouse != null && !PointerOverHud && viewCamera != null;
            float distance = 0;
            Ray ray = visible ? viewCamera.ScreenPointToRay(mouse.position.ReadValue()) : default;
            visible = visible && new Plane(Vector3.up, Vector3.zero).Raycast(ray, out distance);
            placement = visible ? ray.GetPoint(distance) : Vector3.zero;
            visible = visible && Mathf.Abs(placement.x) <= 27 && Mathf.Abs(placement.z) <= 13;
            preview.gameObject.SetActive(visible);
            rangeRing.enabled = visible;
            if (!visible) return;
            placement.x = Mathf.Round(placement.x);
            placement.z = Mathf.Round(placement.z);
            placement.y = 0;
            canPlace = ValidPlacement(placement);
            Material color = canPlace ? validMaterial : invalidMaterial;
            previewRenderer.sharedMaterial = color;
            preview.position = placement + Vector3.up * 0.9f;
            rangeRing.sharedMaterial = color;
            for (int i = 0; i <= 64; i++)
            {
                float angle = i * Mathf.PI * 2 / 64;
                rangeRing.SetPosition(i, placement + new Vector3(Mathf.Cos(angle) * Range, 0.05f, Mathf.Sin(angle) * Range));
            }
            if (canPlace && mouse.leftButton.wasPressedThisFrame) TryPlaceTower(placement);
        }

        static Transform MakePart(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Destroy(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part.transform;
        }

        static LineRenderer MakeLine(string name, Transform parent, Material material, float width, int count)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.positionCount = count;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }
    }
}
