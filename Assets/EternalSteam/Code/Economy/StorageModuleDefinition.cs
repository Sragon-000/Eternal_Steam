using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    [CreateAssetMenu(menuName="Eternal Steam/Modules/Storage")]
    public sealed class StorageModuleDefinition : BuildingModuleDefinition
    {
        public string ResourceId="sample.energy";
        public float Capacity=100;
        public override IBuildingModule CreateRuntime()=>new Runtime(ResourceId,Capacity);
        public override void Validate(List<string> errors)
        { if(string.IsNullOrWhiteSpace(ResourceId) || !float.IsFinite(Capacity) || Capacity<=0) errors.Add("Storage requires resource ID and positive capacity."); }
        sealed class Runtime:IBuildingModule
        {
            readonly string id; readonly float capacity; IResourceBank bank; bool active;
            public Runtime(string id,float capacity) { this.id=id; this.capacity=capacity; }
            public void Initialize(BuildingInstance owner,BuildingServices services)=>bank=services.Resources??throw new InvalidOperationException("Storage requires ResourceBank.");
            public void Activate() { bank.AddCapacity(id,capacity); active=true; }
            public void Tick(float dt) { }
            public void Dispose() { if(active) { bank.AddCapacity(id,-capacity); active=false; } }
        }
    }
}
