using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class VerifyMapAssaultIntegration
{
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    public static string Main()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(Application.isPlaying&&s.Assault!=null,"Authored assault required");
        var input=s.GetComponent<OpenWorldInput>();input.Cancel();s.Clock.SetPhase(DayPhase.Day);s.Clock.Paused=true;
        var main=s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.main_base");bool placed=s.Content.MainBase!=null;
        for(int z=-30;z<40&&!placed;z++)for(int x=-30;x<40&&!placed;x++)if(s.Content.GroundPlacement.Add(main,new Vector2Int(x,z),out _).Success)placed=s.Content.GroundPlacement.Confirm().Success;
        Check(placed,"Main installation");var assault=s.Assault;assault.Tick(1);
        Check(assault.Energy.Extracted>0,"Confirmed base extraction");Check(assault.Planner.Pending==0,"No daytime budget");
        long initial=assault.Energy.Extracted;int covered=assault.Coverage.Covered.Count(x=>x);assault.Tick(1);Check(assault.Energy.Extracted-initial==covered*s.AssaultSettings.ExtractionPerSecond,"Unique covered-cell rate");
        s.Clock.SetPhase(DayPhase.Night);assault.Tick(.3);Check(assault.Planner.Points.Count==2,"Main + one night points");long budget=assault.Planner.Pending;var point=assault.Planner.Points[0];assault.Tick(.001);Check(assault.Planner.Pending==budget&&assault.Planner.Points[0]==point,"Night stable");
        assault.Energy.RewardKill(long.MaxValue);s.Clock.SetPhase(DayPhase.Day);assault.Tick(.001);Check(assault.Energy.Stage==MapStage.WaitingForNight,"Day threshold waits");s.Clock.SetPhase(DayPhase.Night);assault.Tick(.001);Check(assault.BossId>=0&&assault.Energy.Stage==MapStage.BossBattle,"Actual boss spawned");
        int boss=assault.BossId;s.Enemies.ApplyDamage(boss,int.MaxValue);Check(assault.Energy.Stage==MapStage.AwaitingCraft&&assault.Energy.Balance==assault.Energy.Target,"Actual kill callback reward");
        Check(assault.Craft()&&!assault.Craft()&&assault.Energy.PerfectOrb&&assault.Planner.Pending==0,"Actual crafting once and stop budget");
        using(var edge=new MapAssaultController(s,s.AssaultSettings)){
            edge.Energy.RewardKill(edge.Energy.PreBossLimit-1);s.Clock.SetPhase(DayPhase.Night);
            int victim=-1;Action<int> capture=id=>victim=id;s.Enemies.SpawnedEnemy+=capture;s.Enemies.TrySpawn(Vector3.zero,Vector3.one,1,1);s.Enemies.SpawnedEnemy-=capture;
            s.Enemies.ApplyDamage(victim,1);Check(edge.Energy.Stage==MapStage.BossBattle,"Night kill latches boss immediately");
            s.Clock.SetPhase(DayPhase.Day);edge.Tick(.001);Check(edge.BossId>=0,"Dawn cannot postpone latched encounter");
        }
        var root=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>().GetComponent<UIDocument>().rootVisualElement;Check(root.Q("map-progress")!=null&&root.Q<Button>("orb-craft")!=null,"Authored UXML");
        return $"PASS: installed base extraction ({covered} cells), stable 2 night points, real boss kill, exact reward, craft and UXML. Deterministic integration; not elapsed frame timing.";
    }
}
