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
        Submit(root, "low");
        if (s.SpawnRate != 50) throw new Exception("50/s button failed");
        Submit(root, "high");
        if (s.SpawnRate != 300) throw new Exception("300/s button failed");
        bool previous = s.Spawning;
        Submit(root, "pause");
        if (s.Spawning == previous) throw new Exception("Pause button failed");
        Submit(root, "pause");
        Submit(root, "reset");
        if (s.Alive != 0 || s.Killed != 0) throw new Exception("Reset button failed");
        return "PASS: UI Toolkit button actions (spawn rate, pause/resume, reset)";
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
