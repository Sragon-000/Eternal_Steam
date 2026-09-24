using System;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using EternalSteam.Demo;
using EternalSteam.OpenWorld;
public static class BenchmarkMapSpawn
{
    public static string Main()
    {
        var result=new StringBuilder("Spawn spacing cache + 12 probes; standalone, 3 x 30 samples, no rendering/combat\n");
        foreach(int count in new[]{4000,10000,100000}){
            var world=new HordeEnemyWorld(capacity:100000);var layout=new EnemySpawnLayout(100000);
            for(int i=0;i<count;i++)world.TrySpawn(new Vector3(i%400,0,i/400),Vector3.zero);
            Action sample=()=>{layout.Begin(world,.75f);for(int i=0;i<12;i++)layout.IsFree(new Vector3(i,0,-1));};
            for(int i=0;i<5;i++)sample();var times=new double[30];
            for(int run=0;run<3;run++){
                long before=GC.GetAllocatedBytesForCurrentThread();
                for(int i=0;i<times.Length;i++){long start=Stopwatch.GetTimestamp();sample();times[i]=(Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency;}
                long allocated=GC.GetAllocatedBytesForCurrentThread()-before;Array.Sort(times);
                result.AppendLine($"{count} run{run+1}: median={times[15]:F3}ms p95={times[28]:F3}ms GC={allocated} bytes /30 samples");
            }
        }
        System.IO.File.WriteAllText("Temp/map-spawn-benchmark.txt",result.ToString());return result.ToString();
    }
}
