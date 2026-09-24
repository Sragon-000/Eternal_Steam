using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
namespace EternalSteam.Tests
{
    public sealed class DocumentContractTests
    {
        readonly List<Object> assets=new();
        readonly List<IDisposable> owned=new();
        T Asset<T>() where T:ScriptableObject {var a=ScriptableObject.CreateInstance<T>();assets.Add(a);return a;}
        BuildingDefinition Definition()
        {var d=Asset<BuildingDefinition>();d.Id="test";d.DisplayName="Test";d.ViewPrefab=new GameObject("Test");assets.Add(d.ViewPrefab);return d;}
        sealed class Factory:IBuildingFactory
        {
            public bool FailStage,FailActivation;public int Active;
            public BuildingInstance Stage(int id,PlacementRequest r,Vector3 p)
            {if(FailStage)throw new Exception("injected stage failure");return new BuildingInstance(id,r.Definition,r.Cell,p,new BuildingServices(null));}
            public void Activate(BuildingInstance b){if(FailActivation)throw new Exception("injected activation failure");Active++;}
            public void Remove(BuildingInstance b){Active=0;}
        }
        PlacementSession Session(Factory f,out BuildingWorld world)
        {world=new BuildingWorld(new BuildGrid(new RectInt(0,0,4,4),2,default,45),f);owned.Add(world);var s=new PlacementSession(world);owned.Add(s);return s;}
        [TearDown]public void Cleanup(){for(int i=owned.Count-1;i>=0;i--)owned[i].Dispose();foreach(var a in assets)Object.DestroyImmediate(a);owned.Clear();assets.Clear();}
        [Test]public void RotatedGridRoundTripsNegativeCellsAndMulticellCenters()
        {
            var grid=new BuildGrid(new RectInt(-5,-5,10,10),2,new Vector3(3,7,-2),45);
            for(int z=-5;z<5;z++)for(int x=-5;x<5;x++){var c=new Vector2Int(x,z);Assert.That(grid.WorldToCell(grid.Center(c,Vector2Int.one)),Is.EqualTo(c));}
            var center=grid.Center(new Vector2Int(-1,2),new Vector2Int(2,3));
            Assert.That(Vector3.Distance(center,grid.Origin+Quaternion.Euler(0,45,0)*new Vector3(0,0,7)),Is.LessThan(.0001f));
            Assert.Throws<ArgumentException>(()=>new BuildGrid(new RectInt(0,0,1,1),2,default,float.NaN));
        }
        [TestCase(false)][TestCase(true)]public void CrossWorldCreationFailureKeepsAllReservations(bool activation)
        {
            var f1=new Factory();var f2=new Factory{FailStage=!activation,FailActivation=activation};
            var a=Session(f1,out var wa);var b=Session(f2,out var wb);var d=Definition();
            a.Add(d,Vector2Int.zero,out _);b.Add(d,Vector2Int.zero,out _);
            Assert.That(PlacementSession.ConfirmTogether(new[]{a,b}).Success,Is.False);
            Assert.That(wa.Buildings.Count+wb.Buildings.Count,Is.Zero);Assert.That(wa.Grid.OccupiedCount+wb.Grid.OccupiedCount,Is.Zero);
            Assert.That(wa.Grid.ReservationCount,Is.EqualTo(1));Assert.That(wb.Grid.ReservationCount,Is.EqualTo(1));
            f2.FailStage=f2.FailActivation=false;
            Assert.That(PlacementSession.ConfirmTogether(new[]{a,b}).Success,Is.True);
            Assert.That(wa.Buildings.Count+wb.Buildings.Count,Is.EqualTo(2));
        }
        [Test]public void CrossWorldRevalidationAndRecoveryAreAllOrNothing()
        {
            var a=Session(new Factory(),out var wa);var b=Session(new Factory(),out var wb);var d=Definition();
            a.Add(d,Vector2Int.zero,out _);b.Add(d,Vector2Int.zero,out _);wb.Grid.SetBlocked(Vector2Int.zero,true);
            Assert.That(PlacementSession.ConfirmTogether(new[]{a,b}).Success,Is.False);Assert.That(wa.Buildings.Count,Is.Zero);
            wb.Grid.SetBlocked(Vector2Int.zero,false);Assert.That(PlacementSession.ConfirmTogether(new[]{a,b}).Success,Is.True);
            var ra=new RecoverySession(wa);var rb=new RecoverySession(wb);int dead=0;
            foreach(var building in wa.Buildings)ra.Toggle(building.Id);foreach(var building in wb.Buildings){rb.Toggle(building.Id);dead=building.Id;}
            wb.Remove(dead);Assert.That(RecoverySession.ConfirmTogether(new[]{ra,rb}).Success,Is.False);Assert.That(wa.Buildings.Count,Is.EqualTo(1));
            rb.Deselect(dead);Assert.That(RecoverySession.ConfirmTogether(new[]{ra,rb}).Success,Is.True);Assert.That(wa.Buildings.Count,Is.Zero);
        }
        sealed class Target:IDamageReceiver {public bool Alive=>true;public void ApplyDamage(float n){}}
        [Test]public void CompatibleFilterChangeRetainsTargetAndIncompatibleChangeClearsIt()
        {
            var q=new TargetRegistry();var handle=q.Register(1,Vector3.forward*5,TargetKind.Ground,new Target());
            var d=Definition();var attack=Asset<AttackModuleDefinition>();attack.Targets=TargetKind.Ground;d.Modules.Add(attack);
            var building=new BuildingInstance(1,d,Vector2Int.zero,Vector3.zero,new BuildingServices(q));owned.Add(building);building.Activate();building.Tick(0);
            q.Register(2,Vector3.forward,TargetKind.Ground,new Target());var module=building.Module<AttackModule>();module.Kinds=TargetKind.All;building.Tick(.01f);
            Assert.That(module.CurrentTarget,Is.EqualTo(handle));module.Kinds=TargetKind.Air;Assert.That(module.CurrentTarget,Is.Null);
        }
    }
}
