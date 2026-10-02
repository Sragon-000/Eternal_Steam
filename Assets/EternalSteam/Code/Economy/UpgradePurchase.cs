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
        readonly Func<BuildingInstance,ResourceBank> bankFor;readonly IUpgradeCostPolicy policy;
        public bool IsVerificationFree=>policy is VerificationFreeUpgrade;
        public UpgradePurchase(ResourceBank bank,IUpgradeCostPolicy policy):this(_=>bank,policy){}
        public UpgradePurchase(Func<BuildingInstance,ResourceBank> bankFor,IUpgradeCostPolicy policy){this.bankFor=bankFor;this.policy=policy;}
        bool TryQuote(BuildingInstance building,out IUpgradeControl upgrade,out List<ResourceCost> costs,out string reason)
        {
            costs=null;reason="강화할 건물을 선택하세요.";upgrade=building?.Module<IUpgradeControl>();if(upgrade==null)return false;
            if(!upgrade.CanUpgrade(out reason))return false;
            if(policy==null){reason="강화 비용이 설정되지 않았습니다.";return false;}
            return policy.TryQuote(building.DefinitionId,upgrade.Level+1,out costs,out reason);
        }
        public bool CanUpgrade(BuildingInstance building,out string reason)
        {
            // The explicit verification policy has no cost; avoid a per-frame quote allocation.
            if(IsVerificationFree){reason="강화할 건물을 선택하세요.";return building?.Module<IUpgradeControl>() is IUpgradeControl upgrade&&upgrade.CanUpgrade(out reason);}
            if(!TryQuote(building,out _,out var costs,out reason))return false;
            var bank=bankFor?.Invoke(building);
            if(bank!=null)return bank.CanPurchase(costs,out reason);
            reason="강화 비용을 지불할 활성 소속 기지가 없습니다.";return false;
        }
        public bool TryUpgrade(BuildingInstance building,out string reason)
        {
            if(!TryQuote(building,out var upgrade,out var costs,out reason))return false;
            if(IsVerificationFree)return upgrade.TryUpgrade(out reason);
            var bank=bankFor?.Invoke(building);
            if(bank==null){reason="강화 비용을 지불할 활성 소속 기지가 없습니다.";return false;}
            string failure=null;bool result=bank.TryPurchase(costs,()=>upgrade.TryUpgrade(out failure),out reason);if(!result&&failure!=null)reason=failure;return result;
        }
    }
}
