using System;
using NUnit.Framework;
using UnityEngine;
namespace EternalSteam.Tests
{
    public sealed class RailConnectionTransactionTests
    {
        sealed class Factory:IBuildingFactory
        {
            public int active;
            public BuildingInstance Stage(int id,PlacementRequest r,Vector3 p)=>new BuildingInstance(id,r.Definition,r.Cell,p,new BuildingServices(null));
            public void Activate(BuildingInstance b){active++;}
            public void Remove(BuildingInstance b){active--;}
        }
        [TestCase(false)][TestCase(true)] public void ConnectionFailureRollsBackBuildingsAndCostButKeepsReservations(bool throws)
        {
            var def=ScriptableObject.CreateInstance<BuildingDefinition>();def.Id="rail.test";def.DisplayName="Rail test";def.ViewPrefab=new GameObject("Test view");
            var f=new Factory();using var world=new BuildingWorld(new BuildGrid(new RectInt(0,0,10,10),2),f);using var session=new PlacementSession(world);
            try{
                Assert.That(session.Add(def,Vector2Int.zero,out _).Success,Is.True);Assert.That(session.Add(def,Vector2Int.right,out _).Success,Is.True);
                var bank=new ResourceBank();bank.AddCapacity("iron",10);bank.Deposit("iron",10);
                bool paid=ConstructionPurchase.TryCommit(new[]{new ConstructionPurchase.Payment{Bank=bank,Resource="iron",Amount=2}},()=>PlacementSession.ConfirmTogether(new[]{session},()=>{
                    Assert.That(world.Buildings.Count,Is.EqualTo(2));if(throws)throw new InvalidOperationException("Injected connection failure");return new PlacementResult("connection","Injected connection rejection");
                }).Success,out _);
                Assert.That(paid,Is.False);Assert.That(world.Buildings.Count,Is.Zero);Assert.That(world.Grid.OccupiedCount,Is.Zero);Assert.That(f.active,Is.Zero);Assert.That(bank.Amount("iron"),Is.EqualTo(10));Assert.That(session.Pending.Count,Is.EqualTo(2));Assert.That(world.Grid.ReservationCount,Is.EqualTo(2));
                Assert.That(ConstructionPurchase.TryCommit(new[]{new ConstructionPurchase.Payment{Bank=bank,Resource="iron",Amount=2}},()=>PlacementSession.ConfirmTogether(new[]{session},()=>PlacementResult.Ok).Success,out _),Is.True);
                Assert.That(bank.Amount("iron"),Is.EqualTo(8));Assert.That(world.Buildings.Count,Is.EqualTo(2));Assert.That(world.Grid.ReservationCount,Is.Zero);
            }finally{UnityEngine.Object.DestroyImmediate(def.ViewPrefab);UnityEngine.Object.DestroyImmediate(def);}
        }
    }
}
