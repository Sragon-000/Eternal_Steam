using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    [Serializable] public sealed class ResourceCost {public string ResourceId;public double Amount;}
    public interface IUpgradeCostPolicy {bool TryQuote(string buildingId,int targetLevel,out List<ResourceCost> costs,out string reason);}
    public sealed class VerificationFreeUpgrade:IUpgradeCostPolicy
    {public bool TryQuote(string id,int level,out List<ResourceCost> costs,out string reason){costs=new();reason=null;return true;}}
    [CreateAssetMenu(menuName="Eternal Steam/Economy/Upgrade Costs")]
    public sealed class UpgradeCostTable:ScriptableObject,IUpgradeCostPolicy
    {
        [Serializable] public sealed class Entry {public string BuildingId;public int TargetLevel;public List<ResourceCost> Costs=new();}
        public List<Entry> Entries=new();
        public bool TryQuote(string id,int level,out List<ResourceCost> costs,out string reason){costs=null;bool found=false;reason="강화 비용이 설정되지 않았습니다.";foreach(var entry in Entries)if(entry.BuildingId==id&&entry.TargetLevel==level){if(found){costs=null;reason="강화 비용이 중복 설정되었습니다.";return false;}found=true;costs=entry.Costs;}if(costs==null)return false;reason=null;return true;}
    }
    public sealed class UpgradePurchase
    {
        readonly ResourceBank bank;readonly IUpgradeCostPolicy policy;
        public UpgradePurchase(ResourceBank bank,IUpgradeCostPolicy policy){this.bank=bank;this.policy=policy;}
        public bool TryUpgrade(BuildingInstance building,out string reason)
        {
            reason="강화할 건물을 선택하세요.";var upgrade=building?.Module<IUpgradeControl>();if(upgrade==null)return false;
            if(!building.Active||building.Disposed||upgrade.Level>=upgrade.MaximumLevel){reason="강화할 수 없습니다.";return false;}
            if(!policy.TryQuote(building.DefinitionId,upgrade.Level+1,out var costs,out reason))return false;
            string failure=null;bool result=bank.TryPurchase(costs,()=>upgrade.TryUpgrade(out failure),out reason);if(!result&&failure!=null)reason=failure;return result;
        }
    }
}
