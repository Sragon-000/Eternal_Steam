using System;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using EternalSteam.Demo;

// Disposable Play session only. No build, scene serialization or production settings changes.
// The simulation runs its normal Update; this observer reads completed profiler frames.
public static class MeasureLegacyEditorFrames
{
    const int Samples=600, Warmup=120;
    const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly string[] Names={"Main Thread","PlayerLoop","GC Allocated In Frame","CPU Main Thread Frame Time","GPU Frame Time"};
    static readonly long[,] Values=new long[Names.Length,Samples];
    static ProfilerRecorder[] recorders;
    static HordeSimulation sim;
    static Array enemies;
    static Action spawn;
    static int run, frame, lastFrame, oldVsync, oldTarget;
    static float oldCapture;
    static Camera camera;
    static RenderTexture texture, oldTexture;
    static string[] reports;
    static bool running;

    public static string Main()
    {
        if(running) throw new Exception("Already measuring");
        sim=UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
        if(!Application.isPlaying || sim==null || sim.MapKind!=HordeMapKind.Lane || !sim.InLobby)
            throw new Exception("Open HordeDemo Play mode in its lobby, in a disposable copy");
        oldVsync=QualitySettings.vSyncCount; oldTarget=Application.targetFrameRate; oldCapture=Time.captureDeltaTime;
        camera=(Camera)typeof(HordeSimulation).GetField("viewCamera",F).GetValue(sim); oldTexture=camera.targetTexture;
        recorders=new ProfilerRecorder[Names.Length]; reports=new string[3];
        running=true;
        try {
            QualitySettings.vSyncCount=0; Application.targetFrameRate=-1; Time.captureDeltaTime=1f/60;
            texture=new RenderTexture(1024,576,24); texture.Create(); camera.targetTexture=texture;
            sim.ResetEnemies(); sim.ClearTowers();
            var positions=HordeMapLayout.StartingTowers(sim.MapKind);
            for(int i=0;i<4;i++) if(!sim.TryPlaceTower(positions[i],HordeMapLayout.DefaultDirection(sim.MapKind,positions[i]),22,(HordeTowerKind)i)) throw new Exception("Fixed placement failed");
            if(!sim.Deploy(1)) throw new Exception("Deploy failed");
            var type=typeof(HordeSimulation);
            spawn=(Action)type.GetMethod("Spawn",F).CreateDelegate(typeof(Action),sim);
            var old=type.GetField("enemies",F);
            if(old!=null) enemies=(Array)old.GetValue(sim);
            else {var world=type.GetField("enemyWorld",F).GetValue(sim); enemies=(Array)world.GetType().GetField("enemies",F).GetValue(world);}
            var handles=new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            for(int i=0;i<Names.Length;i++) foreach(var handle in handles) {
                var d=ProfilerRecorderHandle.GetDescription(handle);
                if(d.Name==Names[i]) {recorders[i]=ProfilerRecorder.StartNew(d.Category,d.Name,1);break;}
            }
            for(int i=0;i<3;i++) if(!recorders[i].Valid) throw new Exception("Required counter unavailable: "+Names[i]);
            run=0; Setup(); EditorApplication.update+=Observe;
            return "Started: 3 runs, 120 warmup + 600 actual frames each. Poll Temp/legacy-editor-frames-status.txt. No build.";
        } catch { Cleanup(); throw; }
    }
    static void Setup()
    {
        sim.ResetEnemies(); typeof(HordeSimulation).GetField("stageIndex",F).SetValue(sim,2);
        for(int i=0;i<4000;i++) spawn();
        var random=new System.Random(731);
        for(int i=0;i<4000;i++) {
            object enemy=enemies.GetValue(i); var t=enemy.GetType();
            var p=new Vector3(-16+(float)random.NextDouble()*32,.55f,-5.5f+(float)random.NextDouble()*11);
            t.GetField("position").SetValue(enemy,p);t.GetField("destination").SetValue(enemy,new Vector3(41,.55f,p.z));
            t.GetField("health").SetValue(enemy,1000000000);t.GetField("speed").SetValue(enemy,0f); enemies.SetValue(enemy,i);
        }
        typeof(HordeSimulation).GetField("waveStarted",F).SetValue(sim,true);
        // spawning remains false: the 4000 simultaneous targets are installed above.
        frame=0; lastFrame=Time.frameCount;
        File.WriteAllText("Temp/legacy-editor-frames-status.txt","running "+(run+1)+"/3");
    }
    static void Observe()
    {
        try {
            if(!Application.isPlaying) throw new Exception("Play mode ended during measurement");
            int current=Time.frameCount; if(current==lastFrame)return;
            // Never silently count skipped game frames as a contiguous capture.
            if(current!=lastFrame+1) throw new Exception("Observer missed frames: "+(current-lastFrame));
            lastFrame=current;
            if(frame>=Warmup) {
                int index=frame-Warmup;
                for(int i=0;i<Names.Length;i++) Values[i,index]=recorders[i].Valid?recorders[i].LastValue:0;
            }
            frame++;
            if(frame<Warmup+Samples)return;
            if(sim.Alive!=4000 || sim.Killed!=0 || sim.Escaped!=0) throw new Exception("Stress population drift");
            var rows=new string[Names.Length];
            for(int i=0;i<Names.Length;i++) {
                var data=new long[Samples]; long sum=0;int positive=0;
                for(int n=0;n<Samples;n++){data[n]=Values[i,n];sum+=data[n];if(data[n]>0)positive++;}
                Array.Sort(data);
                double scale=i==2?1:1e-6;
                rows[i]=string.Format(CultureInfo.InvariantCulture,"{{\"name\":\"{0}\",\"unit\":\"{1}\",\"median\":{2},\"p95\":{3},\"sumRaw\":{4},\"positiveFrames\":{5}}}",Names[i],i==2?"bytes":"ms",data[300]*scale,data[569]*scale,sum,positive);
            }
            reports[run]="{\"run\":"+(run+1)+",\"alive\":"+sim.Alive+",\"counters\":["+string.Join(",",rows)+"]}";
            run++;
            if(run<3) {Setup();return;}
            var json="{\"unity\":\""+Application.unityVersion+"\",\"cpu\":\""+SystemInfo.processorType+"\",\"mode\":\"batchmode Editor, graphics enabled, 1024x576 camera RenderTexture\",\"samplesPerRun\":600,\"warmupFrames\":120,\"seed\":731,\"fixedDelta\":0.016666667,\"scenario\":\"normal simulation Update and HUD; four tower types; 4000 stationary high-health targets; no spawning; no build\",\"runs\":["+string.Join(",",reports)+"]}";
            File.WriteAllText("Temp/legacy-editor-frames.json",json);
            Cleanup(); File.WriteAllText("Temp/legacy-editor-frames-status.txt","completed");
        } catch(Exception e) {Cleanup();File.WriteAllText("Temp/legacy-editor-frames-status.txt","failed: "+e);}
    }
    static void Cleanup()
    {
        EditorApplication.update-=Observe;
        if(recorders!=null)for(int i=0;i<recorders.Length;i++)recorders[i].Dispose();
        if(sim!=null)sim.ReturnToLobby();
        Time.captureDeltaTime=oldCapture;QualitySettings.vSyncCount=oldVsync;Application.targetFrameRate=oldTarget;
        if(camera!=null)camera.targetTexture=oldTexture;
        if(texture!=null){texture.Release();UnityEngine.Object.Destroy(texture);}
        running=false;
    }
}
