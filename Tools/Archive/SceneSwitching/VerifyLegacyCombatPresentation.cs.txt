using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using EternalSteam.Demo;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VerifyLegacyCombatPresentation
{
    public static async Task<string> Main()
    {
        if(!Application.isPlaying) throw new Exception("Play mode required");
        const BindingFlags f=BindingFlags.Instance|BindingFlags.NonPublic;
        var profileField=typeof(HordeProgress).GetField("current",BindingFlags.Static|BindingFlags.NonPublic);
        var original=HordeProgress.Current; string key="EternalSteam.CombatTest."+Guid.NewGuid();
        profileField.SetValue(null,new HordeProgress(key));
        try {
            foreach(HordeMapKind map in Enum.GetValues(typeof(HordeMapKind))) {
                var load=SceneManager.LoadSceneAsync(HordeMapLayout.SceneName(map));
                while(!load.isDone) await Task.Yield(); await Task.Delay(100);
                var s=UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
                Check(s.Deploy(1),"Deploy "+map); s.ClearTowers();
                var positions=HordeMapLayout.StartingTowers(map);
                for(int i=0;i<4;i++) Check(s.TryPlaceTower(positions[i],HordeMapLayout.DefaultDirection(map,positions[i]),22,(HordeTowerKind)i),"Four kinds "+map);
                var towers=(List<HordeTower>)typeof(HordeSimulation).GetField("towers",f).GetValue(s);
                var tick=(Action<float>)typeof(HordeSimulation).GetMethod("UpdateTowers",f).CreateDelegate(typeof(Action<float>),s);
                tick(1); foreach(var t in towers) Check(t.shot==0 && !t.tracer.enabled && t.coverage.enabled,"Quiet preparation");
                // Preserve the legacy per-placement initial stagger (index * .013 seconds).
                s.ToggleSpawning(); tick(.05f);
                foreach(var t in towers) {
                    Check(t.shot==1 && t.tracer.enabled && !t.coverage.enabled,"Unconditional fire without enemies: "+map+" "+t.kind+" shots="+t.shot+" tracer="+t.tracer.enabled+" coverage="+t.coverage.enabled+" wave="+s.WaveStarted+" lobby="+s.InLobby);
                    Vector3 ray=t.tracer.GetPosition(1)-t.head.position;
                    Check(Mathf.Abs(ray.magnitude-t.range)<.001f,"Configured tracer range");
                    Check(Vector3.Angle(t.head.forward,ray)<=t.halfAngle+.01f,"Spray angle");
                    if(t.kind==HordeTowerKind.Frost) Check(t.impact.enabled && t.impact.positionCount==35,"Frost cone");
                    if(t.kind==HordeTowerKind.Cannon) Check(t.impact.enabled && t.impact.positionCount==33,"Cannon blast");
                }
                tick(.001f); foreach(var t in towers) Check(t.shot==1,"Cooldown survives next tick");
                tick(1); foreach(var t in towers) Check(t.shot==2,"Exactly one shot each tick, no catchup bursts");
                s.ResetEnemies(); foreach(var t in towers) Check(t.shot==0 && t.cooldown==0 && !t.tracer.enabled && (t.impact==null || !t.impact.enabled) && t.coverage.enabled,"Reset effects and combat state");
                s.ReturnToLobby();
            }
            return "PASS: 3 maps, four attack kinds, preparation silence, unconditional fire, tracer range/cone, frost/blast geometry, cooldown, reset.";
        } finally {
            var s=UnityEngine.Object.FindFirstObjectByType<HordeSimulation>(); if(s!=null)s.ReturnToLobby();
            profileField.SetValue(null,original); PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
        }
    }
    static void Check(bool value,string message) { if(!value)throw new Exception(message); }
}
