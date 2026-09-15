using System;
using System.Reflection;
using System.Threading.Tasks;
using EternalSteam.Demo;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class VerifyLegacyDeployment
{
    public static async Task<string> Main()
    {
        if(!Application.isPlaying) throw new Exception("Disposable Play session required.");
        var current=typeof(HordeProgress).GetField("current",BindingFlags.Static|BindingFlags.NonPublic);
        var original=HordeProgress.Current; string key="EternalSteam.DeploymentRegression."+Guid.NewGuid();
        current.SetValue(null,new HordeProgress(key));
        try
        {
            foreach(HordeMapKind map in Enum.GetValues(typeof(HordeMapKind)))
            {
                var load=SceneManager.LoadSceneAsync(HordeMapLayout.SceneName(map));while(!load.isDone) await Task.Yield();await Task.Delay(100);
                var s=UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
                Check(s.Deploy(1),"Deploy");s.ClearTowers();
                var grid=HordeMapLayout.GridBounds(HordeMapLayout.BuildZones(map)[0]);
                var p=new Vector3(grid.xMin+HordeMapLayout.BuildMargin,0,grid.yMin+HordeMapLayout.BuildMargin);
                Check(s.BeginPlacement(p),"Begin " + map);Check(s.ChoosingDirection && s.TowerCount==0,"Preview only");
                Check(!s.ConfirmPlacement(Vector3.zero,12) && s.ChoosingDirection,"Invalid direction retains edit");
                s.CancelPlacement();Check(!s.ChoosingDirection && s.TowerCount==0,"Cancel");
                Check(s.BeginPlacement(p),"Restart edit");Check(s.ConfirmPlacement(Vector3.forward,12),"Confirm");
                Check(s.TowerCount==1 && !s.ChoosingDirection,"Exactly one placement");Check(!s.BeginPlacement(p),"Occupied cell");
                s.SetBattleViewport(0.3f);
                s.ToggleSpawning();Check(!s.BeginPlacement(p+Vector3.right*2),"Combat blocks edit");
                s.ResetEnemies();Check(s.TowerCount==1 && !s.ChoosingDirection,"Retry retains tower");
                s.ReturnToLobby();
            }
            return "PASS: 3 maps; begin/preview/cancel/invalid-confirm/confirm/occupancy/viewport/combat-block/retry through original public facade.";
        }
        finally
        {
            var s=UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();if(s!=null)s.ReturnToLobby();
            current.SetValue(null,original);PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
        }
    }
    static void Check(bool result,string message) { if(!result)throw new Exception(message); }
}
