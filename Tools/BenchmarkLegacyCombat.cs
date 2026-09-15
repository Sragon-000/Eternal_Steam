using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using EternalSteam.Demo;
using UnityEngine;

// Run in the isolated validation copy, in Play mode. No rewards or profile writes.
// Measures controlled simulation + render submission CPU, NOT whole-frame/GPU time.
public static class BenchmarkLegacyCombat
{
    [Serializable] public class Run { public double medianMs, p95Ms; public long allocatedBytes; public int initialAlive, finalAlive, killed; public string checksum; }
    [Serializable] public class Report { public string unity, device, scope; public int seed=731, capacity=4000, samples=600; public float dt=1f/60; public Run[] runs=new Run[3]; }
    public static string Main()
    {
        var sim=UnityEngine.Object.FindFirstObjectByType<HordeSimulation>();
        if(sim==null || !Application.isPlaying) throw new Exception("Play mode required");
        const BindingFlags f=BindingFlags.Instance|BindingFlags.NonPublic;
        var t=typeof(HordeSimulation);
        Action Bind(string n)=>(Action)t.GetMethod(n,f).CreateDelegate(typeof(Action),sim);
        Action<float> Tick(string n)=>(Action<float>)t.GetMethod(n,f).CreateDelegate(typeof(Action<float>),sim);
        var spawn=Bind("Spawn"); var move=Tick("MoveAndIndex"); var towers=Tick("UpdateTowers"); var draw=Bind("DrawEnemies");
        Array Data() {
            var old=t.GetField("enemies",f);
            if(old!=null) return (Array)old.GetValue(sim);
            var world=t.GetField("enemyWorld",f).GetValue(sim);
            return (Array)world.GetType().GetField("enemies",f).GetValue(world);
        }
        void Reset() {
            sim.ResetEnemies(); t.GetField("stageIndex",f).SetValue(sim,2);
            for(int i=0;i<4000;i++) spawn();
            if(sim.Alive!=4000) throw new Exception("Stress scenario must begin with 4000 simultaneous enemies");
            var data=Data(); var random=new System.Random(731);
            for(int i=0;i<data.Length;i++) {
                object enemy=data.GetValue(i); var et=enemy.GetType();
                var position=new Vector3(-16+(float)random.NextDouble()*32,0.55f,-5.5f+(float)random.NextDouble()*11);
                et.GetField("position").SetValue(enemy,position);
                et.GetField("destination").SetValue(enemy,new Vector3(41,0.55f,position.z));
                data.SetValue(enemy,i);
            }
            t.GetField("waveStarted",f).SetValue(sim,true);
        }
        var report=new Report {unity=Application.unityVersion,device=SystemInfo.processorType,
            scope="Editor main-thread controlled MoveAndIndex + UpdateTowers + DrawEnemies submission; excludes UI, Editor frame, GPU, reset and spawn; Lane, four fixed starting positions with MG/Cannon/Frost/Arrow; seeded in-range distribution"};
        bool wasEnabled=sim.enabled;
        try {
            sim.enabled=false;
            if(sim.MapKind!=HordeMapKind.Lane) throw new Exception("Use HordeDemo Lane scene");
            sim.ResetEnemies(); sim.ClearTowers();
            var locations=HordeMapLayout.StartingTowers(sim.MapKind);
            for(int i=0;i<4;i++) if(!sim.TryPlaceTower(locations[i],HordeMapLayout.DefaultDirection(sim.MapKind,locations[i]),22,(HordeTowerKind)i))
                throw new Exception("Fixed tower placement failed");
            for(int run=0;run<3;run++) {
                Reset(); for(int i=0;i<120;i++){move(report.dt);towers(report.dt);draw();}
                var times=new double[report.samples]; long bytes=0;
                for(int i=0;i<times.Length;i++) {
                    if(i%100==0) Reset();
                    long allocated=GC.GetAllocatedBytesForCurrentThread(); long start=Stopwatch.GetTimestamp();
                    move(report.dt); towers(report.dt); draw();
                    long end=Stopwatch.GetTimestamp(); bytes+=GC.GetAllocatedBytesForCurrentThread()-allocated;
                    times[i]=(end-start)*1000.0/Stopwatch.Frequency;
                }
                Array.Sort(times);
                ulong hash=14695981039346656037UL;
                foreach(object enemy in Data()) {
                    var et=enemy.GetType();
                    foreach(string name in new[]{"position","destination","speed","alive","slowTime","health"}) {
                        var value=et.GetField(name).GetValue(enemy);
                        unchecked { hash=(hash^(uint)value.GetHashCode())*1099511628211UL; }
                    }
                }
                report.runs[run]=new Run {medianMs=times[300],p95Ms=times[569],allocatedBytes=bytes,
                    initialAlive=4000,finalAlive=sim.Alive,killed=sim.Killed,checksum=hash.ToString("X16")};
            }
            var rows=new string[3];
            for(int i=0;i<3;i++) { var r=report.runs[i]; rows[i]=string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "{{\"medianMs\":{0},\"p95Ms\":{1},\"allocatedBytes\":{2},\"initialAlive\":{3},\"finalAlive\":{4},\"killed\":{5},\"checksum\":\"{6}\"}}",
                r.medianMs,r.p95Ms,r.allocatedBytes,r.initialAlive,r.finalAlive,r.killed,r.checksum); }
            string header=JsonUtility.ToJson(report,true);
            string json=header.Substring(0,header.LastIndexOf('}'))+",\"runs\":["+string.Join(",",rows)+"]}";
            File.WriteAllText(Path.Combine(Application.dataPath,"../Temp/legacy-combat-benchmark.json"),json);
            return json;
        } finally { sim.ResetEnemies(); t.GetField("stageIndex",f).SetValue(sim,0); sim.enabled=wasEnabled; }
    }
}
