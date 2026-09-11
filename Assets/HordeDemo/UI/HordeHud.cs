using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

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
        VisualElement panel;
        float nextUpdate;

        public void Configure(HordeSimulation value) => simulation = value;

        void Start()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            panel = root.Q("hud");
            stats = root.Q<Label>("stats");
            hint = root.Q<Label>("hint");
            pause = root.Q<Button>("pause");
            root.Q<Button>("low").clicked += () => simulation.SetSpawnRate(50);
            root.Q<Button>("medium").clicked += () => simulation.SetSpawnRate(100);
            root.Q<Button>("high").clicked += () => simulation.SetSpawnRate(300);
            pause.clicked += simulation.ToggleSpawning;
            root.Q<Button>("reset").clicked += simulation.ResetEnemies;
            root.Q<Button>("clear").clicked += simulation.ClearTowers;
        }

        void Update()
        {
            if (panel != null && panel.panel != null && Mouse.current != null)
            {
                Vector2 pointer = Mouse.current.position.ReadValue();
                pointer.y = Screen.height - pointer.y;
                simulation.PointerOverHud = panel.worldBound.Contains(RuntimePanelUtils.ScreenToPanel(panel.panel, pointer));
            }
            if (stats == null || Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 0.2f;
            stats.text = $"ALIVE  {simulation.Alive:N0} / {HordeSimulation.Capacity:N0}     KILLED  {simulation.Killed:N0}     ESCAPED  {simulation.Escaped:N0}\nTURRETS  {simulation.TowerCount}     SPAWN  {simulation.SpawnRate}/s     FPS  {simulation.Fps:F0}";
            hint.text = simulation.AtCapacity ? "Enemy cap reached — spawns resume when space is free" : simulation.PlacementHint;
            pause.text = simulation.Spawning ? "Pause spawns" : "Resume spawns";
        }

        void OnDisable()
        {
            if (simulation != null) simulation.PointerOverHud = false;
        }
    }
}
