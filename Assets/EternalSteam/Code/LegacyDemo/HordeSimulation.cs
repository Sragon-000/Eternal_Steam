using System;
using System.Collections.Generic;
using UnityEngine;
using static EternalSteam.Demo.HordeVisualPrimitives;

namespace EternalSteam.Demo
{
    public sealed class HordeSimulation : MonoBehaviour
    {
        public const int Capacity = HordeEnemyWorld.Capacity;
        public const float Range = HordeDeploymentGeometry.Range;
        public const float MinRange = HordeDeploymentGeometry.MinRange;
        public const float MaxRange = HordeDeploymentGeometry.MaxRange;
        public const float ShotHalfWidth = HordeAttackResolver.ShotHalfWidth;
        public const int MaxHealth = HordeEnemyWorld.MaxHealth;
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

        readonly HordeEnemyWorld enemyWorld = new();
        HordeAttackResolver attacks;
        HordeTowerCombat towerCombat;
        HordeEnemyRenderer enemyRenderer;
        readonly List<HordeTower> towers = new List<HordeTower>(MaxTowers);
        float spawnBudget;
        float smoothedDelta = 1f / 60f;
        HordeBuildGrid buildGrid;
        HordeBuildingPlacement buildingPlacement;
        HordeTowerFactory towerFactory;
        public BuildingWorld BuildingWorld => buildingPlacement?.World;
        HordePlacementController placementController;
        HordePlacementView placementView;
        readonly HordeCameraFraming cameraFraming = new();
        int frameCount;
        float sampleSeconds;
        bool spawning;
        bool waveStarted;
        bool previousBackgroundSetting;
        readonly Material[] typeMaterials = new Material[4];
        readonly Material[] effectMaterials = new Material[4];
        string battleId;
        bool resultRecorded;
        public bool InLobby { get; private set; } = true;
        public int LastReward { get; private set; }

        public int Alive => enemyWorld.Alive;
        public int Killed => enemyWorld.Killed;
        public int Escaped => enemyWorld.Escaped;
        public int Spawned => enemyWorld.Spawned;
        public int Health => enemyWorld.Health;
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
        public bool AtCapacity => enemyWorld.FreeCount == 0;
        public float Fps => 1f / Mathf.Max(0.0001f, smoothedDelta);
        public bool PointerOverHud { get; set; }
        public HordeMapKind MapKind => mapKind;
        public bool ChoosingDirection => placementController?.ChoosingDirection ?? false;
        public bool CanBuild => !InLobby && !waveStarted && !StageEnded;
        public string PlacementHint => Defeated ? "체력이 0이 되어 전투가 종료되었습니다. 재도전하거나 로비로 돌아가세요." : StageFailed ? "방어 실패 — 체력이 소진되었습니다. 다시 도전하세요." : AllStagesCleared ? "전체 클리어 — 처음부터 누르면 1단계로 돌아갑니다." : StageCleared ? "스테이지 클리어 — 다음 단계에서 다시 포탑을 준비하세요." : waveStarted ? "전투 중에는 포탑을 설치할 수 없습니다." : TowerCountFor(SelectedTower) >= TowerLimitFor(SelectedTower) ? $"{HordeTowerStats.Name(SelectedTower)} 설치 한도 {TowerLimitFor(SelectedTower)}개 — 로비에서 한도를 강화하세요." : ChoosingDirection ? $"사거리 {placementController.Range:F1} / 퍼짐 {SpreadHalfAngle(placementController.Range) * 2:F0}도 — 가까이: 넓게 / 멀리: 좁게 / 클릭: 확정" : towers.Count >= MaxTowers ? "포탑은 최대 64개까지 설치할 수 있습니다." : "파란 바닥을 클릭한 뒤 공격 방향과 거리를 정하세요.";

        public void SelectTower(HordeTowerKind kind)
        {
            if (!Enum.IsDefined(typeof(HordeTowerKind), kind)) return;
            CancelPlacement();
            SelectedTower = kind;
        }

        public static float SpreadHalfAngle(float distance) => HordeDeploymentGeometry.SpreadHalfAngle(distance);

        public void ConfigureMap(HordeMapKind kind)
        {
            mapKind = kind;
            buildingPlacement?.ConfigureMap(kind);
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
            attacks = new HordeAttackResolver(enemyWorld);
            towerCombat = new HordeTowerCombat(towers, enemyWorld, attacks);
            enemyWorld.Defeat += OnDefeat;
            placementController = new HordePlacementController(ValidPlacement,TryPlaceTower);
            previousBackgroundSetting = Application.runInBackground;
            Application.runInBackground = true;

        }

        void OnDestroy()
        {
            buildingPlacement?.Dispose();
            towerFactory?.Dispose();
            placementView?.Dispose();
            Application.runInBackground = previousBackgroundSetting;
            foreach (var material in typeMaterials) if (material != null) Destroy(material);
            foreach (var material in effectMaterials) if (material != null) Destroy(material);
            enemyWorld.Defeat -= OnDefeat;
            enemyRenderer?.Dispose();
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
            enemyRenderer = new HordeEnemyRenderer(enemyWorld, enemyMesh, enemyMaterial);
            towerFactory = new HordeTowerFactory(transform,towers,()=>mapKind,barrelMaterial,tracerMaterial,validMaterial,typeMaterials,effectMaterials);
            buildingPlacement = new HordeBuildingPlacement(towerFactory, mapKind, ()=>!waveStarted && !Defeated, ()=>towers.Count, TowerCountFor, TowerLimitFor, MaxTowers);
            placementView = new HordePlacementView(transform,validMaterial,invalidMaterial,typeMaterials);
            var gridObject = new GameObject("Deployment Grid");
            gridObject.transform.SetParent(transform, false);
            buildGrid = gridObject.AddComponent<HordeBuildGrid>();
            buildGrid.Configure(mapKind, tracerMaterial);
            int index = 0;
            foreach (var position in HordeMapLayout.StartingTowers(mapKind))
                TryPlaceTower(position, HordeMapLayout.DefaultDirection(mapKind, position), Range, (HordeTowerKind)(index++ % 3));
            FitCamera();
        }

        public void SetBattleViewport(float leftFraction) => cameraFraming.SetViewport(viewCamera,mapKind,leftFraction);

        void FitCamera() => cameraFraming.Fit(viewCamera,mapKind);

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
            enemyWorld.Reset();
            spawnBudget = 0;
            towerCombat.Reset();
        }

        public void ClearTowers()
        {
            if (waveStarted) return;
            CancelPlacement();
            buildingPlacement?.Clear();
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
                int count = Mathf.Min(Mathf.FloorToInt(spawnBudget), enemyWorld.FreeCount, RemainingToSpawn);
                for (int n = 0; n < count; n++) Spawn();
                spawnBudget -= count;
                if (enemyWorld.FreeCount == 0) spawnBudget = 0;
                if (RemainingToSpawn == 0) spawning = false;
            }
            if (!StageEnded) MoveAndIndex(dt);
            UpdateTowers(dt);
            RecordResult();
            UpdatePlacement();
            buildGrid.gameObject.SetActive(!Defeated && (!waveStarted || ChoosingDirection));
            DrawEnemies();
        }

        void OnDefeat()
        {
            spawning = false;
            CancelPlacement();
        }

        void Spawn()
        {
            if (StageEnded || RemainingToSpawn == 0) return;
            enemyWorld.Spawn(mapKind);
        }

        void MoveAndIndex(float dt) => enemyWorld.MoveAndIndex(dt);

        int FindTarget(Vector3 origin, Vector3 forward, float range) => attacks.FindTarget(origin, forward, range);

        void UpdateTowers(float dt) => towerCombat.Update(dt, waveStarted, StageEnemyTotal);

        void ApplyDamage(int id, int damage) => enemyWorld.ApplyDamage(id, damage);

        void ApplyPiercingShot(Vector3 origin, Vector3 direction, float range) => attacks.ApplyPiercingShot(origin, direction, range);

        void Remove(int id, bool killed) => enemyWorld.Remove(id, killed);

        void ApplyFrostCone(Vector3 origin, Vector3 direction, float range, float halfAngle) => attacks.ApplyFrostCone(origin, direction, range, halfAngle);

        void ApplyArea(Vector3 center, float radius) => attacks.ApplyArea(center, radius);

        void DrawEnemies() => enemyRenderer.Draw();

        public bool TryPlaceTower(Vector3 position)
            => TryPlaceTower(position, HordeMapLayout.DefaultDirection(mapKind, position));

        public bool TryPlaceTower(Vector3 position, Vector3 direction)
            => TryPlaceTower(position, direction, Range);

        public bool TryPlaceTower(Vector3 position, Vector3 direction, float distance)
            => TryPlaceTower(position, direction, distance, SelectedTower);

        public bool TryPlaceTower(Vector3 position, Vector3 direction, float distance, HordeTowerKind kind)
            => buildingPlacement != null && buildingPlacement.TryPlace(position,direction,distance,kind);

        bool ValidPlacement(Vector3 position)
            => ValidPlacement(position, SelectedTower);

        bool ValidPlacement(Vector3 position, HordeTowerKind kind)
            => buildingPlacement != null && buildingPlacement.CanPlace(position,kind);

        public void CancelPlacement() => placementController?.Cancel();

        public bool BeginPlacement(Vector3 position) => placementController.Begin(position,mapKind);

        public bool ConfirmPlacement(Vector3 direction)
            => ConfirmPlacement(direction, Range);

        public bool ConfirmPlacement(Vector3 direction, float distance) => placementController.Confirm(direction,distance);

        void UpdatePlacement()
        {
            placementController.Tick(CanBuild,PointerOverHud,mapKind,HordePointerInput.Read(viewCamera));
            placementView.Render(placementController,mapKind,SelectedTower);
        }

    }
}
