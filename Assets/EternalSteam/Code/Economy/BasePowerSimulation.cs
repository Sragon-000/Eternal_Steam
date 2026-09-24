using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    public sealed class BasePowerSimulation
    {
        readonly BaseRegistry registry;
        readonly List<PowerModule> stores=new(),devices=new();
        readonly Quaternion rotation;
        readonly float halfCell;
        int revision=-1;[Saved(0,1)] double elapsed;
        public BasePowerSimulation(BaseRegistry registry,float cellSize,Quaternion rotation){this.registry=registry;halfCell=cellSize*.5f;this.rotation=rotation;}
        bool Covers(PowerModule store,PowerModule device)=>store.Owner.Active&&!store.Owner.Disposed&&store.Owner.Module<IBuildArea>() is IBuildArea area&&area.Contains(device.Owner.Position,(Vector2)device.Owner.Footprint*halfCell,rotation);
        public void Refresh()
        {
            registry.Refresh();if(revision==registry.Revision)return;revision=registry.Revision;
            stores.Clear();devices.Clear();
            foreach(var b in registry.Buildings)if(b.Active&&!b.Disposed&&b.Module<PowerModule>() is PowerModule power){if(power.Role==PowerRole.Storage)stores.Add(power);else devices.Add(power);}
            foreach(var d in devices){
                if(d.Supply!=null&&stores.Contains(d.Supply)&&Covers(d.Supply,d))continue;
                d.Supplied=false;d.Supply=null;float best=float.PositiveInfinity;string bestId=null;
                foreach(var store in stores){if(!Covers(store,d))continue;var delta=store.Owner.Position-d.Owner.Position;delta.y=0;float distance=delta.sqrMagnitude;string id=store.Owner.Module<IBaseIdentity>().BaseId;
                    if(distance<best||(distance==best&&string.CompareOrdinal(id,bestId)<0)){d.Supply=store;best=distance;bestId=id;}}
            }
            Rates();
        }
        public void RecalculateRates()=>Rates();
        void Rates()
        {
            foreach(var s in stores){s.Production=0;s.Requested=0;}
            foreach(var d in devices)if(d.Supply!=null&&d.Owner.Operational){if(d.Role==PowerRole.Producer)d.Supply.Production+=d.Rate;else if(d.RequestsSupply)d.Supply.Requested+=d.Rate;}
        }
        public void Tick(double seconds)
        {
            Refresh();if(stores.Count==0){elapsed=0;return;}if(!double.IsFinite(seconds)||seconds<=0)return;
            // Accumulate only the time each configuration actually existed, then settle at 1Hz.
            while(seconds>0){double part=Math.Min(seconds,1-elapsed);seconds-=part;elapsed+=part;
                foreach(var s in stores){s.PendingProduction+=s.Production*part;s.PendingDemand+=s.Requested*part;}
                if(elapsed<1-1e-9)break;elapsed=0;
                foreach(var s in stores){double available=s.Stored+s.PendingProduction;bool enough=available+1e-9>=s.PendingDemand;s.Consumed=enough?s.PendingDemand:0;s.Stored=Math.Clamp(available-s.Consumed,0,s.Capacity);s.Supplied=enough;s.PendingProduction=s.PendingDemand=0;}
                foreach(var d in devices)d.Supplied=d.Supply!=null&&d.Owner.Operational&&d.Supply.Supplied;
            }
        }
    }
}
