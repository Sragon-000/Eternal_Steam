using System;
using System.Reflection;
using System.Threading.Tasks;
using EternalSteam.Demo;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run only in a disposable Play session: changes scenes and resets battles, but isolates progress.
public static class VerifyFoundationRegression
{
    public static async Task<string> Main()
    {
        if (!Application.isPlaying) throw new Exception("Play mode required.");
        var profileField = typeof(HordeProgress).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
        var originalProfile = HordeProgress.Current;
        string key = "EternalSteam.FoundationRegression." + Guid.NewGuid();
        var profile = new HordeProgress(key);
        profileField.SetValue(null,profile);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var spawnMethod = typeof(HordeSimulation).GetMethod("Spawn",flags);
        var removeMethod = typeof(HordeSimulation).GetMethod("Remove",flags);
        var resultMethod = typeof(HordeSimulation).GetMethod("RecordResult",flags);
        int maps = 0;
        try
        {
            foreach (HordeMapKind map in Enum.GetValues(typeof(HordeMapKind)))
            {
                var load = SceneManager.LoadSceneAsync(HordeMapLayout.SceneName(map));
                while (!load.isDone) await Task.Yield();
                await Task.Delay(100);
                var simulation = UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
                Check(simulation != null && simulation.InLobby && simulation.MapKind == map,"Map lobby " + map);
                var spawn = (Action)Delegate.CreateDelegate(typeof(Action),simulation,spawnMethod);
                var remove = (Action<int,bool>)Delegate.CreateDelegate(typeof(Action<int,bool>),simulation,removeMethod);
                var record = (Action)Delegate.CreateDelegate(typeof(Action),simulation,resultMethod);
                int towers = simulation.TowerCount;
                Check(simulation.Deploy(1),"Deploy"); Check(!simulation.WaveStarted,"Wait for manual start");
                simulation.ToggleSpawning();
                for (int i = 0; i < simulation.StageEnemyTotal; i++) { spawn(); remove(0,i != 0); }
                Check(simulation.StageCleared && simulation.Health == HordeSimulation.MaxHealth-1,"Escape resolution " + map);
                int before = profile.Parts;
                record(); int reward = profile.Parts-before;
                Check(reward == 200,"First reward"); record(); Check(profile.Parts == before+reward,"No duplicate reward");
                var loaded = new HordeProgress(key);
                Check(loaded.Parts == profile.Parts && loaded.CanDeploy(map,2),"Save and unlock");
                simulation.ToggleSpawning();
                Check(simulation.StageNumber == 2 && simulation.TowerCount == towers && !simulation.WaveStarted,"Next stage retains towers");
                simulation.ToggleSpawning();
                for (int i = 0; i < HordeSimulation.MaxHealth; i++) { spawn(); remove(0,false); }
                record();
                Check(simulation.StageFailed && !simulation.StageCleared && profile.Parts == before+reward,"Defeat without reward");
                simulation.ResetEnemies();
                Check(simulation.Health == HordeSimulation.MaxHealth && simulation.Alive == 0 && simulation.TowerCount == towers,"Retry");
                simulation.ReturnToLobby();
                Check(simulation.InLobby && profile.UpgradeCapacity(HordeTowerKind.MachineGun),"Lobby capacity purchase");
                Check(new HordeProgress(key).TowerLimit(HordeTowerKind.MachineGun) == profile.TowerLimit(HordeTowerKind.MachineGun),"Capacity save");
                maps++;
            }
            return "PASS: " + maps + " maps; lobby/deploy/start/escape-clear/reward-once/save/unlock/next-stage/defeat/retry/capacity-save. Test profile isolated.";
        }
        finally
        {
            var simulation = UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
            if (simulation != null) simulation.ReturnToLobby();
            profileField.SetValue(null,originalProfile);
            PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
        }
    }
    static void Check(bool condition,string message) { if (!condition) throw new Exception(message); }
}
