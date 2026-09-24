using System;
using System.Collections.Generic;
namespace EternalSteam
{
    public sealed class ResourceBank : IResourceBank
    {
        readonly Dictionary<string,double> amounts=new(),capacities=new();
        [Serializable] public sealed class Stock {public string id;public double amount,capacity;}
        public List<Stock> Capture(){var result=new List<Stock>();foreach(var pair in capacities)result.Add(new Stock{id=pair.Key,capacity=pair.Value,amount=Amount(pair.Key)});result.Sort((a,b)=>string.CompareOrdinal(a.id,b.id));return result;}
        public void Restore(List<Stock> stocks){var ids=new HashSet<string>();if(stocks==null)throw new ArgumentException("Missing stocks");foreach(var s in stocks)if(s==null||string.IsNullOrWhiteSpace(s.id)||!ids.Add(s.id)||!double.IsFinite(s.amount)||s.amount<0||!double.IsFinite(s.capacity)||s.capacity<0)throw new ArgumentException("Invalid resource stock");amounts.Clear();capacities.Clear();foreach(var s in stocks){amounts.Add(s.id,s.amount);capacities.Add(s.id,s.capacity);}}
        public bool TryPurchase(IReadOnlyList<ResourceCost> costs,Func<bool> commit,out string reason){
            reason=null;var totals=new Dictionary<string,double>();if(costs==null){reason="비용이 설정되지 않았습니다.";return false;}
            foreach(var cost in costs){if(cost==null||string.IsNullOrWhiteSpace(cost.ResourceId)||!double.IsFinite(cost.Amount)||cost.Amount<0){reason="잘못된 비용 설정입니다.";return false;}totals.TryGetValue(cost.ResourceId,out var old);double total=old+cost.Amount;if(!double.IsFinite(total)){reason="비용 범위 오류";return false;}totals[cost.ResourceId]=total;}
            foreach(var pair in totals)if(Amount(pair.Key)<pair.Value){reason="자원이 부족합니다: "+pair.Key;return false;}
            if(!commit()){reason="강화 조건을 만족하지 않습니다.";return false;}
            foreach(var pair in totals)amounts[pair.Key]=Amount(pair.Key)-pair.Value;return true;
        }
        public double Amount(string id) => amounts.TryGetValue(id,out var value)?value:0;
        public double Capacity(string id) => capacities.TryGetValue(id,out var value)?value:0;
        public void AddCapacity(string id,double value)
        {
            if(string.IsNullOrWhiteSpace(id) || !double.IsFinite(value)) throw new ArgumentException("Invalid resource capacity.");
            capacities[id]=Math.Max(0,Capacity(id)+value);
            // Removing storage preserves stock; no further production until below capacity.
        }
        public bool Exchange(string input,double cost,string output,double gain)
        {
            if(string.IsNullOrWhiteSpace(output) || !double.IsFinite(cost) || cost<0 || !double.IsFinite(gain) || gain<=0) return false;
            if(cost>0 && (string.IsNullOrWhiteSpace(input) || Amount(input)<cost)) return false;
            double after=Amount(output)+gain-(input==output?cost:0);
            if(after>Capacity(output)) return false;
            if(cost>0) amounts[input]=Amount(input)-cost;
            amounts[output]=Amount(output)+gain; return true;
        }
    }
}
