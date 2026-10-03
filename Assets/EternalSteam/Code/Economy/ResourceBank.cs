using System;
using System.Collections.Generic;
namespace EternalSteam
{
    public sealed class ResourceBank : IResourceBank
    {
        public readonly struct Change
        {
            public readonly string Operation, Resource;
            public readonly double Before, After, CapacityBefore, CapacityAfter, Requested;
            public Change(string operation,string resource,double before,double after,double capacityBefore,double capacityAfter,double requested)
            {Operation=operation;Resource=resource;Before=before;After=after;CapacityBefore=capacityBefore;CapacityAfter=capacityAfter;Requested=requested;}
        }
        // Optional observation. Recording failures must never change an economic transaction.
        public event Action<Change> Changed;
        public long ObservationErrors {get;private set;}
        void Observe(string operation,string id,double before,double capacityBefore,double requested=0)
        {
            var observers=Changed;if(observers==null)return;
            var change=new Change(operation,id,before,Amount(id),capacityBefore,Capacity(id),requested);
            foreach(Action<Change> observer in observers.GetInvocationList())
                try{observer(change);}catch{ObservationErrors++;}
        }
        void ObserveRestored(string operation,List<Stock> before)
        {
            if(before==null)return;
            var seen=new HashSet<string>();
            foreach(var stock in before){seen.Add(stock.id);if(stock.amount!=Amount(stock.id)||stock.capacity!=Capacity(stock.id))Observe(operation,stock.id,stock.amount,stock.capacity);}
            foreach(var stock in Capture())if(seen.Add(stock.id))Observe(operation,stock.id,0,0);
        }
        // Test mode is session-only; real stocks and saved snapshots remain finite.
        public bool InfiniteResources {get;set;}
        readonly Dictionary<string,double> amounts=new(),capacities=new();
        [Serializable] public sealed class Stock {public string id;public double amount,capacity;}
        public List<Stock> Capture(){var result=new List<Stock>();foreach(var pair in capacities)result.Add(new Stock{id=pair.Key,capacity=pair.Value,amount=Amount(pair.Key)});result.Sort((a,b)=>string.CompareOrdinal(a.id,b.id));return result;}
        public void Restore(List<Stock> stocks){var ids=new HashSet<string>();if(stocks==null)throw new ArgumentException("Missing stocks");foreach(var s in stocks)if(s==null||string.IsNullOrWhiteSpace(s.id)||!ids.Add(s.id)||!double.IsFinite(s.amount)||s.amount<0||!double.IsFinite(s.capacity)||s.capacity<0)throw new ArgumentException("Invalid resource stock");var before=Changed!=null?Capture():null;amounts.Clear();capacities.Clear();foreach(var s in stocks){amounts.Add(s.id,s.amount);capacities.Add(s.id,s.capacity);}ObserveRestored("restore",before);}
        public bool CanPurchase(IReadOnlyList<ResourceCost> costs,out string reason)=>ValidatePurchase(costs,out _,out reason);
        bool ValidatePurchase(IReadOnlyList<ResourceCost> costs,out Dictionary<string,double> totals,out string reason){
            reason=null;totals=new Dictionary<string,double>();if(costs==null){reason="비용이 설정되지 않았습니다.";return false;}
            foreach(var cost in costs){if(cost==null||string.IsNullOrWhiteSpace(cost.ResourceId)||!double.IsFinite(cost.Amount)||cost.Amount<0){reason="잘못된 비용 설정입니다.";return false;}totals.TryGetValue(cost.ResourceId,out var old);double total=old+cost.Amount;if(!double.IsFinite(total)){reason="비용 범위 오류";return false;}totals[cost.ResourceId]=total;}
            foreach(var pair in totals)if(!InfiniteResources&&Amount(pair.Key)<pair.Value){reason="자원이 부족합니다: "+pair.Key;return false;}
            return true;
        }
        public bool TryPurchase(IReadOnlyList<ResourceCost> costs,Func<bool> commit,out string reason){
            if(!ValidatePurchase(costs,out var totals,out reason))return false;
            if(commit==null){reason="강화 실행 조건이 없습니다.";return false;}
            var beforeAmounts=new Dictionary<string,double>(amounts);
            var beforeCapacities=new Dictionary<string,double>(capacities);
            bool beforeInfinite=InfiniteResources;
            if(!InfiniteResources)foreach(var pair in totals){double old=Amount(pair.Key);amounts[pair.Key]=old-pair.Value;if(pair.Value>0)Observe("purchase-debit",pair.Key,old,Capacity(pair.Key),pair.Value);}
            try{if(commit())return true;reason="강화 조건을 만족하지 않습니다.";}
            catch{RestoreBefore();throw;}
            RestoreBefore();return false;
            void RestoreBefore(){var attempted=Changed!=null?Capture():null;amounts.Clear();foreach(var pair in beforeAmounts)amounts.Add(pair.Key,pair.Value);capacities.Clear();foreach(var pair in beforeCapacities)capacities.Add(pair.Key,pair.Value);InfiniteResources=beforeInfinite;ObserveRestored("purchase-rollback",attempted);}
        }
        public double Amount(string id) => amounts.TryGetValue(id,out var value)?value:0;
        public double Capacity(string id) => capacities.TryGetValue(id,out var value)?value:0;
        public double Deposit(string id,double maximum)
        {
            if(string.IsNullOrWhiteSpace(id)||!double.IsFinite(maximum)||maximum<=0)return 0;
            double accepted=Math.Min(maximum,Math.Max(0,Capacity(id)-Amount(id)));
            if(accepted>0){double before=Amount(id);amounts[id]=before+accepted;Observe("deposit",id,before,Capacity(id),maximum);}
            return accepted;
        }
        public double Withdraw(string id,double maximum)
        {
            if(string.IsNullOrWhiteSpace(id)||!double.IsFinite(maximum)||maximum<=0)return 0;
            if(InfiniteResources){Observe("virtual-withdraw",id,Amount(id),Capacity(id),maximum);return maximum;}
            double taken=Math.Min(maximum,Math.Max(0,Amount(id)));
            if(taken>0){double before=Amount(id);amounts[id]=before-taken;Observe("withdraw",id,before,Capacity(id),maximum);}
            return taken;
        }
        public void AddCapacity(string id,double value)
        {
            if(string.IsNullOrWhiteSpace(id) || !double.IsFinite(value)) throw new ArgumentException("Invalid resource capacity.");
            double oldCapacity=Capacity(id);capacities[id]=Math.Max(0,oldCapacity+value);
            if(oldCapacity!=Capacity(id))Observe("capacity",id,Amount(id),oldCapacity,value);
            // Removing storage preserves stock; no further production until below capacity.
        }
        public bool Exchange(string input,double cost,string output,double gain)
        {
            if(string.IsNullOrWhiteSpace(output) || !double.IsFinite(cost) || cost<0 || !double.IsFinite(gain) || gain<=0) return false;
            if(cost>0 && (string.IsNullOrWhiteSpace(input) || (!InfiniteResources&&Amount(input)<cost))) return false;
            double after=Amount(output)+gain-(!InfiniteResources&&input==output?cost:0);
            if(after>Capacity(output)) return false;
            double inputBefore=cost>0?Amount(input):0;
            if(cost>0&&!InfiniteResources){amounts[input]=inputBefore-cost;Observe("production-input",input,inputBefore,Capacity(input),cost);}
            double outputBefore=Amount(output);amounts[output]=outputBefore+gain;
            Observe("production-output",output,outputBefore,Capacity(output),gain);return true;
        }
    }
}
