using System;
using System.Linq;
using EternalSteam.Railway;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    /// <summary>Only weapon, rotation and performance modules; never a ground building/target/power consumer.</summary>
    public sealed class MobileDefenseRuntime:IDisposable
    {
        readonly BuildingDefinition composition;
        readonly double rate;
        public TrainArmament State {get;}
        public BuildingInstance Building {get;}
        public bool Pending=>Building.Modules.OfType<IPendingExecution>().Any(p=>p.HasPendingExecution);
        public static bool Compatible(BuildingDefinition d,Vector2Int size)=>d!=null&&d.Category==BuildingCategory.Defense&&d.Footprint==size&&
            d.Modules.OfType<WeaponModuleDefinition>().Count()==1&&d.Modules.OfType<PerformanceUpgradeDefinition>().Count()==1;
        public MobileDefenseRuntime(TrainArmament state,BuildingDefinition definition,ITargetQuery targets,Func<Vector3> position,ILevelLimit levelLimit=null)
        {
            TrainArmament.Validate(state);
            if(!Compatible(definition,state.footprint)||state.definition!=definition.Id)throw new ArgumentException("방어칸 크기 또는 무기 구성 불일치");
            State=state;rate=definition.Modules.OfType<PowerModuleDefinition>().Where(p=>p.Role==PowerRole.Consumer).Sum(p=>p.Rate);
            composition=ScriptableObject.CreateInstance<BuildingDefinition>();composition.Id=definition.Id;composition.DisplayName=definition.DisplayName;composition.Footprint=definition.Footprint;
            composition.Modules=definition.Modules.Where(m=>m is WeaponModuleDefinition or TurretRotationDefinition or PerformanceUpgradeDefinition).ToList();
            try{
                Building=new BuildingInstance(-1,composition,default,default,new BuildingServices(targets,levelLimit:levelLimit),position);
                if(state.modules.Count>0){if(state.modules.Count!=Building.Modules.Count)throw new ArgumentException("기차 방어 모듈 저장 개수 오류");foreach(var module in Building.Modules)state.modules.Single(m=>m.type==module.GetType().FullName).Restore(module);}
                var upgrade=Building.Module<IUpgradeControl>();if(upgrade.Level>upgrade.MaximumLevel)throw new ArgumentException("기차 방어 강화 범위 오류");
                Building.Activate();
            }catch{Building?.Dispose();UnityEngine.Object.DestroyImmediate(composition);throw;}
        }
        public void Tick(float seconds,bool combatEnabled)
        {
            if(!float.IsFinite(seconds)||seconds<=0||!combatEnabled)return;
            // Debit only powered time. Previously fired projectiles continue after depletion.
            float powered=rate<=0?seconds:(float)Math.Min(seconds,State.battery/rate);
            if(powered>0){State.battery=Math.Max(0,State.battery-powered*rate);Building.Tick(powered);}
            float remaining=seconds-powered;if(remaining>0)foreach(var module in Building.Modules.OfType<IUnpoweredCombat>())module.TickUnpowered(remaining);
        }
        public void Capture()=>State.modules=Building.Modules.Select(SavedState.Capture).ToList();
        public void Dispose(){Building.Dispose();UnityEngine.Object.DestroyImmediate(composition);}
    }
}
