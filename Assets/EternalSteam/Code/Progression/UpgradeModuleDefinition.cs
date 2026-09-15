using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    [CreateAssetMenu(menuName="Eternal Steam/Modules/Upgrade")]
    public sealed class UpgradeModuleDefinition : BuildingModuleDefinition
    {
        public int MaximumLevel=5;
        public bool RequireNexus=true;
        public float DamagePerLevel=0.2f;
        public float HealthPerLevel=0.2f;
        public override IBuildingModule CreateRuntime() => new UpgradeModule(MaximumLevel,RequireNexus,DamagePerLevel,HealthPerLevel);
        public override void Validate(List<string> errors)
        { if(MaximumLevel<1 || !float.IsFinite(DamagePerLevel) || DamagePerLevel<0 || !float.IsFinite(HealthPerLevel) || HealthPerLevel<0) errors.Add("Invalid upgrade settings."); }
    }
    public sealed class UpgradeModule : IBuildingModule, IUpgradeControl, IDamageModifier
    {
        readonly int maximum; readonly bool requireNexus; readonly float damage,health;
        BuildingInstance owner; ILevelLimit nexus;
        public int Level { get; private set; }=1;
        public int MaximumLevel => maximum;
        public float DamageMultiplier => 1+damage*(Level-1);
        public UpgradeModule(int maximum,bool requireNexus,float damage,float health)
        { this.maximum=maximum; this.requireNexus=requireNexus; this.damage=damage; this.health=health; }
        public void Initialize(BuildingInstance owner,BuildingServices services)
        { this.owner=owner; nexus=services.LevelLimit; if(requireNexus && nexus==null) throw new InvalidOperationException("Limited upgrades require ILevelLimit."); }
        public bool TryUpgrade(out string reason)
        {
            reason=null;
            if(!owner.Active || owner.Disposed) { reason="활성 건물이 아닙니다."; return false; }
            if(Level>=maximum) { reason="최대 레벨입니다."; return false; }
            if(requireNexus && !nexus.IsExempt(owner) && Level+1>nexus.LevelCap) { reason="넥서스를 먼저 강화하세요."; return false; }
            Level++; owner.Module<IHealthScaling>()?.SetMaximumMultiplier(1+health*(Level-1)); return true;
        }
        public void Activate() { }
        public void Tick(float dt) { }
        public void Dispose() { }
    }
}
