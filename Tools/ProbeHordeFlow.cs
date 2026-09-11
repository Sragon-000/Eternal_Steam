using System;
using UnityEngine;
using UnityEngine.UIElements;
using EternalSteam.Demo;

// Manual CLI smoke-test steps. Run one entry at a time so Unity can process scene changes.
public static class ProbeHordeFlow
{
    public static string Start()
    {
        var s = UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
        if (s.Spawning || s.WaveStarted || s.Alive != 0) throw new Exception("Expected deployment before Start wave.");
        Submit("pause");
        if (!s.Spawning || !s.WaveStarted) throw new Exception("Start wave did not start spawning.");
        return "PASS: Start wave button starts spawning.";
    }
    public static string Wide() { Submit("wide"); return "Switch requested: WideFront"; }
    public static string Pincer() { Submit("pincer"); return "Switch requested: Pincer"; }
    public static string Lane() { Submit("lane"); return "Switch requested: Lane"; }
    public static string CheckDeployment()
    {
        var s = UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
        if (s.Spawning || s.WaveStarted || s.Alive != 0) throw new Exception("Map did not enter deployment.");
        return "PASS: deployment on " + s.MapKind + ", towers=" + s.TowerCount;
    }
    static void Submit(string name)
    {
        var button = UnityEngine.Object.FindFirstObjectByType<UIDocument>().rootVisualElement.Q<Button>(name);
        button.Focus();
        using (var e = NavigationSubmitEvent.GetPooled()) { e.target = button; button.SendEvent(e); }
    }
}
