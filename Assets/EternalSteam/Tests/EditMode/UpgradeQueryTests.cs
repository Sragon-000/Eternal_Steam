using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace EternalSteam.Tests
{
    public sealed class UpgradeQueryTests
    {
        readonly List<Object> assets=new();
        readonly List<BuildingInstance> buildings=new();
        sealed class LevelLimit:ILevelLimit
        {public int LevelCap {get;set;}=1;public bool IsExempt(BuildingInstance b)=>false;}
        T Asset<T>() where T:ScriptableObject
        {var asset=ScriptableObject.CreateInstance<T>();assets.Add(asset);return asset;}
        BuildingInstance Create(BuildingModuleDefinition module,BuildingServices services=null,bool active=true)
        {
            var definition=Asset<BuildingDefinition>();definition.Id="query.fixture";definition.DisplayName="Query fixture";
            definition.Modules.Add(module);definition.Modules.Add(Asset<HealthModuleDefinition>());
            var b=new BuildingInstance(buildings.Count+1,definition,Vector2Int.zero,Vector3.zero,services??new BuildingServices(null));
            buildings.Add(b);if(active)b.Activate();return b;
        }
        static ResourceBank Bank(double amount)
        {var bank=new ResourceBank();bank.AddCapacity("ore",100);if(amount>0)bank.Exchange(null,0,"ore",amount);return bank;}
        UpgradeCostTable Price(params double[] amounts)
        {
            var table=Asset<UpgradeCostTable>();var entry=new UpgradeCostTable.Entry{BuildingId="query.fixture",TargetLevel=2};
            foreach(var amount in amounts)entry.Costs.Add(new ResourceCost{ResourceId="ore",Amount=amount});
            table.Entries.Add(entry);return table;
        }
        [TearDown] public void Cleanup()
        {foreach(var b in buildings)b.Dispose();buildings.Clear();foreach(var a in assets)Object.DestroyImmediate(a);assets.Clear();}

        [Test] public void QueryDoesNotUpgradeOrScaleHealthAndCommandRechecksLevelLimit()
        {
            var limit=new LevelLimit();var b=Create(Asset<UpgradeModuleDefinition>(),new BuildingServices(null,levelLimit:limit));
            var upgrade=b.Module<IUpgradeControl>();var health=b.Module<HealthModule>();health.ApplyDamage(25);
            Assert.That(upgrade.CanUpgrade(out var reason),Is.False);Assert.That(reason,Does.Contain("메인 기지"));
            limit.LevelCap=2;for(int i=0;i<100;i++)Assert.That(upgrade.CanUpgrade(out _),Is.True);
            Assert.That(upgrade.Level,Is.EqualTo(1));Assert.That(health.Current,Is.EqualTo(75));Assert.That(health.Maximum,Is.EqualTo(100));
            limit.LevelCap=1;Assert.That(upgrade.TryUpgrade(out _),Is.False);
            limit.LevelCap=2;Assert.That(upgrade.TryUpgrade(out _),Is.True);Assert.That(upgrade.Level,Is.EqualTo(2));
            Assert.That(health.Current,Is.EqualTo(90).Within(.001));Assert.That(health.Maximum,Is.EqualTo(120).Within(.001));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void AllUpgradeKindsRejectInactiveDestroyedAndMaximumWithoutMutation(int kind)
        {
            var campaign=new CampaignProgression();BuildingModuleDefinition definition;
            if(kind==0){var d=Asset<UpgradeModuleDefinition>();d.RequireNexus=false;d.MaximumLevel=2;definition=d;}
            else if(kind==1){var d=Asset<PerformanceUpgradeDefinition>();d.MaximumLevel=2;definition=d;}
            else definition=Asset<MainBaseUpgradeDefinition>();
            var b=Create(definition,new BuildingServices(null,campaign:campaign),false);var u=b.Module<IUpgradeControl>();
            Assert.That(u.CanUpgrade(out _),Is.False);Assert.That(u.TryUpgrade(out _),Is.False);b.Activate();
            for(int i=0;i<100;i++)Assert.That(u.CanUpgrade(out _),Is.True);
            Assert.That(u.Level,Is.EqualTo(1));Assert.That(campaign.MainLevel,Is.EqualTo(1));
            while(u.Level<u.MaximumLevel)Assert.That(u.TryUpgrade(out _),Is.True);
            int level=u.Level;Assert.That(u.CanUpgrade(out var reason),Is.False);Assert.That(reason,Does.Contain("최대 레벨"));
            Assert.That(u.TryUpgrade(out _),Is.False);Assert.That(u.Level,Is.EqualTo(level));
            var other=Create(definition,new BuildingServices(null,campaign:new CampaignProgression()));other.Destroy();
            Assert.That(other.Module<IUpgradeControl>().CanUpgrade(out reason),Is.False);Assert.That(reason,Does.Contain("활성"));
            Assert.That(other.Module<IUpgradeControl>().TryUpgrade(out _),Is.False);
        }
        [Test] public void PaidQueryAggregatesDuplicateCostsAndCommitDebitsExactlyOnce()
        {
            var b=Create(Asset<PerformanceUpgradeDefinition>());var bank=Bank(10);var purchase=new UpgradePurchase(bank,Price(4,6));
            for(int i=0;i<100;i++)Assert.That(purchase.CanUpgrade(b,out _),Is.True);
            Assert.That(b.Module<IUpgradeControl>().Level,Is.EqualTo(1));Assert.That(bank.Amount("ore"),Is.EqualTo(10));
            Assert.That(purchase.TryUpgrade(b,out _),Is.True);Assert.That(bank.Amount("ore"),Is.Zero);
            Assert.That(b.Module<IUpgradeControl>().Level,Is.EqualTo(2));Assert.That(purchase.IsVerificationFree,Is.False);
        }
        [Test] public void ClickRechecksFundsAndChangedCostPolicy()
        {
            var b=Create(Asset<PerformanceUpgradeDefinition>());var bank=Bank(10);var table=Price(4,6);var purchase=new UpgradePurchase(bank,table);
            Assert.That(purchase.CanUpgrade(b,out _),Is.True);
            bank.TryPurchase(new[]{new ResourceCost{ResourceId="ore",Amount=1}},()=>true,out _);
            Assert.That(purchase.TryUpgrade(b,out var reason),Is.False);Assert.That(reason,Does.Contain("부족"));
            Assert.That(bank.Amount("ore"),Is.EqualTo(9));Assert.That(b.Module<IUpgradeControl>().Level,Is.EqualTo(1));
            table.Entries.Clear();Assert.That(purchase.CanUpgrade(b,out reason),Is.False);Assert.That(reason,Does.Contain("설정되지"));
        }
        [Test] public void InvalidOrMissingCostsAreNotVerificationFree()
        {
            var b=Create(Asset<PerformanceUpgradeDefinition>());var bank=Bank(10);
            var unset=new UpgradePurchase(bank,null);Assert.That(unset.CanUpgrade(b,out _),Is.False);Assert.That(unset.TryUpgrade(b,out _),Is.False);
            var table=Price(double.NaN);var purchase=new UpgradePurchase(bank,table);
            Assert.That(purchase.CanUpgrade(b,out _),Is.False);Assert.That(purchase.TryUpgrade(b,out _),Is.False);
            table.Entries[0].Costs[0].Amount=1;table.Entries.Add(table.Entries[0]);
            Assert.That(purchase.CanUpgrade(b,out var reason),Is.False);Assert.That(reason,Does.Contain("중복"));
            Assert.That(purchase.TryUpgrade(b,out _),Is.False);Assert.That(purchase.IsVerificationFree,Is.False);
            Assert.That(bank.Amount("ore"),Is.EqualTo(10));Assert.That(b.Module<IUpgradeControl>().Level,Is.EqualTo(1));
        }
        [Test] public void ExplicitFreePolicyStillChecksBuildingAndLevelConditions()
        {
            var limit=new LevelLimit();var b=Create(Asset<UpgradeModuleDefinition>(),new BuildingServices(null,levelLimit:limit));
            var purchase=new UpgradePurchase(Bank(0),new VerificationFreeUpgrade());Assert.That(purchase.IsVerificationFree,Is.True);
            Assert.That(purchase.CanUpgrade(null,out _),Is.False);Assert.That(purchase.TryUpgrade(null,out _),Is.False);
            Assert.That(purchase.CanUpgrade(b,out _),Is.False);limit.LevelCap=2;
            Assert.That(purchase.CanUpgrade(b,out _),Is.True);Assert.That(purchase.TryUpgrade(b,out _),Is.True);
            Assert.That(b.Module<IUpgradeControl>().Level,Is.EqualTo(2));
        }
        [Test] public void BankQuoteAndRejectedCommitNeverDebit()
        {
            var bank=Bank(10);var costs=new[]{new ResourceCost{ResourceId="ore",Amount=3},new ResourceCost{ResourceId="ore",Amount=8}};
            Assert.That(bank.CanPurchase(costs,out _),Is.False);int commits=0;
            Assert.That(bank.TryPurchase(costs,()=>{commits++;return true;},out _),Is.False);Assert.That(commits,Is.Zero);
            costs[1].Amount=7;Assert.That(bank.CanPurchase(costs,out _),Is.True);
            Assert.That(bank.TryPurchase(costs,()=>false,out _),Is.False);Assert.That(bank.Amount("ore"),Is.EqualTo(10));
            Assert.That(bank.CanPurchase(null,out _),Is.False);Assert.That(bank.CanPurchase(System.Array.Empty<ResourceCost>(),out _),Is.True);
            costs[0].Amount=double.MaxValue;costs[1].Amount=double.MaxValue;Assert.That(bank.CanPurchase(costs,out _),Is.False);
        }
    }
}
