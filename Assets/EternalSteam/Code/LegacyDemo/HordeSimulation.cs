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
        const int GridWidth = 44;
        const int GridHeight = 34;
        const float CellSize = 2f;
        public const float Range = 22f;
        public const float MinRange = 6f;
        public const float MaxRange = 26f;
        public const float ShotHalfWidth = 0.3f;
        public const int MaxHealth = 1000;
        const int MaxTowers = 64;
        static readonly int[] StageCounts = { 1500, 3000, 5000 };
        int stageIndex;

        [SerializeField] Camera viewCamera;
        [SerializeField] Mesh enemyMesh;
        [SerializeField] Material enemyMaterial;
        [SerializeField] Material towerMaterial;
        [SerializeField] Material barrelMaterial;
        [SerializeField] Material tracerMaterial;
        [SerializeField] Material validMaterial;
        [SerializeField] Material invalidMaterial;
        [SerializeField, Range(10, 400)] int spawnRate = 100;
        [SerializeField] HordeMapKind mapKind;

        struct Enemy
        {
            public Vector3 position;
            public Vector3 destination;
            public float speed;
            public bool alive;
            public float slowTime;
            public int health;
        }

        sealed class Tower
        {
            public GameObject root;
            public Transform head;
            public LineRenderer tracer;
            public float range;
            public float halfAngle;
            public int shot;
            public float cooldown;
            public float tracerTime;
            public HordeTowerKind kind;
            public LineRenderer impact;
            public LineRenderer coverage;
            public float impactTime;
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
        LineRenderer directionArrow;
        Transform previewBarrel;
        HordeBuildGrid buildGrid;
        LineRenderer selectedCell;
        Rect[] buildZones;
        bool choosingDirection;
        Vector3 lockedPosition;
        Vector3 placementDirection = Vector3.forward;
        float placementRange = Range;
        bool canPlace;
        Vector3 placement;
        int frameCount;
        float sampleSeconds;
        bool spawning;
        bool waveStarted;
        bool previousBackgroundSetting;
        readonly Material[] typeMaterials = new Material[4];
        readonly Material[] effectMaterials = new Material[4];
        Material slowedMaterial;
        string battleId;
        bool resultRecorded;
        public bool InLobby { get; private set; } = true;
        public int LastReward { get; private set; }

        public int Alive { get; private set; }
        public int Killed { get; private set; }
        public int Escaped { get; private set; }
        public int Spawned { get; private set; }
        public int Health { get; private set; } = MaxHealth;
        public bool Defeated => Health == 0;
        public int StageNumber => stageIndex + 1;
        public int StageCount => StageCounts.Length;
        public int StageEnemyTotal => StageCounts[stageIndex];
        public int RemainingToSpawn => Mathf.Max(0, StageEnemyTotal - Spawned);
        public int Resolved => Killed + Escaped;
        public bool StageCleared => waveStarted && !Defeated && Resolved == StageEnemyTotal && Alive == 0;
        public bool StageFailed => Defeated;
        public bool StageEnded => StageCleared || StageFailed;
        public bool AllStagesCleared => StageCleared && StageNumber == StageCount;
        public HordeTowerKind SelectedTower { get; private set; }
        public int TowerCount => towers.Count;
        public int TowerCountFor(HordeTowerKind kind)
        {
            int count = 0;
            foreach (var tower in towers) if (tower.kind == kind) count++;
            return count;
        }
        public int TowerLimitFor(HordeTowerKind kind) => HordeProgress.Current.TowerLimit(kind);
        public bool UpgradeTowerCapacity(HordeTowerKind kind) => InLobby && HordeProgress.Current.UpgradeCapacity(kind);
        public int SpawnRate => spawnRate;
        public bool Spawning => spawning;
        public bool WaveStarted => waveStarted;
        public bool AtCapacity => freeCount == 0;
        public float Fps => 1f / Mathf.Max(0.0001f, smoothedDelta);
        public bool PointerOverHud { get; set; }
        public HordeMapKind MapKind => mapKind;
        public bool ChoosingDirection => choosingDirection;
        public bool CanBuild => !InLobby && !waveStarted && !StageEnded;
        public string PlacementHint => Defeated ? "체력이 0이 되어 전투가 종료되었습니다. 재도전하거나 로비로 돌아가세요." : StageFailed ? "방어 실패 — 체력이 소진되었습니다. 다시 도전하세요." : AllStagesCleared ? "전체 클리어 — 처음부터 누르면 1단계로 돌아갑니다." : StageCleared ? "스테이지 클리어 — 다음 단계에서 다시 포탑을 준비하세요." : waveStarted ? "전투 중에는 포탑을 설치할 수 없습니다." : TowerCountFor(SelectedTower) >= TowerLimitFor(SelectedTower) ? $"{HordeTowerStats.Name(SelectedTower)} 설치 한도 {TowerLimitFor(SelectedTower)}개 — 로비에서 한도를 강화하세요." : choosingDirection ? $"사거리 {placementRange:F1} / 퍼짐 {SpreadHalfAngle(placementRange) * 2:F0}도 — 가까이: 넓게 / 멀리: 좁게 / 클릭: 확정" : towers.Count >= MaxTowers ? "포탑은 최대 64개까지 설치할 수 있습니다." : "파란 바닥을 클릭한 뒤 공격 방향과 거리를 정하세요.";

        public void SelectTower(HordeTowerKind kind)
        {
            if (!Enum.IsDefined(typeof(HordeTowerKind), kind)) return;
            CancelPlacement();
            SelectedTower = kind;
        }

        public static float SpreadHalfAngle(float distance)
        {
            distance = Mathf.Clamp(distance, MinRange, MaxRange);
            float halfWidth = Mathf.Lerp(4.6f, 1.8f, Mathf.InverseLerp(MinRange, MaxRange, distance));
            return Mathf.Asin(halfWidth / distance) * Mathf.Rad2Deg;
        }

        public void ConfigureMap(HordeMapKind kind)
        {
            mapKind = kind;
            buildZones = HordeMapLayout.BuildZones(kind);
            spawnRate = kind == HordeMapKind.Lane ? 100 : 300;
            if (buildGrid != null) buildGrid.Configure(kind, tracerMaterial);
        }

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
            buildZones = HordeMapLayout.BuildZones(mapKind);
        }

        void OnDestroy()
        {
            Application.runInBackground = previousBackgroundSetting;
            foreach (var material in typeMaterials) if (material != null) Destroy(material);
            foreach (var material in effectMaterials) if (material != null) Destroy(material);
            if (slowedMaterial != null) Destroy(slowedMaterial);
        }

        void Start()
        {
            ResetEnemies();
            for (int i = 0; i < typeMaterials.Length; i++)
            {
                typeMaterials[i] = new Material(towerMaterial);
                typeMaterials[i].SetColor("_BaseColor", HordeTowerStats.Color((HordeTowerKind)i));
                effectMaterials[i] = new Material(tracerMaterial);
                effectMaterials[i].SetColor("_BaseColor", HordeTowerStats.Color((HordeTowerKind)i));
            }
            slowedMaterial = new Material(enemyMaterial);
            slowedMaterial.SetColor("_BaseColor", new Color(0.35f, 0.7f, 1));
            CreatePreview();
            var gridObject = new GameObject("Deployment Grid");
            gridObject.transform.SetParent(transform, false);
            buildGrid = gridObject.AddComponent<HordeBuildGrid>();
            buildGrid.Configure(mapKind, tracerMaterial);
            int index = 0;
            foreach (var position in HordeMapLayout.StartingTowers(mapKind))
                TryPlaceTower(position, HordeMapLayout.DefaultDirection(mapKind, position), Range, (HordeTowerKind)(index++ % 3));
            FitCamera();
        }

        int viewportWidth;
        int viewportHeight;
        public void SetBattleViewport(float leftFraction)
        {
            if (viewCamera == null || !float.IsFinite(leftFraction)) return;
            leftFraction = Mathf.Clamp(leftFraction, 0, 0.8f);
            var rect = new Rect(leftFraction, 0, 1 - leftFraction, 1);
            if (viewCamera.rect == rect && viewportWidth == Screen.width && viewportHeight == Screen.height) return;
            viewCamera.rect = rect;
            viewportWidth = Screen.width;
            viewportHeight = Screen.height;
            FitCamera();
        }

        void FitCamera()
        {
            if (viewCamera == null) return;
            Vector2 size = HordeMapLayout.Size(mapKind);
            Vector3 up = viewCamera.transform.up;
            Vector3 right = viewCamera.transform.right;
            float height = Mathf.Abs(up.x) * size.x + Mathf.Abs(up.z) * size.y + 8;
            float width = Mathf.Abs(right.x) * size.x + Mathf.Abs(right.z) * size.y + 8;
            viewCamera.aspect = Mathf.Max(0.01f, viewCamera.pixelRect.width / Mathf.Max(1, viewCamera.pixelRect.height));
            viewCamera.orthographicSize = Mathf.Max(height * 0.5f, width / (2 * viewCamera.aspect));
            viewCamera.transform.position = -viewCamera.transform.forward * 80;
        }

        public void SetSpawnRate(int value) => spawnRate = Mathf.Clamp(value, 10, 400);
        public bool Deploy(int stage)
        {
            if (!InLobby || !HordeProgress.Current.CanDeploy(mapKind, stage)) return false;
            stageIndex = stage - 1;
            ResetEnemies();
            InLobby = false;
            return true;
        }

        public void ReturnToLobby()
        {
            RecordResult();
            ResetEnemies();
            InLobby = true;
        }

        void RecordResult()
        {
            if (resultRecorded || !StageEnded) return;
            resultRecorded = true;
            LastReward = StageCleared ? HordeProgress.Current.AwardClear(mapKind, StageNumber, battleId) : 0;
        }

        public void ToggleSpawning()
        {
            if (InLobby) return;
            RecordResult();
            if (StageCleared)
            {
                stageIndex = AllStagesCleared ? 0 : stageIndex + 1;
                ResetEnemies();
                return;
            }
            if (StageFailed || RemainingToSpawn == 0) return;
            CancelPlacement();
            waveStarted = true;
            spawning = !spawning;
        }

        public void ResetEnemies()
        {
            RecordResult();
            battleId = Guid.NewGuid().ToString("N");
            resultRecorded = false;
            LastReward = 0;
            CancelPlacement();
            spawning = false;
            waveStarted = false;
            Health = MaxHealth;
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
                tower.shot = 0;
                tower.tracer.enabled = false;
                tower.impactTime = 0;
                if (tower.impact != null) tower.impact.enabled = false;
                tower.coverage.enabled = true;
            }
        }

        public void ClearTowers()
        {
            if (waveStarted) return;
            CancelPlacement();
            foreach (Tower tower in towers) Destroy(tower.root);
            towers.Clear();
        }

        void Update()
        {
            if (InLobby)
            {
                PointerOverHud = true;
                UpdatePlacement();
                buildGrid.gameObject.SetActive(false);
                return;
            }
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
                int count = Mathf.Min(Mathf.FloorToInt(spawnBudget), freeCount, RemainingToSpawn);
                for (int n = 0; n < count; n++) Spawn();
                spawnBudget -= count;
                if (freeCount == 0) spawnBudget = 0;
                if (RemainingToSpawn == 0) spawning = false;
            }
            if (!StageEnded) MoveAndIndex(dt);
            UpdateTowers(dt);
            RecordResult();
            UpdatePlacement();
            buildGrid.gameObject.SetActive(!Defeated && (!waveStarted || choosingDirection));
            DrawEnemies();
        }

        void Spawn()
        {
            if (freeCount == 0 || StageEnded || RemainingToSpawn == 0) return;
            HordeMapLayout.SpawnRoute(mapKind, random, out var start, out var end);
            int id = free[--freeCount];
            enemies[id] = new Enemy
            {
                alive = true,
                health = 20,
                position = start,
                destination = end,
                speed = (mapKind == HordeMapKind.Lane ? 1.7f : 3.8f) + (float)random.NextDouble() * 0.8f
            };
            Alive++;
            Spawned++;
        }

        void MoveAndIndex(float dt)
        {
            Array.Fill(heads, -1);
            for (int i = 0; i < Capacity; i++)
            {
                if (Defeated) break;
                if (!enemies[i].alive) continue;
                float slowedSeconds = Mathf.Min(dt, enemies[i].slowTime);
                enemies[i].slowTime = Mathf.Max(0, enemies[i].slowTime - dt);
                float movementSeconds = dt - slowedSeconds * 0.6f;
                enemies[i].position = Vector3.MoveTowards(enemies[i].position, enemies[i].destination, enemies[i].speed * movementSeconds);
                if ((enemies[i].position - enemies[i].destination).sqrMagnitude < 0.001f)
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
            int x = Mathf.Clamp(Mathf.FloorToInt((position.x + 44) / CellSize), 0, GridWidth - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((position.z + 34) / CellSize), 0, GridHeight - 1);
            return z * GridWidth + x;
        }

        int FindTarget(Vector3 origin, Vector3 forward, float range)
        {
            Vector3 end = origin + forward * range;
            int minX = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(origin.x, end.x) - ShotHalfWidth + 44) / CellSize), 0, GridWidth - 1);
            int maxX = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(origin.x, end.x) + ShotHalfWidth + 44) / CellSize), 0, GridWidth - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(origin.z, end.z) - ShotHalfWidth + 34) / CellSize), 0, GridHeight - 1);
            int maxZ = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(origin.z, end.z) + ShotHalfWidth + 34) / CellSize), 0, GridHeight - 1);
            float best = range;
            int target = -1;
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    for (int i = heads[z * GridWidth + x]; i >= 0; i = next[i])
                    {
                        if (!enemies[i].alive) continue;
                        Vector3 delta = enemies[i].position - origin;
                        delta.y = 0;
                        float along = Vector3.Dot(delta, forward);
                        float lateral = Mathf.Abs(delta.x * forward.z - delta.z * forward.x);
                        if (along < 1.2f || along >= best || lateral > ShotHalfWidth || delta.sqrMagnitude > range * range) continue;
                        best = along;
                        target = i;
                    }
            return target;
        }

        void UpdateTowers(float dt)
        {
            foreach (Tower tower in towers)
            {
                tower.coverage.enabled = !waveStarted && !Defeated;
                tower.tracerTime -= dt;
                tower.tracer.enabled = !Defeated && tower.tracerTime > 0;
                tower.impactTime -= dt;
                if (tower.impact != null) tower.impact.enabled = !Defeated && tower.impactTime > 0;
                if (!waveStarted || StageEnded) continue;
                tower.cooldown -= dt;
                if (tower.cooldown > 0) continue;
                tower.cooldown = HordeTowerStats.Interval(tower.kind);
                tower.shot++;
                tower.tracerTime = tower.kind == HordeTowerKind.MachineGun ? 0.035f : 0.1f;
                float fraction = Mathf.Repeat(tower.shot * 0.6180339f, 1f);
                float angle = Mathf.Lerp(-tower.halfAngle, tower.halfAngle, fraction);
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * tower.head.forward;
                tower.tracer.SetPosition(0, tower.head.position + direction * 1.2f);
                tower.tracer.SetPosition(1, tower.head.position + direction * tower.range);
                tower.tracer.enabled = true;
                if (tower.kind == HordeTowerKind.Frost)
                {
                    ApplyFrostCone(tower.root.transform.position, tower.head.forward, tower.range, tower.halfAngle);
                    tower.impactTime = 0.2f;
                    tower.impact.enabled = true;
                    Vector3 origin = tower.root.transform.position + Vector3.up * 0.13f;
                    tower.impact.SetPosition(0, origin);
                    for (int i = 0; i <= 32; i++)
                        tower.impact.SetPosition(i + 1, origin + Quaternion.AngleAxis(Mathf.Lerp(-tower.halfAngle, tower.halfAngle, i / 32f), Vector3.up) * tower.head.forward * tower.range);
                    tower.impact.SetPosition(34, origin);
                    continue;
                }
                if (tower.kind == HordeTowerKind.Arrow)
                {
                    ApplyPiercingShot(tower.root.transform.position, direction, tower.range);
                    continue;
                }
                int target = FindTarget(tower.root.transform.position, direction, tower.range);
                if (tower.kind == HordeTowerKind.MachineGun)
                {
                    if (target >= 0) ApplyDamage(target, HordeTowerStats.Damage(tower.kind));
                }
                else
                {
                    Vector3 center = target >= 0 ? enemies[target].position : tower.root.transform.position + direction * tower.range;
                    float radius = 3;
                    ApplyArea(center, radius);
                    tower.impactTime = 0.2f;
                    tower.impact.enabled = true;
                    for (int i = 0; i <= 32; i++)
                    {
                        float arcAngle = i * Mathf.PI * 2 / 32;
                        tower.impact.SetPosition(i, new Vector3(center.x + Mathf.Cos(arcAngle) * radius, 0.13f, center.z + Mathf.Sin(arcAngle) * radius));
                    }
                }
            }
        }

        void ApplyDamage(int id, int damage)
        {
            if (!enemies[id].alive || damage <= 0) return;
            enemies[id].health -= damage;
            if (enemies[id].health <= 0) Remove(id, true);
        }

        void ApplyPiercingShot(Vector3 origin, Vector3 direction, float range)
        {
            // A single ray per shot keeps the prototype cheap and cannot hit a slot twice.
            for (int i = 0; i < Capacity; i++)
            {
                if (!enemies[i].alive) continue;
                Vector3 delta = enemies[i].position - origin;
                delta.y = 0;
                float along = Vector3.Dot(delta, direction);
                float lateral = Mathf.Abs(delta.x * direction.z - delta.z * direction.x);
                if (along >= 1.2f && along <= range && delta.sqrMagnitude <= range * range && lateral <= ShotHalfWidth)
                    ApplyDamage(i, HordeTowerStats.Damage(HordeTowerKind.Arrow));
            }
        }

        void Remove(int id, bool killed)
        {
            if (!enemies[id].alive) return;
            enemies[id].alive = false;
            free[freeCount++] = id;
            Alive--;
            if (killed) Killed++;
            else
            {
                Escaped++;
                Health = Mathf.Max(0, Health - 1);
                if (Defeated)
                {
                    spawning = false;
                    CancelPlacement();
                }
            }
        }

        void ApplyFrostCone(Vector3 origin, Vector3 direction, float range, float halfAngle)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt((origin.x - range + 44) / CellSize), 0, GridWidth - 1);
            int maxX = Mathf.Clamp(Mathf.FloorToInt((origin.x + range + 44) / CellSize), 0, GridWidth - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt((origin.z - range + 34) / CellSize), 0, GridHeight - 1);
            int maxZ = Mathf.Clamp(Mathf.FloorToInt((origin.z + range + 34) / CellSize), 0, GridHeight - 1);
            float cosine = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    for (int i = heads[z * GridWidth + x]; i >= 0; i = next[i])
                    {
                        if (!enemies[i].alive) continue;
                        Vector3 delta = enemies[i].position - origin;
                        delta.y = 0;
                        float squareDistance = delta.sqrMagnitude;
                        if (squareDistance > range * range || Vector3.Dot(delta, direction) < Mathf.Sqrt(squareDistance) * cosine) continue;
                        enemies[i].slowTime = 2.5f;
                    }
        }

        void ApplyArea(Vector3 center, float radius)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt((center.x - radius + 44) / CellSize), 0, GridWidth - 1);
            int maxX = Mathf.Clamp(Mathf.FloorToInt((center.x + radius + 44) / CellSize), 0, GridWidth - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt((center.z - radius + 34) / CellSize), 0, GridHeight - 1);
            int maxZ = Mathf.Clamp(Mathf.FloorToInt((center.z + radius + 34) / CellSize), 0, GridHeight - 1);
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    for (int i = heads[z * GridWidth + x]; i >= 0; i = next[i])
                    {
                        if (!enemies[i].alive) continue;
                        var delta = enemies[i].position - center;
                        delta.y = 0;
                        if (delta.sqrMagnitude > radius * radius) continue;
                        ApplyDamage(i, HordeTowerStats.Damage(HordeTowerKind.Cannon));
                    }
        }

        void DrawEnemies()
        {
            DrawEnemyGroup(false);
            DrawEnemyGroup(true);
        }

        void DrawEnemyGroup(bool slowed)
        {
            var rp = new RenderParams(slowed ? slowedMaterial : enemyMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, new Vector3(90, 5, 68))
            };
            int count = 0;
            for (int i = 0; i < Capacity; i++)
            {
                if (!enemies[i].alive || (enemies[i].slowTime > 0) != slowed) continue;
                matrices[count++] = Matrix4x4.TRS(enemies[i].position, Quaternion.identity, new Vector3(0.38f, 0.52f, 0.38f));
                if (count != matrices.Length) continue;
                Graphics.RenderMeshInstanced(rp, enemyMesh, 0, matrices, count);
                count = 0;
            }
            if (count > 0) Graphics.RenderMeshInstanced(rp, enemyMesh, 0, matrices, count);
        }

        public bool TryPlaceTower(Vector3 position)
            => TryPlaceTower(position, HordeMapLayout.DefaultDirection(mapKind, position));

        public bool TryPlaceTower(Vector3 position, Vector3 direction)
            => TryPlaceTower(position, direction, Range);

        public bool TryPlaceTower(Vector3 position, Vector3 direction, float distance)
            => TryPlaceTower(position, direction, distance, SelectedTower);

        public bool TryPlaceTower(Vector3 position, Vector3 direction, float distance, HordeTowerKind kind)
        {
            if (!Enum.IsDefined(typeof(HordeTowerKind), kind)) return false;
            position = HordeMapLayout.Snap(position);
            direction.y = 0;
            if (direction.sqrMagnitude < 0.001f || !float.IsFinite(direction.sqrMagnitude)) return false;
            if (!float.IsFinite(distance)) return false;
            distance = Mathf.Clamp(distance, MinRange, MaxRange);
            if (!ValidPlacement(position, kind)) return false;
            var root = new GameObject(HordeTowerStats.Name(kind) + " " + (towers.Count + 1));
            root.transform.SetParent(transform);
            root.transform.position = position + Vector3.up * HordeMapLayout.SurfaceHeight(mapKind);
            root.transform.localScale = Vector3.one * HordeMapLayout.TowerScale;
            MakePart("Base", root.transform, new Vector3(0, 0.25f, 0), new Vector3(1.35f, 0.5f, 1.35f), barrelMaterial);
            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0, 0.9f, 0);
            head.rotation = Quaternion.LookRotation(direction.normalized);
            MakePart("Cube", head, Vector3.zero, new Vector3(1, 0.8f, 1), typeMaterials[(int)kind]);
            var barrelScale = kind == HordeTowerKind.Cannon ? new Vector3(0.55f, 0.55f, 1.3f) : kind == HordeTowerKind.Frost ? new Vector3(0.65f, 0.2f, 0.7f) : new Vector3(0.28f, 0.28f, 1.1f);
            MakePart("Barrel", head, new Vector3(0, 0, 0.8f), barrelScale, barrelMaterial);
            var tracer = MakeLine("Weapon tracer", root.transform, kind == HordeTowerKind.MachineGun ? tracerMaterial : effectMaterials[(int)kind], kind == HordeTowerKind.MachineGun ? 0.065f : 0.14f, 2);
            tracer.startColor = tracer.endColor = HordeTowerStats.Color(kind);
            tracer.enabled = false;
            LineRenderer impact = null;
            if (kind == HordeTowerKind.Cannon || kind == HordeTowerKind.Frost)
            {
                impact = MakeLine("Area effect", root.transform, effectMaterials[(int)kind], 0.12f, kind == HordeTowerKind.Frost ? 35 : 33);
                impact.startColor = impact.endColor = HordeTowerStats.Color(kind);
                impact.enabled = false;
            }
            var sight = MakeLine("Fixed direction", root.transform, validMaterial, 0.045f, 4);
            Vector3 forward = head.forward;
            Vector3 right = head.right;
            Vector3 tip = position + forward * 3 + Vector3.up * 0.08f;
            sight.SetPosition(0, tip - forward * 0.65f - right * 0.4f);
            sight.SetPosition(1, tip);
            sight.SetPosition(2, tip - forward * 0.65f + right * 0.4f);
            sight.SetPosition(3, tip);
            var coverage = MakeLine("Deployment firing range", root.transform, effectMaterials[(int)kind], 0.14f, 35);
            Vector3 coverageOrigin = position + Vector3.up * (HordeMapLayout.SurfaceHeight(mapKind) + 0.09f);
            float halfAngle = SpreadHalfAngle(distance);
            coverage.SetPosition(0, coverageOrigin);
            for (int i = 0; i <= 32; i++)
                coverage.SetPosition(i + 1, coverageOrigin + Quaternion.AngleAxis(Mathf.Lerp(-halfAngle, halfAngle, i / 32f), Vector3.up) * head.forward * distance);
            coverage.SetPosition(34, coverageOrigin);
            coverage.enabled = !waveStarted && !Defeated;
            towers.Add(new Tower { root = root, head = head, tracer = tracer, impact = impact, coverage = coverage, kind = kind, range = distance, halfAngle = halfAngle, cooldown = towers.Count * 0.013f });
            return true;
        }

        bool ValidPlacement(Vector3 position)
            => ValidPlacement(position, SelectedTower);

        bool ValidPlacement(Vector3 position, HordeTowerKind kind)
        {
            if (waveStarted || TowerCountFor(kind) >= TowerLimitFor(kind)) return false;
            if (Defeated || towers.Count >= MaxTowers || !float.IsFinite(position.x) || !float.IsFinite(position.z)) return false;
            bool inside = false;
            foreach (var zone in buildZones)
                if (HordeMapLayout.ContainsFootprint(zone, position, HordeMapLayout.BuildMargin)
                    && HordeMapLayout.ContainsFootprint(zone, position, HordeMapLayout.TowerHalfWidth)) inside = true;
            if (!inside) return false;
            foreach (Tower tower in towers)
                if (tower.root.transform.position.x == position.x && tower.root.transform.position.z == position.z) return false;
            return true;
        }

        void CreatePreview()
        {
            preview = MakePart("Placement Preview", transform, Vector3.zero, new Vector3(1, 0.8f, 1), validMaterial);
            preview.localScale *= HordeMapLayout.TowerScale;
            previewRenderer = preview.GetComponent<Renderer>();
            previewBarrel = MakePart("Preview Barrel", preview, new Vector3(0, 0, 0.8f), new Vector3(0.28f, 0.28f, 1.1f), validMaterial);
            rangeRing = MakeLine("Firing corridor", transform, validMaterial, 0.065f, 35);
            directionArrow = MakeLine("Aim arrow", transform, validMaterial, 0.09f, 5);
            selectedCell = MakeLine("Selected grid cell", transform, validMaterial, 0.09f, 5);
        }

        public void CancelPlacement() => choosingDirection = false;

        public bool BeginPlacement(Vector3 position)
        {
            position = HordeMapLayout.Snap(position);
            if (!ValidPlacement(position)) return false;
            lockedPosition = position;
            placementDirection = HordeMapLayout.DefaultDirection(mapKind, position);
            placementRange = Range;
            choosingDirection = true;
            return true;
        }

        public bool ConfirmPlacement(Vector3 direction)
            => ConfirmPlacement(direction, Range);

        public bool ConfirmPlacement(Vector3 direction, float distance)
        {
            if (!choosingDirection || !TryPlaceTower(lockedPosition, direction, distance)) return false;
            CancelPlacement();
            return true;
        }

        void UpdatePlacement()
        {
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame || Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CancelPlacement();
            bool visible = CanBuild && mouse != null && !PointerOverHud && viewCamera != null;
            float distance = 0;
            Ray ray = visible ? viewCamera.ScreenPointToRay(mouse.position.ReadValue()) : default;
            visible = visible && new Plane(Vector3.up, Vector3.zero).Raycast(ray, out distance);
            Vector3 pointer = visible ? ray.GetPoint(distance) : Vector3.zero;
            Vector2 size = HordeMapLayout.Size(mapKind);
            visible = visible && (choosingDirection || Mathf.Abs(pointer.x) <= size.x / 2 && Mathf.Abs(pointer.z) <= size.y / 2);
            preview.gameObject.SetActive(visible);
            rangeRing.enabled = visible && choosingDirection;
            directionArrow.enabled = visible && choosingDirection;
            selectedCell.enabled = visible && choosingDirection;
            if (!visible) return;
            placement = choosingDirection ? lockedPosition : pointer;
            placement = HordeMapLayout.Snap(placement);
            canPlace = ValidPlacement(placement);
            if (choosingDirection)
            {
                var aim = pointer - lockedPosition;
                aim.y = 0;
                if (aim.sqrMagnitude > 0.25f) placementDirection = aim.normalized;
                placementRange = Mathf.Clamp(aim.magnitude, MinRange, MaxRange);
            }
            else placementDirection = HordeMapLayout.DefaultDirection(mapKind, placement);
            Material color = canPlace ? validMaterial : invalidMaterial;
            previewRenderer.sharedMaterial = canPlace ? typeMaterials[(int)SelectedTower] : invalidMaterial;
            preview.position = placement + Vector3.up * (0.9f * HordeMapLayout.TowerScale + HordeMapLayout.SurfaceHeight(mapKind));
            preview.rotation = Quaternion.LookRotation(placementDirection);
            previewBarrel.GetComponent<Renderer>().sharedMaterial = color;
            previewBarrel.localScale = SelectedTower == HordeTowerKind.Cannon ? new Vector3(0.55f, 0.55f, 1.3f) : SelectedTower == HordeTowerKind.Frost ? new Vector3(0.65f, 0.2f, 0.7f) : new Vector3(0.28f, 0.28f, 1.1f);
            rangeRing.sharedMaterial = color;
            directionArrow.sharedMaterial = color;
            selectedCell.sharedMaterial = color;
            Vector3 origin = placement + Vector3.up * (HordeMapLayout.SurfaceHeight(mapKind) + 0.07f);
            float halfCell = HordeMapLayout.BuildCellSize * 0.5f;
            selectedCell.SetPosition(0, origin + new Vector3(-halfCell, 0, -halfCell));
            selectedCell.SetPosition(1, origin + new Vector3(-halfCell, 0, halfCell));
            selectedCell.SetPosition(2, origin + new Vector3(halfCell, 0, halfCell));
            selectedCell.SetPosition(3, origin + new Vector3(halfCell, 0, -halfCell));
            selectedCell.SetPosition(4, origin + new Vector3(-halfCell, 0, -halfCell));
            Vector3 right = Vector3.Cross(Vector3.up, placementDirection);
            Vector3 end = origin + placementDirection * placementRange;
            float spread = SpreadHalfAngle(placementRange);
            rangeRing.SetPosition(0, origin);
            for (int i = 0; i <= 32; i++)
                rangeRing.SetPosition(i + 1, origin + Quaternion.AngleAxis(Mathf.Lerp(-spread, spread, i / 32f), Vector3.up) * placementDirection * placementRange);
            rangeRing.SetPosition(34, origin);
            directionArrow.SetPosition(0, origin);
            directionArrow.SetPosition(1, end);
            directionArrow.SetPosition(2, end - placementDirection * 1.5f - right);
            directionArrow.SetPosition(3, end);
            directionArrow.SetPosition(4, end - placementDirection * 1.5f + right);
            if (canPlace && mouse.leftButton.wasPressedThisFrame)
            {
                if (choosingDirection) ConfirmPlacement(placementDirection, placementRange);
                else BeginPlacement(placement);
            }
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
