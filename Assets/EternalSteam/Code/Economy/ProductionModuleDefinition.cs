using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    [CreateAssetMenu(menuName="Eternal Steam/Modules/Production or Conversion")]
    public sealed class ProductionModuleDefinition:BuildingModuleDefinition
    {
        public string InputId;
        public float InputAmount;
        public string OutputId="sample.energy";
        public float OutputAmount=5;
        public float Interval=1;
        public override IBuildingModule CreateRuntime()=>new Runtime(InputId,InputAmount,OutputId,OutputAmount,Interval);
        public override void Validate(List<string> errors)
        {
            if(!float.IsFinite(InputAmount) || InputAmount<0 || (InputAmount>0 && string.IsNullOrWhiteSpace(InputId)) || string.IsNullOrWhiteSpace(OutputId)
                || !float.IsFinite(OutputAmount) || OutputAmount<=0 || !float.IsFinite(Interval) || Interval<=0) errors.Add("Invalid production recipe.");
        }
        sealed class Runtime:IBuildingModule,IOperationalModule
        {
            readonly string input,output; readonly float cost,gain,interval; [Saved(0)] float elapsed; IResourceBank bank;
            public Runtime(string input,float cost,string output,float gain,float interval) { this.input=input; this.cost=cost; this.output=output; this.gain=gain; this.interval=interval; }
            public void Initialize(BuildingInstance owner,BuildingServices services)=>bank=services.Resources??throw new InvalidOperationException("Production requires ResourceBank.");
            public void Activate() { }
            public void Tick(float dt)
            {
                elapsed+=dt;
                while(elapsed>=interval)
                { if(!bank.Exchange(input,cost,output,gain)) { elapsed=interval; break; } elapsed-=interval; }
            }
            public void Dispose() { elapsed=0; }
        }
    }
}
