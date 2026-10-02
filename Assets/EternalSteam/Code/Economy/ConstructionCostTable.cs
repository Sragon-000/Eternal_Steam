using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    public interface IConstructionCostPolicy
    { bool TryQuote(string buildingId,out List<ResourceCost> costs,out string reason); }
    // Explicit fallback until product costs are decided. A configured table never silently falls back.
    public sealed class VerificationFreeConstruction:IConstructionCostPolicy
    {
        public bool TryQuote(string id,out List<ResourceCost> costs,out string reason)
        {costs=new();reason=null;return true;}
    }
    [CreateAssetMenu(menuName="Eternal Steam/Economy/Construction Costs")]
    public sealed class ConstructionCostTable:ScriptableObject,IConstructionCostPolicy
    {
        public const string FoundationId="openworld.foundation";
        [Serializable] public sealed class Entry
        {public string BuildingId;public List<ResourceCost> Costs=new();}
        public List<Entry> Entries=new();
        public bool TryQuote(string id,out List<ResourceCost> costs,out string reason)
        {
            costs=null;reason="건설 비용이 설정되지 않았습니다: "+id;
            if(Entries==null)return false;
            foreach(var entry in Entries)if(entry!=null&&entry.BuildingId==id){
                if(costs!=null){costs=null;reason="건설 비용이 중복 설정되었습니다: "+id;return false;}
                if(entry.Costs==null){reason="건설 비용 목록이 없습니다: "+id;return false;}
                costs=entry.Costs;
            }
            if(costs==null)return false;
            // Validate definitions independently of stock and infinite-resource test mode.
            if(!new ResourceBank{InfiniteResources=true}.CanPurchase(costs,out reason)){costs=null;return false;}
            reason=null;return true;
        }
    }
}
