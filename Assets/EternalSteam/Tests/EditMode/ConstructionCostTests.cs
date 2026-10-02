using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EternalSteam.OpenWorld;
using Object=UnityEngine.Object;
namespace EternalSteam.Tests
{
    public sealed class ConstructionCostTests
    {
        readonly List<Object> assets=new();
        T Asset<T>() where T:ScriptableObject {var a=ScriptableObject.CreateInstance<T>();assets.Add(a);return a;}
        ConstructionCostTable Table(string id,params ResourceCost[] costs)
        {var t=Asset<ConstructionCostTable>();t.Entries.Add(new(){BuildingId=id,Costs=new(costs)});return t;}
        static ResourceCost Cost(string id,double amount)=>new(){ResourceId=id,Amount=amount};
        [TearDown] public void Cleanup(){foreach(var a in assets)Object.DestroyImmediate(a);assets.Clear();}
        [Test] public void ConfiguredTableNeverFallsBackForMissingOrDuplicateEntries()
        {
            var t=Table("generator",Cost("iron",4));Assert.That(t.TryQuote("absent",out _,out _),Is.False);
            t.Entries.Add(new(){BuildingId="generator",Costs=new()});Assert.That(t.TryQuote("generator",out _,out _),Is.False);
        }
        [TestCase(double.NaN)][TestCase(double.PositiveInfinity)][TestCase(-1)]
        public void TableRejectsInvalidCostsEvenWithInfiniteStock(double amount)
        {Assert.That(Table("generator",Cost("iron",amount)).TryQuote("generator",out _,out _),Is.False);}
        [Test] public void DuplicateResourceRowsAggregateBeforeAtomicPayment()
        {
            var t=Table("generator",Cost("iron",4),Cost("iron",3),Cost("copper",2));
            Assert.That(t.TryQuote("generator",out var costs,out _),Is.True);
            var bank=new ResourceBank();bank.AddCapacity("iron",10);bank.AddCapacity("copper",10);bank.Deposit("iron",7);bank.Deposit("copper",1);
            bool installed=false;Assert.That(bank.TryPurchase(costs,()=>installed=true,out _),Is.False);
            Assert.That(installed,Is.False);Assert.That(bank.Amount("iron"),Is.EqualTo(7));bank.Deposit("copper",1);
            Assert.That(bank.TryPurchase(costs,()=>true,out _),Is.True);Assert.That(bank.Amount("iron"),Is.Zero);Assert.That(bank.Amount("copper"),Is.Zero);
        }
        [Test] public void GeneralPreviewAggregatesQuantityAndDetectsLiveShortage()
        {
            var root=new GameObject("cost quote fixture");
            // Quote does not need terrain or presentation. Exercise the gameplay editing API.
            var content=new OpenWorldContent(null,root.transform,null,null,8);
            try {
                var d=Asset<BuildingDefinition>();d.Id="generator";
                content.ConstructionCosts=Table(d.Id,Cost("iron",4),Cost("iron",3),Cost("copper",2));
                content.Resources.AddCapacity("iron",100);content.Resources.AddCapacity("copper",100);content.Resources.Deposit("iron",21);content.Resources.Deposit("copper",6);
                using var edit=new WorldEditSession(null,null,content);
                var q=edit.Quote(d,3);Assert.That(q.Affordable,Is.True);Assert.That(q.Iron,Is.EqualTo(21));Assert.That(q.Lines.Count,Is.EqualTo(2));Assert.That(q.BaseName,Is.EqualTo("선택 기지 없음"));
                content.Resources.Withdraw("copper",1);q=edit.Quote(d,3);Assert.That(q.Affordable,Is.False);Assert.That(q.Lines[0].Shortage,Is.EqualTo(1));
                content.InfiniteResources=true;Assert.That(edit.Quote(d,3).Affordable,Is.True);content.InfiniteResources=false;Assert.That(edit.Quote(d,3).Affordable,Is.False);
                content.ConstructionCosts=new VerificationFreeConstruction();q=edit.Quote(d);Assert.That(q.Affordable,Is.True);Assert.That(q.VerificationFreeItems,Is.EqualTo(1));
                Assert.That(edit.Quote(d,513).Affordable,Is.False);
            } finally {content.GroundPlacement.Dispose();content.GroundWorld.Dispose();Object.DestroyImmediate(root);}
        }
        [Test] public void ConstructionExceptionRestoresAllPayersStocksCapacityAndMode()
        {
            var a=new ResourceBank();var b=new ResourceBank();a.AddCapacity("iron",100);b.AddCapacity("copper",100);a.Deposit("iron",20);b.Deposit("copper",10);
            var payments=new[]{new ConstructionPurchase.Payment{Bank=a,Resource="iron",Amount=7},new ConstructionPurchase.Payment{Bank=b,Resource="copper",Amount=4}};
            Assert.Throws<InvalidOperationException>(()=>ConstructionPurchase.TryCommit(payments,()=>{a.AddCapacity("iron",-90);b.InfiniteResources=true;throw new InvalidOperationException();},out _));
            Assert.That(a.Amount("iron"),Is.EqualTo(20));Assert.That(a.Capacity("iron"),Is.EqualTo(100));Assert.That(b.Amount("copper"),Is.EqualTo(10));Assert.That(b.InfiniteResources,Is.False);
        }
    }
}
