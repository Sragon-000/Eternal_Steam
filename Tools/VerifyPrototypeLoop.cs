using System;
using System.Reflection;
using EternalSteam.Demo;
using UnityEngine;
using UnityEngine.UIElements;

public static class VerifyPrototypeLoop
{
    public static string Main()
    {
        var sim = UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
        if (sim == null) throw new Exception("Play mode required");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var current = typeof(HordeProgress).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
        var original = HordeProgress.Current;
        string key = "EternalSteam.Test." + Guid.NewGuid();
        Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); };
        object Call(string name, params object[] args) => typeof(HordeSimulation).GetMethod(name, flags).Invoke(sim, args);
        var root = UnityEngine.Object.FindFirstObjectByType<HordeHud>().GetComponent<UIDocument>().rootVisualElement;
        void Click(string name)
        {
            var button = root.Q<Button>(name);
            check(button != null, "Missing button " + name);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
        }
        try
        {
            current.SetValue(null, new HordeProgress(key));
            sim.ReturnToLobby();
            check(sim.InLobby && !sim.WaveStarted && sim.Alive == 0, "Lobby idle");
            check(!sim.Deploy(2), "Locked stage");
            Click("stage1");
            check(!sim.InLobby && !sim.WaveStarted, "Deploy waits for start");
            Click("arrow");
            check(sim.SelectedTower == HordeTowerKind.Arrow, "Arrow selection");
            Call("Spawn");
            Call("ApplyDamage", 0, 10);
            check(sim.Alive == 1 && sim.Killed == 0, "Partial damage");
            Call("ApplyDamage", 0, 10);
            check(sim.Killed == 1, "Health depletion");
            sim.ResetEnemies();
            var enemies = (Array)typeof(HordeSimulation).GetField("enemies", flags).GetValue(sim);
            Vector3[] positions = { new Vector3(3, 0, 0), new Vector3(7, 0, 0), new Vector3(-3, 0, 0), new Vector3(15, 0, 0), new Vector3(5, 0, 3) };
            for (int i = 0; i < positions.Length; i++)
            {
                Call("Spawn");
                object enemy = enemies.GetValue(i);
                enemy.GetType().GetField("position").SetValue(enemy, positions[i]);
                enemy.GetType().GetField("health").SetValue(enemy, 30);
                enemies.SetValue(enemy, i);
            }
            Call("ApplyPiercingShot", Vector3.zero, Vector3.right, 12f);
            for (int i = 0; i < positions.Length; i++)
            {
                object enemy = enemies.GetValue(i);
                int hp = (int)enemy.GetType().GetField("health").GetValue(enemy);
                check(hp == (i < 2 ? 10 : 30), "Piercing once, excludes rear/side/range: " + i);
            }
            sim.ResetEnemies();
            Click("pause");
            check(sim.WaveStarted, "Start button");
            for (int i = 0; i < sim.StageEnemyTotal; i++) Call("Spawn");
            for (int i = 0; i < sim.StageEnemyTotal; i++) Call("Remove", i, true);
            Call("RecordResult");
            check(sim.StageCleared && sim.LastReward == 200 && HordeProgress.Current.Parts == 200, "First clear reward");
            Call("RecordResult");
            check(HordeProgress.Current.Parts == 200, "No repeated settlement");
            var loaded = new HordeProgress(key);
            check(loaded.Parts == 200 && loaded.CanDeploy(sim.MapKind, 2) && !loaded.CanDeploy(sim.MapKind, 3), "Save reload and unlock");
            check(loaded.AwardClear(sim.MapKind, 1, "repeat-test") == 100, "Replay reward");
            check(new HordeProgress(key).AwardClear(sim.MapKind, 1, "repeat-test") == 0, "Duplicate across reload");
            Click("pause");
            check(sim.StageNumber == 2 && !sim.WaveStarted, "Next deployment");
            Click("returnLobby");
            check(sim.InLobby && sim.Alive == 0, "Return lobby");
            check(HordeProgress.Current.Parts == 200, "Abandon gives no reward");
            return "PASS: lobby/deploy/start/return UI; arrow selection; health and piercing; first/repeat rewards; duplicate protection; reload; stage locks and next deployment";
        }
        finally
        {
            sim.ReturnToLobby();
            current.SetValue(null, original);
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
