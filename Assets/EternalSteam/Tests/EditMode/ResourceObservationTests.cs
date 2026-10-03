using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
namespace EternalSteam.Tests
{
 [Category("EconomyObservation")]
 public sealed class ResourceObservationTests
 {
  static ResourceBank Bank(){var b=new ResourceBank();b.AddCapacity("iron",100);b.AddCapacity("coal",100);b.Deposit("iron",20);return b;}
  [Test] public void ProductionAndConsumptionRemainVisibleWhenNetStockDoesNotChange()
  {
   var b=Bank();var events=new List<ResourceBank.Change>();b.Changed+=events.Add;
   Assert.That(b.Exchange("iron",4,"iron",4),Is.True);
   Assert.That(b.Amount("iron"),Is.EqualTo(20));
   Assert.That(events.Select(e=>e.Operation),Is.EqualTo(new[]{"production-input","production-output"}));
   Assert.That(events.Select(e=>e.After-e.Before),Is.EqualTo(new[]{-4d,4d}));
  }
  [Test] public void FailedUpgradeRecordsDebitAndRollbackWithoutLosingStockOrCapacity()
  {
   var b=Bank();var events=new List<ResourceBank.Change>();b.Changed+=events.Add;
   Assert.That(b.TryPurchase(new[]{new ResourceCost{ResourceId="iron",Amount=7}},()=>{b.AddCapacity("iron",-90);return false;},out _),Is.False);
   Assert.That(b.Amount("iron"),Is.EqualTo(20));Assert.That(b.Capacity("iron"),Is.EqualTo(100));
   Assert.That(events.Sum(e=>e.After-e.Before),Is.Zero);Assert.That(events.Sum(e=>e.CapacityAfter-e.CapacityBefore),Is.Zero);
   Assert.That(events.Last().Operation,Is.EqualTo("purchase-rollback"));
  }
  [Test] public void MultiBankConstructionRollbackRemainsVisibleAsRestore()
  {
   var a=Bank();var b=Bank();var events=new List<ResourceBank.Change>();a.Changed+=events.Add;b.Changed+=events.Add;
   Assert.That(ConstructionPurchase.TryCommit(new[]{new ConstructionPurchase.Payment{Bank=a,Resource="iron",Amount=7},new ConstructionPurchase.Payment{Bank=b,Resource="iron",Amount=3}},()=>false,out _),Is.False);
   Assert.That(events.Count(e=>e.Operation=="restore"),Is.EqualTo(2));Assert.That(events.Sum(e=>e.After-e.Before),Is.Zero);
  }
  [Test] public void BrokenObserverDoesNotAbortProductionOrHideEventsFromOtherObservers()
  {
   var b=Bank();var events=new List<ResourceBank.Change>();b.Changed+=_=>throw new Exception("recorder disk failure");b.Changed+=events.Add;
   Assert.That(b.Exchange("iron",2,"coal",3),Is.True);Assert.That(b.Amount("iron"),Is.EqualTo(18));Assert.That(b.Amount("coal"),Is.EqualTo(3));
   Assert.That(b.ObservationErrors,Is.EqualTo(2));Assert.That(events.Count,Is.EqualTo(2));
  }
  [Test] public void ClampedDepositAndVirtualWithdrawalDistinguishActualStockChanges()
  {
   var b=Bank();var events=new List<ResourceBank.Change>();b.Changed+=events.Add;
   Assert.That(b.Deposit("iron",1000),Is.EqualTo(80));Assert.That(events[0].Requested,Is.EqualTo(1000));
   b.InfiniteResources=true;Assert.That(b.Withdraw("iron",500),Is.EqualTo(500));
   Assert.That(events[1].Operation,Is.EqualTo("virtual-withdraw"));Assert.That(events[1].After-events[1].Before,Is.Zero);
  }
  [Test] public void BuildingMembershipObserverRunsAfterRegistrationAndCannotBreakRemoval()
  {
   var definition=UnityEngine.ScriptableObject.CreateInstance<BuildingDefinition>();definition.Id="observed";
   var building=new BuildingInstance(1,definition,UnityEngine.Vector2Int.zero,UnityEngine.Vector3.zero,new BuildingServices(null));
   var registry=new BaseRegistry(2,UnityEngine.Quaternion.identity);var events=new List<bool>();
   registry.MembershipChanged+=(_,_)=>throw new Exception("recorder failure");
   registry.MembershipChanged+=(b,added)=>{Assert.That(registry.Buildings.Contains(b),Is.EqualTo(added));events.Add(added);};
   try{registry.Register(building);registry.Remove(building);Assert.That(events,Is.EqualTo(new[]{true,false}));Assert.That(registry.ObservationErrors,Is.EqualTo(2));}
   finally{building.Dispose();UnityEngine.Object.DestroyImmediate(definition);}
  }
  [Test] public void NewBaseObserverAttachesBeforeItsFirstProductionAndCannotBreakCreation()
  {
   var registry=new BaseInventoryRegistry(Bank(),_=>true);var events=new List<ResourceBank.Change>();
   registry.Created+=(_,_)=>throw new Exception("recorder failure");registry.Created+=(_,b)=>b.Changed+=events.Add;
   var bank=registry.Ensure(Guid.NewGuid().ToString("N"));Assert.That(bank.Exchange(null,0,"iron",1),Is.True);
   Assert.That(registry.ObservationErrors,Is.EqualTo(1));Assert.That(events.Single().Operation,Is.EqualTo("production-output"));
  }
 }
}
