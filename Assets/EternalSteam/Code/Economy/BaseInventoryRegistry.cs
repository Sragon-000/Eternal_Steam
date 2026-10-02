using System;
using System.Collections.Generic;
using System.Linq;
namespace EternalSteam
{
    /// <summary>Independent persistent ledgers. Removing a base suspends access, never merges stock.</summary>
    public sealed class BaseInventoryRegistry
    {
        [Serializable] public sealed class Record { public string baseId; public List<ResourceBank.Stock> stocks=new(); }
        readonly Dictionary<string,ResourceBank> banks=new(StringComparer.Ordinal);
        readonly Func<string,bool> available;
        readonly ResourceBank defaults;
        bool infiniteResources;
        public bool InfiniteResources {get=>infiniteResources;set{infiniteResources=value;foreach(var bank in banks.Values)bank.InfiniteResources=value;}}
        public BaseInventoryRegistry(ResourceBank defaults,Func<string,bool> available)
        { this.defaults=defaults??throw new ArgumentNullException(nameof(defaults));this.available=available??throw new ArgumentNullException(nameof(available)); }
        public ResourceBank Ensure(string id)
        {
            if(string.IsNullOrWhiteSpace(id))throw new ArgumentException("기지 소속이 없습니다.");
            if(!banks.TryGetValue(id,out var bank)) { bank=new ResourceBank{InfiniteResources=infiniteResources};bank.Restore(defaults.Capture().Select(s=>new ResourceBank.Stock{id=s.id,amount=0,capacity=s.capacity}).ToList());banks.Add(id,bank); }
            return bank;
        }
        public ResourceBank Available(string id)=>id!=null&&available(id)?Ensure(id):null;
        public IResourceBank Bind(BuildingInstance owner,string selectedBase)
            =>new Binding(this,owner,selectedBase);
        public List<Record> Capture()=>banks.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>new Record{baseId=p.Key,stocks=p.Value.Capture()}).ToList();
        public static void Validate(IReadOnlyList<Record> records)
        {
            if(records==null)throw new ArgumentException("기지 재고 목록이 없습니다.");var seen=new HashSet<string>();
            foreach(var record in records){if(record==null||!Guid.TryParseExact(record.baseId,"N",out _)||!seen.Add(record.baseId))throw new ArgumentException("기지 재고 ID 중복/오류");new ResourceBank().Restore(record.stocks);}
        }
        // Bindings resolve the ledger by stable base ID, so replacing restored records is safe.
        public void Restore(List<Record> records)
        { Validate(records);banks.Clear();foreach(var record in records)Ensure(record.baseId).Restore(record.stocks); }
        // Buildings have already activated during reload. Keep their reconstructed capacities
        // and restore only balances; old snapshots may contain stale shared capacities.
        public void RestoreAmounts(List<Record> records)
        {
            Validate(records);
            foreach(var record in records)
            {
                var bank=Ensure(record.baseId);
                var saved=record.stocks.ToDictionary(stock=>stock.id,StringComparer.Ordinal);
                var current=bank.Capture();
                var restored=current.Select(stock=>new ResourceBank.Stock{
                    id=stock.id,amount=saved.TryGetValue(stock.id,out var value)?value.amount:0,
                    capacity=stock.capacity
                }).ToList();
                foreach(var stock in record.stocks)
                    if(!current.Any(value=>value.id==stock.id))
                        restored.Add(new ResourceBank.Stock{id=stock.id,amount=stock.amount,capacity=0});
                bank.Restore(restored);
            }
        }
        sealed class Binding:IResourceBank
        {
            readonly BaseInventoryRegistry registry;readonly BuildingInstance owner;readonly string selected;
            string id;
            public Binding(BaseInventoryRegistry registry,BuildingInstance owner,string selected){this.registry=registry;this.owner=owner;this.selected=selected;}
            string Id=>id??=owner.Module<IBaseIdentity>()?.BaseId??owner.OwnerBaseId??selected;
            ResourceBank Ledger=>registry.Ensure(Id);
            bool Active=>Id!=null&&registry.available(Id);
            public double Amount(string resource)=>Active?Ledger.Amount(resource):0;
            public double Capacity(string resource)=>Active?Ledger.Capacity(resource):0;
            // Storage lifecycle can remove capacity after a base was lost; stock is preserved.
            public void AddCapacity(string resource,double value)=>Ledger.AddCapacity(resource,value);
            public bool Exchange(string input,double cost,string output,double gain)=>Active&&Ledger.Exchange(input,cost,output,gain);
        }
    }
}
