using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    public sealed class NexusState : IBaseObjective
    {
        readonly HashSet<BuildingInstance> members=new();
        public bool Defeated { get; private set; }
        public event Action Lost;
        public int LevelCap
        {
            get { int level=0; foreach(var item in members) if(item.Active && !item.Disposed) level=Mathf.Max(level,item.Module<ILevelProvider>()?.Level ?? 1); return level; }
        }
        public bool HasNexus => LevelCap>0;
        public bool IsExempt(BuildingInstance building) => building.Module<ILevelAuthority>() != null;
        public void Register(BuildingInstance owner) => members.Add(owner);
        public void Unregister(BuildingInstance owner) => members.Remove(owner);
        public void ReportDestroyed()
        { if(Defeated) return; Defeated=true; Lost?.Invoke(); }
    }
    [CreateAssetMenu(menuName="Eternal Steam/Modules/Nexus")]
    public sealed class NexusModuleDefinition : BuildingModuleDefinition
    {
        public override IBuildingModule CreateRuntime() => new NexusModule();
        public override void ValidateComposition(BuildingDefinition definition,List<string> errors)
        {
            if(!definition.Provides<IDamageReceiver>()) errors.Add("Nexus requires a damage-receiving capability.");
            if(definition.Recoverable) errors.Add("Nexus must be non-recoverable.");
        }
    }
    public sealed class NexusModule : IBuildingModule, ILevelAuthority
    {
        BuildingInstance owner; IBaseObjective state;
        public void Initialize(BuildingInstance owner,BuildingServices services)
        {
            this.owner=owner; state=services.Nexus ?? throw new InvalidOperationException("Nexus requires IBaseObjective.");
            if(owner.Module<IDamageReceiver>()==null || owner.Recoverable) throw new InvalidOperationException("Nexus requires IDamageReceiver and a non-recoverable definition.");
        }
        public void Activate() { state.Register(owner); owner.Destroying+=OnDestroyed; }
        void OnDestroyed(BuildingInstance _) => state.ReportDestroyed();
        public void Tick(float dt) { }
        public void Dispose() { if(owner==null) return; owner.Destroying-=OnDestroyed; state?.Unregister(owner); }
    }
}
