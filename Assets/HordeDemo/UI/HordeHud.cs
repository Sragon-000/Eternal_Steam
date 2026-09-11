using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace EternalSteam.Demo
{
    [RequireComponent(typeof(UIDocument))]
    [DefaultExecutionOrder(-100)]
    public sealed class HordeHud : MonoBehaviour
    {
        [SerializeField] HordeSimulation simulation;
        Label stats;
        Label hint;
        Button pause;
        Button reset;
        Button clear;
        Button[] weapons;
        ProgressBar health;
        ProgressBar combatHealth;
        ScrollView panel;
        bool wasInLobby;
        bool screenInitialized;
        bool wasFighting;
        VisualElement lobby;
        VisualElement battle;
        Label wallet;
        Label progress;
        Button[] stages;
        Button[] upgrades;
        static readonly string[] TowerNames = { "기관총", "대포", "냉각", "화살" };
        float nextUpdate;

        public void Configure(HordeSimulation value) => simulation = value;

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            panel = root.Q<ScrollView>("hud");
            lobby = root.Q("lobby");
            battle = root.Q("battle");
            wallet = root.Q<Label>("wallet");
            progress = root.Q<Label>("progress");
            stages = new[] { root.Q<Button>("stage1"), root.Q<Button>("stage2"), root.Q<Button>("stage3") };
            upgrades = new Button[4];
            for (int i = 0; i < upgrades.Length; i++)
            {
                var kind = (HordeTowerKind)i;
                upgrades[i] = root.Q<Button>("upgrade" + i);
                upgrades[i].clicked += () => simulation.UpgradeTowerCapacity(kind);
            }
            for (int i = 0; i < stages.Length; i++)
            {
                int stage = i + 1;
                stages[i].clicked += () => simulation.Deploy(stage);
            }
            root.Q<Button>("returnLobby").clicked += simulation.ReturnToLobby;
            stats = root.Q<Label>("stats");
            hint = root.Q<Label>("hint");
            pause = root.Q<Button>("pause");
            reset = root.Q<Button>("reset");
            clear = root.Q<Button>("clear");
            health = root.Q<ProgressBar>("health");
            health.highValue = HordeSimulation.MaxHealth;
            combatHealth = root.Q<ProgressBar>("combatHealth");
            combatHealth.highValue = HordeSimulation.MaxHealth;
            weapons = new[] { root.Q<Button>("machinegun"), root.Q<Button>("cannon"), root.Q<Button>("frost"), root.Q<Button>("arrow") };
            for (int i = 0; i < weapons.Length; i++)
            {
                var kind = (HordeTowerKind)i;
                weapons[i].clicked += () => simulation.SelectTower(kind);
                weapons[i].tooltip = HordeTowerStats.Description(kind);
            }
            root.Q<Button>("low").clicked += () => simulation.SetSpawnRate(50);
            root.Q<Button>("medium").clicked += () => simulation.SetSpawnRate(100);
            root.Q<Button>("high").clicked += () => simulation.SetSpawnRate(300);
            pause.clicked += simulation.ToggleSpawning;
            root.Q<Button>("reset").clicked += simulation.ResetEnemies;
            root.Q<Button>("clear").clicked += simulation.ClearTowers;
            root.Q<Button>("lane").clicked += () => SwitchMap(HordeMapKind.Lane);
            root.Q<Button>("wide").clicked += () => SwitchMap(HordeMapKind.WideFront);
            root.Q<Button>("pincer").clicked += () => SwitchMap(HordeMapKind.Pincer);
            root.Q<Button>(simulation.MapKind == HordeMapKind.Lane ? "lane" : simulation.MapKind == HordeMapKind.WideFront ? "wide" : "pincer").AddToClassList("selected");
        }

        void SwitchMap(HordeMapKind kind)
        {
            if (kind == simulation.MapKind) return;
            simulation.CancelPlacement();
            SceneManager.LoadScene(HordeMapLayout.SceneName(kind));
        }

        void Update()
        {
            bool fighting = !simulation.InLobby && simulation.WaveStarted && !simulation.StageEnded;
            if (combatHealth != null)
            {
                combatHealth.EnableInClassList("hidden", !fighting);
                combatHealth.value = simulation.Health;
                combatHealth.title = $"플레이어 체력 {simulation.Health:N0} / {HordeSimulation.MaxHealth:N0}";
                combatHealth.EnableInClassList("critical", simulation.Health <= HordeSimulation.MaxHealth / 4);
            }
            if (panel != null)
            {
                panel.EnableInClassList("combat-hidden", fighting);
                if (wasFighting != fighting)
                {
                    panel.scrollOffset = Vector2.zero;
                    nextUpdate = 0;
                    wasFighting = fighting;
                }
            }
            if (panel != null && panel.panel != null)
            {
                float totalWidth = GetComponent<UIDocument>().rootVisualElement.worldBound.width;
                if (totalWidth > 0) simulation.SetBattleViewport(fighting ? 0 : panel.worldBound.xMax / totalWidth);
            }
            simulation.PointerOverHud = false;
            if (panel != null && panel.panel != null && Mouse.current != null)
            {
                Vector2 pointer = Mouse.current.position.ReadValue();
                pointer.y = Screen.height - pointer.y;
                simulation.PointerOverHud = !fighting && (simulation.InLobby || panel.worldBound.Contains(RuntimePanelUtils.ScreenToPanel(panel.panel, pointer)));
            }
            if (stats == null || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.2f;
            if (!screenInitialized || wasInLobby != simulation.InLobby)
            {
                panel.scrollOffset = Vector2.zero;
                wasInLobby = simulation.InLobby;
                screenInitialized = true;
            }
            lobby.EnableInClassList("hidden", !simulation.InLobby);
            battle.EnableInClassList("hidden", simulation.InLobby);
            var profile = HordeProgress.Current;
            wallet.text = $"부품 {profile.Parts:N0}   /   기지 1레벨";
            progress.text = $"{HordeMapLayout.Title(simulation.MapKind)} / 클리어 {profile.Cleared(simulation.MapKind)}/3\n첫 클리어 추가 보상 · 반복 보상 지급";
            for (int i = 0; i < stages.Length; i++)
            {
                bool unlocked = profile.CanDeploy(simulation.MapKind, i + 1);
                stages[i].text = unlocked ? $"{i + 1}단계 출격  ·  보상 {profile.RewardPreview(simulation.MapKind, i + 1)} 부품" : $"{i + 1}단계 · 이전 단계 클리어 필요";
                stages[i].SetEnabled(unlocked);
            }
            stats.text = $"{HordeMapLayout.Title(simulation.MapKind)}   /   단계 {simulation.StageNumber}/{simulation.StageCount}     처리 {simulation.Resolved:N0}/{simulation.StageEnemyTotal:N0}\n남은 적 {simulation.Alive:N0}   출현 대기 {simulation.RemainingToSpawn:N0}   처치 {simulation.Killed:N0}   통과 {simulation.Escaped:N0}\n포탑 {simulation.TowerCount}   초당 {simulation.SpawnRate}마리   초당 프레임 {simulation.Fps:F0}";
            hint.text = simulation.PlacementHint;
            if (simulation.StageEnded) hint.text += simulation.StageCleared ? $"\n보상: 부품 +{simulation.LastReward} (저장 완료)" : "\n보상 없음 — 재도전하거나 로비로 돌아가세요.";
            pause.text = simulation.StageFailed ? "방어 실패" : simulation.AllStagesCleared ? "처음부터" : simulation.StageCleared ? "다음 단계" : !simulation.WaveStarted ? "전투 시작" : simulation.RemainingToSpawn == 0 ? $"남은 적 {simulation.Alive:N0}마리" : simulation.Spawning ? "출현 일시정지" : "출현 재개";
            pause.SetEnabled(!simulation.StageFailed && (simulation.StageCleared || simulation.RemainingToSpawn > 0));
            reset.text = "재도전";
            clear.SetEnabled(simulation.CanBuild);
            health.value = simulation.Health;
            health.title = simulation.Defeated ? "플레이어 체력 0 — 패배" : $"플레이어 체력 {simulation.Health:N0} / {HordeSimulation.MaxHealth:N0}";
            health.EnableInClassList("critical", simulation.Health <= HordeSimulation.MaxHealth / 4);
            for (int i = 0; i < weapons.Length; i++)
            {
                var kind = (HordeTowerKind)i;
                int limit = profile.TowerLimit(kind);
                weapons[i].text = $"{TowerNames[i]} {simulation.TowerCountFor(kind)}/{limit}";
                upgrades[i].text = limit >= 16 ? $"{TowerNames[i]} 16개 / 최대" : $"{TowerNames[i]}  {limit} → {limit + 1}개  ·  {profile.UpgradeCost(kind)} 부품";
                upgrades[i].SetEnabled(simulation.InLobby && profile.CanUpgrade(kind));
                weapons[i].EnableInClassList("selected", i == (int)simulation.SelectedTower);
                weapons[i].SetEnabled(simulation.CanBuild);
            }
        }

        void OnDisable()
        {
            if (simulation != null) simulation.PointerOverHud = false;
        }
    }
}
