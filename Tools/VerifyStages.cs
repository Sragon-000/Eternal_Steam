using System;
using System.Reflection;
using EternalSteam.Demo;
using UnityEngine;

public static class VerifyStages
{
    public static string Main()
    {
        var sim = UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
        if (sim == null) throw new Exception("Run in Play mode");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var spawn = typeof(HordeSimulation).GetMethod("Spawn", flags);
        var remove = typeof(HordeSimulation).GetMethod("Remove", flags);
        var index = typeof(HordeSimulation).GetField("stageIndex", flags);
        int towers = sim.TowerCount;
        var profileField = typeof(HordeProgress).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
        var originalProfile = HordeProgress.Current;
        string key = "EternalSteam.StageTest." + Guid.NewGuid();
        Action<bool, string> check = (ok, message) => { if (!ok) throw new Exception(message); };
        try
        {
            profileField.SetValue(null, new HordeProgress(key));
            sim.ReturnToLobby();
            sim.Deploy(1);
            index.SetValue(sim, 0);
            sim.ResetEnemies();
            for (int stage = 1; stage <= 3; stage++)
            {
                check(sim.StageNumber == stage && !sim.WaveStarted && sim.Alive == 0, "Deployment");
                check(sim.StageEnemyTotal == new[] { 1500, 3000, 5000 }[stage - 1], "Quota");
                sim.ToggleSpawning();
                for (int n = 0; n < sim.StageEnemyTotal - 1; n++)
                {
                    spawn.Invoke(sim, null);
                    remove.Invoke(sim, new object[] { 0, true });
                }
                for (int n = 0; n < 10; n++) spawn.Invoke(sim, null);
                check(sim.Spawned == sim.StageEnemyTotal && !sim.StageCleared, "Hard quota; no premature clear");
                check(!sim.StageCleared && sim.Alive == 1, "Last enemy required");
                remove.Invoke(sim, new object[] { 0, true });
                check(sim.StageCleared && sim.AllStagesCleared == (stage == 3), "Clear state");
                sim.ToggleSpawning();
                check(sim.TowerCount == towers && !sim.WaveStarted && sim.Health == HordeSimulation.MaxHealth, "Next deployment preserves towers");
            }
            check(sim.StageNumber == 1, "Play again");
            sim.ToggleSpawning();
            for (int n = 0; n < sim.StageEnemyTotal; n++) spawn.Invoke(sim, null);
            remove.Invoke(sim, new object[] { 0, false });
            for (int n = 1; n < sim.StageEnemyTotal; n++) remove.Invoke(sim, new object[] { n, true });
            check(sim.StageCleared && !sim.StageFailed && sim.Health == HordeSimulation.MaxHealth - 1, "Escapes count as resolved");
            sim.ToggleSpawning();
            check(sim.StageNumber == 2 && !sim.WaveStarted, "Advance after escape clear");
            sim.ResetEnemies();
            check(!sim.StageFailed && sim.Spawned == 0 && !sim.WaveStarted, "Retry");
            return "PASS: 1500/3000/5000 quotas, last-enemy clear, next deployment, tower retention, final replay, escape resolution, retry";
        }
        finally
        {
            sim.ReturnToLobby();
            profileField.SetValue(null, originalProfile);
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }
}
