using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    [CreateAssetMenu(menuName="Eternal Steam/Modules/Performance Upgrade")]
    public sealed class PerformanceUpgradeDefinition : BuildingModuleDefinition
    {
        public int MaximumLevel=10;
        public override IBuildingModule CreateRuntime()=>new PerformanceUpgrade(MaximumLevel);
        public override void Validate(List<string> errors){if(MaximumLevel<1||MaximumLevel>10)errors.Add("Performance levels must be 1–10.");}
    }
    public sealed class PerformanceUpgrade : IBuildingModule,IUpgradeControl,IPerformanceScaling
    {
        BuildingInstance owner;
        ILevelLimit levelLimit;
        [Saved(1,10)] public int Level {get;private set;}=1;
        public int MaximumLevel {get;}
        public PerformanceUpgrade(int maximum){MaximumLevel=maximum;}
        public float Increase(float value)=>value*(1+.1f*(Level-1));
        public float Decrease(float value)=>value*Mathf.Max(.1f,1-.1f*(Level-1));
        public int Count(int value)=>value+Mathf.FloorToInt(value*(Level-1)/10f+.00001f);
        public void Initialize(BuildingInstance owner,BuildingServices services){this.owner=owner;levelLimit=services.LevelLimit;}
        public bool CanUpgrade(out string reason)
        {
            reason=null;
            if(owner==null||!owner.Active||owner.Disposed){reason="활성 건물이 아닙니다.";return false;}
            if(Level>=MaximumLevel){reason="최대 레벨입니다.";return false;}
            if(levelLimit!=null&&!levelLimit.IsExempt(owner)&&Level+1>levelLimit.LevelCap){reason="메인 기지를 먼저 강화하세요.";return false;}
            return true;
        }
        public bool TryUpgrade(out string reason){if(!CanUpgrade(out reason))return false;Level++;return true;}
        public void Activate(){} public void Tick(float dt){} public void Dispose(){}
    }
}
