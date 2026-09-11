using System;
using UnityEngine;
using UnityEngine.UIElements;
using EternalSteam.Demo;
public static class VerifyHordeHud
{
    public static string Main()
    {
        var s = UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
        var root = UnityEngine.Object.FindFirstObjectByType<UIDocument>().rootVisualElement;
        Submit(root, "cannon");
        if (s.SelectedTower != HordeTowerKind.Cannon) throw new Exception("Cannon selector failed");
        Submit(root, "frost");
        if (s.SelectedTower != HordeTowerKind.Frost) throw new Exception("Frost selector failed");
        Submit(root, "machinegun");
        if (s.SelectedTower != HordeTowerKind.MachineGun) throw new Exception("Machine gun selector failed");
        Submit(root, "reset");
        if (s.Spawning || s.WaveStarted || s.Health != HordeSimulation.MaxHealth) throw new Exception("Deployment state failed");
        Submit(root, "low");
        if (s.SpawnRate != 50) throw new Exception("50/s button failed");
        Submit(root, "high");
        if (s.SpawnRate != 300) throw new Exception("300/s button failed");
        bool previous = s.Spawning;
        Submit(root, "pause");
        if (s.Spawning == previous) throw new Exception("Pause button failed");
        if (!s.WaveStarted) throw new Exception("Start wave button failed");
        Submit(root, "pause");
        Submit(root, "reset");
        if (s.Alive != 0 || s.Killed != 0 || s.Spawning || s.WaveStarted) throw new Exception("Reset button failed");
        return "PASS: UI Toolkit button actions (spawn rate, start wave, pause/resume, reset to deployment)";
    }
    static void Submit(VisualElement root, string name)
    {
        var button = root.Q<Button>(name);
        button.Focus();
        using (var e = NavigationSubmitEvent.GetPooled())
        {
            e.target = button;
            button.SendEvent(e);
        }
    }
}
