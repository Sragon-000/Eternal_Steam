// Managed command checks with synthetic building handles; not Unity lifecycle/UI or asset validation.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using EternalSteam;
public static class UpgradeLevelChecks
{
    sealed class Limit:ILevelLimit {public int LevelCap{get;set;}=1;public bool Exempt;public bool IsExempt(BuildingInstance b)=>Exempt;}
    sealed class Price:IUpgradeCostPolicy {public bool TryQuote(string id,int level,out List<ResourceCost> costs,out string reason){costs=new(){new ResourceCost{ResourceId="ore",Amount=10}};reason=null;return true;}}
    static void Set(BuildingInstance b,string name,object value)=>typeof(BuildingInstance).GetField("<"+name+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(b,value);
    static BuildingInstance Building(PerformanceUpgrade u,ILevelLimit limit)
    {
        var b=(BuildingInstance)FormatterServices.GetUninitializedObject(typeof(BuildingInstance));Set(b,"Active",true);
        typeof(BuildingInstance).GetField("modules",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(b,new List<IBuildingModule>{u});
        u.Initialize(b,new BuildingServices(null,levelLimit:limit));return b;
    }
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static int Main()
    {
        var limit=new Limit();var u=new PerformanceUpgrade(3);var b=Building(u,limit);var bank=new ResourceBank();bank.AddCapacity("ore",100);bank.Deposit("ore",30);var paid=new UpgradePurchase(bank,new Price());
        Check(!paid.CanUpgrade(b,out _)&&!paid.TryUpgrade(b,out _)&&u.Level==1&&bank.Amount("ore")==30,"main1 blocks query and paid command without debit");
        limit.LevelCap=2;Check(paid.CanUpgrade(b,out _),"main2 unlocks query");limit.LevelCap=1;
        Check(!paid.TryUpgrade(b,out _)&&u.Level==1&&bank.Amount("ore")==30,"command rechecks changed main level");
        limit.LevelCap=2;Check(paid.TryUpgrade(b,out _)&&u.Level==2&&bank.Amount("ore")==20,"one upgrade debits exactly once");
        Check(!paid.TryUpgrade(b,out _)&&u.Level==2&&bank.Amount("ore")==20,"second upgrade cannot exceed cap");
        limit.LevelCap=1;limit.Exempt=true;Check(paid.TryUpgrade(b,out _)&&u.Level==3,"explicit exemption allows own progression");
        Check(!paid.TryUpgrade(b,out _)&&bank.Amount("ore")==10,"exemption does not bypass own maximum");
        var freeModule=new PerformanceUpgrade(10);var freeBuilding=Building(freeModule,new Limit());var free=new UpgradePurchase(bank,new VerificationFreeUpgrade());
        Check(!free.CanUpgrade(freeBuilding,out _)&&!free.TryUpgrade(freeBuilding,out _)&&freeModule.Level==1,"verification free policy also honors cap");
        var mobile=new PerformanceUpgrade(10);var mobileBuilding=Building(mobile,null);Check(mobile.TryUpgrade(out _),"unscoped module remains compatible with train controller policy");
        Set(mobileBuilding,"Active",false);Check(!mobile.TryUpgrade(out _),"inactive blocks upgrade");Set(mobileBuilding,"Active",true);Set(mobileBuilding,"Disposed",true);Check(!mobile.TryUpgrade(out _),"disposed blocks upgrade");
        string owner=Guid.NewGuid().ToString("N"),other=Guid.NewGuid().ToString("N");
        var registry=new BaseInventoryRegistry(bank,id=>id==owner);
        registry.Ensure(owner).Deposit("ore",15);registry.Ensure(other).Deposit("ore",25);
        var ownedModule=new PerformanceUpgrade(3);var ownedBuilding=Building(ownedModule,new Limit{LevelCap=2});Set(ownedBuilding,"OwnerBaseId",owner);
        var ownerPurchase=new UpgradePurchase(building=>registry.Available(building.OwnerBaseId),new Price());
        Check(ownerPurchase.CanUpgrade(ownedBuilding,out _)&&ownerPurchase.TryUpgrade(ownedBuilding,out _)&&ownedModule.Level==2&&registry.Ensure(owner).Amount("ore")==5&&registry.Ensure(other).Amount("ore")==25,"paid upgrade charges only installed owner's ledger");
        var lostModule=new PerformanceUpgrade(3);var lostBuilding=Building(lostModule,new Limit{LevelCap=2});Set(lostBuilding,"OwnerBaseId",other);
        Check(!ownerPurchase.CanUpgrade(lostBuilding,out _)&&!ownerPurchase.TryUpgrade(lostBuilding,out _)&&lostModule.Level==1&&registry.Ensure(other).Amount("ore")==25,"lost owner blocks paid upgrade without touching preserved stock");
        return 0;
    }
}
