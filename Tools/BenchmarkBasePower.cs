using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using EternalSteam;
public static class BenchmarkBasePower
{
    public static string Main()
    {
        var output=new StringBuilder("Power Tick only; 3 x 120 samples per device count, 1-second settlements each sample; excludes combat/rendering/enemy updates\n");
        foreach(int count in new[]{100,1000}){
            var registry=new BaseRegistry(2,Quaternion.identity){AnyNormalBaseCoverage=true};var all=new List<BuildingInstance>();var assets=new List<ScriptableObject>();
            T Asset<T>()where T:ScriptableObject{var a=ScriptableObject.CreateInstance<T>();assets.Add(a);return a;}
            var baseDef=Asset<BuildingDefinition>();var identity=Asset<BaseModuleDefinition>();identity.Role=BaseRole.Main;var area=Asset<BuildAreaModuleDefinition>();area.Shape=BuildAreaShape.Square;area.Radius=1000;area.Yaw=0;var store=Asset<PowerModuleDefinition>();store.Role=PowerRole.Storage;baseDef.Modules.Add(identity);baseDef.Modules.Add(area);baseDef.Modules.Add(store);
            var deviceDef=Asset<BuildingDefinition>();var consumer=Asset<PowerModuleDefinition>();consumer.Role=PowerRole.Consumer;consumer.ExternalAttackAdapter=true;deviceDef.Modules.Add(consumer);
            try{
                for(int i=0;i<=count;i++){var b=new BuildingInstance(i,i==0?baseDef:deviceDef,default,new Vector3(i%100,0,i/100),new BuildingServices(null));b.Activate();registry.Register(b);all.Add(b);}
                var power=new BasePowerSimulation(registry,2,Quaternion.identity);for(int i=0;i<10;i++)power.Tick(1);var samples=new double[120];
                for(int run=0;run<3;run++){long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<samples.Length;i++){long start=Stopwatch.GetTimestamp();power.Tick(1);samples[i]=(Stopwatch.GetTimestamp()-start)*1000d/Stopwatch.Frequency;}long gc=GC.GetAllocatedBytesForCurrentThread()-before;Array.Sort(samples);output.AppendLine($"{count} devices run{run+1}: median {samples[60]:F4}ms p95 {samples[114]:F4}ms GC {gc}B");}
            }finally{foreach(var b in all)b.Dispose();foreach(var a in assets)UnityEngine.Object.DestroyImmediate(a);}
        }
        System.IO.File.WriteAllText("Temp/base-power-benchmark.txt",output.ToString());return output.ToString();
    }
}
