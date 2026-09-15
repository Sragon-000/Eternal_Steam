using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
namespace EternalSteam.Demo.Tests
{
    public sealed class HordeBuildingTests
    {
        FakeFactory factory;HordeBuildingPlacement adapter;bool permitted;int limit;
        [SetUp] public void Setup()
        {
            permitted=true;limit=2;factory=new FakeFactory();
            adapter=new HordeBuildingPlacement(factory,HordeMapKind.Lane,()=>permitted,()=>factory.Live.Count,k=>factory.Live.Values.Count(x=>x.kind==k),_=>limit);
        }
        [TearDown] public void Cleanup() { adapter.Dispose();Object.DestroyImmediate(factory.Template); }
        [Test] public void OffsetGridPreservesDefaultCentersAndLegacyEvenCenters()
        {
            var normal=new BuildGrid(new RectInt(-3,-3,6,6),2);
            Assert.That(normal.Center(Vector2Int.zero,Vector2Int.one),Is.EqualTo(new Vector3(1,0,1)));
            var grid=adapter.World.Grid;
            foreach(var p in new[]{Vector3.zero,new Vector3(-12,0,-8),new Vector3(4,0,8)})
                Assert.That(grid.Center(grid.WorldToCell(p),Vector2Int.one),Is.EqualTo(p));
            Assert.Throws<ArgumentException>(()=>new BuildGrid(new RectInt(0,0,1,1),2,new Vector3(float.NaN,0,0)));
        }
        [TestCase(HordeMapKind.Lane)] [TestCase(HordeMapKind.WideFront)] [TestCase(HordeMapKind.Pincer)]
        public void AllCellsMatchOriginalPlacementFootprintRule(HordeMapKind map)
        {
            adapter.ConfigureMap(map);var zones=HordeMapLayout.BuildZones(map);var grid=adapter.World.Grid;int valid=0;
            for(int z=-34;z<=34;z+=2)for(int x=-44;x<=44;x+=2)
            {
                var p=new Vector3(x,0,z);bool expected=false;
                foreach(var zone in zones)if(HordeMapLayout.ContainsFootprint(zone,p,HordeMapLayout.BuildMargin) && HordeMapLayout.ContainsFootprint(zone,p,HordeMapLayout.TowerHalfWidth))expected=true;
                Assert.That(adapter.CanPlace(p,HordeTowerKind.MachineGun),Is.EqualTo(expected),map+" "+p);if(expected)valid++;
            }
            Assert.That(valid,Is.GreaterThan(0));
        }
        [Test] public void PlacementSharesOccupancyLifetimeAndStableDefinitionId()
        {
            var p=new Vector3(-12,0,-8);
            Assert.That(adapter.TryPlace(p,Vector3.forward,12,HordeTowerKind.Arrow),Is.True);
            var b=adapter.World.Buildings.Single();Assert.That(b.Position,Is.EqualTo(p));Assert.That(b.DefinitionId,Is.EqualTo("legacy.arrow"));
            Assert.That(factory.Live[b.Id].range,Is.EqualTo(12));Assert.That(adapter.World.Grid.OccupiedCount,Is.EqualTo(1));
            Assert.That(adapter.CanPlace(p+new Vector3(0.3f,0,0.3f),HordeTowerKind.Cannon),Is.False);
            adapter.World.Remove(b.Id);Assert.That(factory.Live,Is.Empty);Assert.That(adapter.World.Grid.OccupiedCount,Is.Zero);
            Assert.That(adapter.TryPlace(p,Vector3.right,10,HordeTowerKind.Cannon),Is.True);adapter.Clear();
            Assert.That(adapter.World.Buildings,Is.Empty);Assert.That(factory.Live,Is.Empty);
        }
        [TestCase(false)] [TestCase(true)] public void FailedCreationRollsBackWorldLegacyListAndReservations(bool activate)
        {
            factory.FailStage=!activate;factory.FailActivate=activate;
            Assert.That(adapter.TryPlace(new Vector3(-12,0,-8),Vector3.forward,12,HordeTowerKind.Cannon),Is.False);
            Assert.That(adapter.World.Buildings,Is.Empty);Assert.That(factory.Live,Is.Empty);Assert.That(factory.Staged,Is.Empty);
            Assert.That(adapter.World.Grid.OccupiedCount,Is.Zero);Assert.That(adapter.World.Grid.ReservationCount,Is.Zero);Assert.That(factory.Requests,Is.Empty);
            factory.FailStage=factory.FailActivate=false;
            Assert.That(adapter.TryPlace(new Vector3(-12,0,-8),Vector3.forward,12,HordeTowerKind.Cannon),Is.True);
        }
        [Test] public void PhaseAndCapacityAreRecheckedBeforeCommit()
        {
            var p=new Vector3(-12,0,-8);permitted=false;Assert.That(adapter.TryPlace(p,Vector3.forward,12,HordeTowerKind.Frost),Is.False);
            permitted=true;factory.BeforeCommit=()=>limit=0;
            Assert.That(adapter.TryPlace(p,Vector3.forward,12,HordeTowerKind.Frost),Is.False);
            Assert.That(adapter.World.Buildings,Is.Empty);Assert.That(factory.Requests,Is.Empty);Assert.That(adapter.World.Grid.ReservationCount,Is.Zero);
        }
        [Test] public void InvalidDirectionRangeAndKindCannotLeaveState()
        {
            var p=new Vector3(-12,0,-8);
            Assert.That(adapter.TryPlace(p,Vector3.zero,12,HordeTowerKind.Frost),Is.False);
            Assert.That(adapter.TryPlace(p,Vector3.forward,float.NaN,HordeTowerKind.Frost),Is.False);
            Assert.That(adapter.TryPlace(p,Vector3.forward,12,(HordeTowerKind)99),Is.False);
            Assert.That(adapter.World.Grid.ReservationCount,Is.Zero);Assert.That(factory.Live,Is.Empty);
        }
        [Test] public void AdapterDoesNotReferenceSimulationAssembly()
        { Assert.That(typeof(HordeBuildingPlacement).Assembly.GetReferencedAssemblies().Select(x=>x.Name),Does.Not.Contain("EternalSteam.LegacyDemo")); }
        sealed class FakeFactory:IHordeTowerFactory
        {
            public GameObject Template { get; }=new GameObject("Factory template");
            public readonly Dictionary<int,(float range,HordeTowerKind kind)> Requests=new(),Live=new(),Staged=new();
            public bool FailStage,FailActivate;public Action BeforeCommit;
            public void ConfigureRequest(int id,float range,HordeTowerKind kind) { Requests.Add(id,(range,kind));BeforeCommit?.Invoke(); }
            public void ForgetRequest(int id)=>Requests.Remove(id);
            public BuildingInstance Stage(int id,PlacementRequest request,Vector3 position)
            {
                if(FailStage)throw new Exception("Injected stage failure");
                var b=new BuildingInstance(id,request.Definition,request.Cell,position,new BuildingServices(null));Staged.Add(id,Requests[request.Id]);return b;
            }
            public void Activate(BuildingInstance b) { Live.Add(b.Id,Staged[b.Id]);if(FailActivate)throw new Exception("Injected activation failure"); }
            public void Remove(BuildingInstance b) { Staged.Remove(b.Id);Live.Remove(b.Id); }
        }
    }
}
