using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using EternalSteam.Demo;
using UnityEngine;
using UnityEngine.SceneManagement;
public static class VerifyLegacyBuildingWorld
{
    public static async Task<string> Main()
    {
        if(!Application.isPlaying)throw new Exception("Disposable Play session required.");
        var field=typeof(HordeProgress).GetField("current",BindingFlags.Static|BindingFlags.NonPublic);var original=HordeProgress.Current;
        string key="EternalSteam.WorldBridgeRegression."+Guid.NewGuid();field.SetValue(null,new HordeProgress(key));
        try
        {
            foreach(HordeMapKind map in Enum.GetValues(typeof(HordeMapKind)))
            {
                var load=SceneManager.LoadSceneAsync(HordeMapLayout.SceneName(map));while(!load.isDone)await Task.Yield();await Task.Delay(100);
                var s=UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();var world=s.BuildingWorld;
                Check(world!=null && world.Buildings.Count==s.TowerCount,"Initial tower/world agreement");
                foreach(var b in world.Buildings)Check(world.Grid.Center(b.Cell,b.Footprint)==b.Position,"Legacy center");
                s.ClearTowers();Check(world.Buildings.Count==0 && world.Grid.OccupiedCount==0,"Clear both registries");
                var grid=HordeMapLayout.GridBounds(HordeMapLayout.BuildZones(map)[0]);var origin=new Vector3(grid.xMin+HordeMapLayout.BuildMargin,0,grid.yMin+HordeMapLayout.BuildMargin);
                int i=0;
                foreach(HordeTowerKind kind in Enum.GetValues(typeof(HordeTowerKind)))
                {
                    var p=origin+Vector3.right*HordeMapLayout.BuildCellSize*i++;
                    Check(s.TryPlaceTower(p,Vector3.forward,12,kind),"Place "+map+" "+kind);
                    Check(!s.TryPlaceTower(p,Vector3.forward,12,kind),"Duplicate cell rejected");
                }
                Check(world.Buildings.Count==4 && s.TowerCount==4 && world.Grid.ReservationCount==0,"All four towers committed");
                Check(world.Buildings.Select(x=>x.DefinitionId).Distinct().Count()==4,"Explicit stable IDs");
                var first=world.Buildings.First();world.Remove(first.Id);
                Check(s.TowerCount==3 && world.Grid.OccupiedCount==3,"Common removal cleans legacy tower");
                Check(s.Deploy(1),"Deploy");var ids=world.Buildings.Select(x=>x.Id).ToArray();s.ResetEnemies();
                Check(ids.SequenceEqual(world.Buildings.Select(x=>x.Id)),"Retry preserves common instances");
                s.ToggleSpawning();Check(!s.TryPlaceTower(first.Position,Vector3.forward,12),"Combat blocks common placement");
                s.ReturnToLobby();s.ClearTowers();Check(s.TowerCount==0 && world.Buildings.Count==0 && world.Grid.ReservationCount==0,"Final cleanup");
            }
            return "PASS: 3 maps; initial/4-kind placement/center/unique IDs/occupancy/common removal/retry/combat block/cleanup.";
        }
        finally
        {
            var s=UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();if(s!=null)s.ReturnToLobby();
            field.SetValue(null,original);PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
        }
    }
    static void Check(bool condition,string message) { if(!condition)throw new Exception(message); }
}
