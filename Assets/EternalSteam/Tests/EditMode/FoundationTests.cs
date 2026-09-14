using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EternalSteam.Tests
{
    public sealed class FoundationTests
    {
        readonly List<Object> assets = new();
        readonly List<IDisposable> owned = new();
        BuildingDefinition Definition(Vector2Int? size = null)
        {
            var definition = ScriptableObject.CreateInstance<BuildingDefinition>(); assets.Add(definition);
            definition.Id = Guid.NewGuid().ToString(); definition.DisplayName = "Test building";
            definition.ViewPrefab = new GameObject("Test view"); assets.Add(definition.ViewPrefab);
            definition.Footprint = size ?? Vector2Int.one;
            return definition;
        }
        T Asset<T>() where T : ScriptableObject
        { var asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset; }
        BuildingWorld World(out FakeFactory factory, TargetRegistry registry = null)
        {
            factory = new FakeFactory(registry ?? new TargetRegistry());
            var world = new BuildingWorld(new BuildGrid(new RectInt(0,0,6,6),2),factory); owned.Add(world); return world;
        }
        PlacementSession Placement(BuildingWorld world)
        { var placement = new PlacementSession(world); owned.Add(placement); return placement; }
        BuildingInstance Instance(BuildingDefinition definition, TargetRegistry registry, int id = 1)
        {
            var instance = new BuildingInstance(id,definition,Vector2Int.zero,Vector3.zero,new BuildingServices(registry));
            owned.Add(instance); instance.Activate(); return instance;
        }
        [TearDown] public void Cleanup()
        {
            for (int i = owned.Count-1; i >= 0; i--) owned[i].Dispose(); owned.Clear();
            foreach (var asset in assets) Object.DestroyImmediate(asset); assets.Clear();
        }
        [Test] public void MultiCellBoundsAndCenter()
        {
            var world = World(out _); var placement = Placement(world); var definition = Definition(new Vector2Int(2,2));
            Assert.That(placement.Add(definition,new Vector2Int(5,5),out _).Code,Is.EqualTo("terrain"));
            Assert.That(world.Grid.ReservationCount,Is.Zero);
            Assert.That(placement.Add(definition,new Vector2Int(4,4),out _).Success,Is.True);
            Assert.That(world.Grid.ReservationCount,Is.EqualTo(4));
            Assert.That(placement.Confirm().Success,Is.True);
            Assert.That(world.Buildings.Single().Position,Is.EqualTo(new Vector3(10,0,10)));
            Assert.That(world.Grid.OccupiedCount,Is.EqualTo(4));
            Assert.That(world.Grid.ReservationCount,Is.Zero);
        }
        [Test] public void ReservationsSurviveSelectionAndFailedMove()
        {
            var world = World(out _); var placement = Placement(world); var definition = Definition();
            placement.Add(definition,Vector2Int.zero,out var first);
            placement.Add(definition,Vector2Int.right,out var second);
            Assert.That(placement.Add(definition,Vector2Int.zero,out _).Code,Is.EqualTo("occupied"));
            Assert.That(placement.Move(first.Id,second.Cell).Success,Is.False);
            Assert.That(first.Cell,Is.EqualTo(Vector2Int.zero));
            Assert.That(world.Grid.ReservationAt(Vector2Int.zero),Is.EqualTo(first.Id));
            Assert.That(placement.Move(first.Id,Vector2Int.up).Success,Is.True);
            Assert.That(world.Grid.ReservationAt(Vector2Int.zero),Is.Null);
            placement.Cancel(); Assert.That(world.Grid.ReservationCount,Is.Zero);
        }
        [Test] public void RevalidationRejectsWholeBatchAndKeepsReservations()
        {
            var world = World(out _); var placement = Placement(world); var definition = Definition();
            placement.Add(definition,Vector2Int.zero,out _); placement.Add(definition,Vector2Int.one,out _);
            world.Grid.SetBlocked(Vector2Int.one,true);
            Assert.That(placement.Confirm().Success,Is.False);
            Assert.That(world.Buildings,Is.Empty); Assert.That(world.Grid.ReservationCount,Is.EqualTo(2));
            world.Grid.SetBlocked(Vector2Int.one,false);
            Assert.That(placement.Confirm().Success,Is.True);
            Assert.That(placement.Add(definition,Vector2Int.one,out _).Code,Is.EqualTo("occupied"));
        }
        [TestCase(false)] [TestCase(true)] public void FactoryFailureRollsBackWithoutLosingPendingWork(bool activationFailure)
        {
            var world = World(out var factory); var placement = Placement(world); var definition = Definition();
            factory.FailStage = !activationFailure; factory.FailActivate = activationFailure;
            placement.Add(definition,Vector2Int.zero,out _); placement.Add(definition,Vector2Int.one,out _);
            Assert.That(placement.Confirm().Code,Is.EqualTo("creation"));
            Assert.That(world.Buildings,Is.Empty); Assert.That(factory.Live,Is.Empty);
            Assert.That(world.Grid.OccupiedCount,Is.Zero); Assert.That(world.Grid.ReservationCount,Is.EqualTo(2));
            factory.FailStage = factory.FailActivate = false;
            Assert.That(placement.Confirm().Success,Is.True);
        }
        [Test] public void RecoveryPreservesActivityUntilCommitAndBlocksStaleBatch()
        {
            var world = World(out _); var placement = Placement(world); var definition = Definition();
            placement.Add(definition,Vector2Int.zero,out _); placement.Add(definition,Vector2Int.one,out _); placement.Confirm();
            var instances = world.Buildings.ToArray(); var recovery = new RecoverySession(world);
            Assert.That(recovery.Toggle(instances[0].Id),Is.True);
            Assert.That(instances[0].Active,Is.True); Assert.That(world.Grid.OccupiedCount,Is.EqualTo(2));
            recovery.Cancel(); Assert.That(instances[0].Active,Is.True);
            foreach (var instance in instances) recovery.Toggle(instance.Id);
            world.Remove(instances[0].Id);
            Assert.That(recovery.Confirm().Code,Is.EqualTo("recovery-invalid"));
            Assert.That(instances[1].Active,Is.True);
            recovery.Deselect(instances[0].Id); Assert.That(recovery.Confirm().Success,Is.True);
            Assert.That(world.Grid.OccupiedCount,Is.Zero);
        }
        [Test] public void NonRecoverableAndDestroyedBuildings()
        {
            var world = World(out _); var placement = Placement(world); var definition = Definition(); definition.Recoverable = false;
            definition.Modules.Add(Asset<HealthModuleDefinition>());
            placement.Add(definition,Vector2Int.zero,out _); placement.Confirm();
            var instance = world.Buildings.Single();
            Assert.That(new RecoverySession(world).Toggle(instance.Id),Is.False);
            instance.Module<HealthModule>().ApplyDamage(100);
            Assert.That(world.Buildings,Is.Empty); Assert.That(world.Grid.OccupiedCount,Is.Zero);
        }
        [Test] public void DefinitionsDoNotShareRuntimeStateOrLiveSettings()
        {
            var definition = Definition(); var health = Asset<HealthModuleDefinition>(); var attack = Asset<AttackModuleDefinition>();
            definition.Modules.Add(health); definition.Modules.Add(attack);
            var registry = new TargetRegistry(); var target = new Receiver(); registry.Register(1,Vector3.forward*3,TargetKind.Ground,target);
            var first = Instance(definition,registry); var second = Instance(definition,registry,2);
            health.Maximum = 1; attack.Damage = 99; definition.Footprint = new Vector2Int(4,4);
            first.Module<HealthModule>().ApplyDamage(20);
            Assert.That(first.Module<HealthModule>().Current,Is.EqualTo(80));
            Assert.That(second.Module<HealthModule>().Current,Is.EqualTo(100));
            Assert.That(first.Footprint,Is.EqualTo(Vector2Int.one));
            first.Tick(0); first.Tick(0); second.Tick(0);
            Assert.That(target.Health,Is.EqualTo(80));
        }
        [Test] public void TargetLockRetainsCloserArrivalAndReacquiresOnRemoval()
        {
            var definition = Definition(); definition.Modules.Add(Asset<AttackModuleDefinition>());
            var registry = new TargetRegistry(); var far = new Receiver(); var near = new Receiver();
            var farHandle = registry.Register(4,Vector3.forward*5,TargetKind.Ground,far);
            var building = Instance(definition,registry); building.Tick(0);
            var nearHandle = registry.Register(1,Vector3.forward*2,TargetKind.Ground,near);
            building.Tick(0.5f); Assert.That(building.Module<AttackModule>().CurrentTarget,Is.EqualTo(farHandle));
            registry.Unregister(farHandle); building.Tick(0);
            Assert.That(building.Module<AttackModule>().CurrentTarget,Is.EqualTo(nearHandle));
            Assert.That(near.Health,Is.EqualTo(100),"Retarget must preserve cooldown.");
        }
        [Test] public void RecycledSlotCannotResolveOldHandle()
        {
            var registry = new TargetRegistry(); var first = registry.Register(1,Vector3.zero,TargetKind.Ground,new Receiver());
            registry.Unregister(first); var second = registry.Register(1,Vector3.zero,TargetKind.Air,new Receiver());
            registry.Unregister(first);
            Assert.That(registry.TryGet(first,out _),Is.False); Assert.That(registry.TryGet(second,out _),Is.True);
            Assert.That(second.Generation,Is.GreaterThan(first.Generation));
        }
        [Test] public void SectorIncludesBoundariesAndIgnoresHeight()
        {
            var receiver = new Receiver();
            var target = new TargetInfo(new TargetHandle(1,1),new Vector3(3,40,3),TargetKind.Air,receiver);
            Assert.That(Sector.Contains(Vector3.zero,Vector3.forward,Mathf.Sqrt(18)+0.0001f,90,TargetKind.All,target),Is.True);
            Assert.That(Sector.Contains(Vector3.zero,Vector3.forward,4,90,TargetKind.All,target),Is.False);
            Assert.That(Sector.Contains(Vector3.zero,Vector3.forward,10,89,TargetKind.All,target),Is.False);
            Assert.That(Sector.Contains(Vector3.zero,Vector3.forward,10,90,TargetKind.Ground,target),Is.False);
        }
        [Test] public void TargetKindsAndEqualDistanceTieBreak()
        {
            var definition = Definition(); definition.Modules.Add(Asset<AttackModuleDefinition>());
            var registry = new TargetRegistry(); registry.Register(5,new Vector3(0,1,3),TargetKind.Ground,new Receiver());
            var air = registry.Register(2,new Vector3(0,8,3),TargetKind.Air,new Receiver());
            var instance = Instance(definition,registry); instance.Tick(0);
            Assert.That(instance.Module<AttackModule>().CurrentTarget,Is.EqualTo(air));
            instance.Module<AttackModule>().Kinds = TargetKind.Ground; instance.Tick(0);
            Assert.That(instance.Module<AttackModule>().CurrentTarget.Value.Id,Is.EqualTo(5));
            registry.Move(new TargetHandle(5,1),Vector3.back*30); instance.Tick(0.5f);
            Assert.That(instance.Module<AttackModule>().CurrentTarget,Is.Null);
        }
        [Test] public void DirectionEditIsTransactionalAndPhasePolicyGuardsCommands()
        {
            var world = World(out _); var definition = Definition(); definition.Modules.Add(Asset<AttackModuleDefinition>());
            using var session = new SandboxSession(world,new PreparationOnly());
            session.BeginPlacement(); Assert.That(session.StartCombat(),Is.False);
            session.Add(definition,Vector2Int.zero,out _); session.Confirm();
            var instance = world.Buildings.Single();
            Assert.That(session.BeginDirection(instance.Id),Is.True);
            session.Aim(instance.Position+Vector3.right);
            Assert.That(instance.Direction,Is.EqualTo(Vector3.forward)); Assert.That(instance.EditingDirection,Is.True);
            session.Cancel(); Assert.That(instance.Direction,Is.EqualTo(Vector3.forward)); Assert.That(instance.EditingDirection,Is.False);
            session.BeginDirection(instance.Id); session.Aim(instance.Position+Vector3.right); session.Confirm();
            Assert.That(instance.Direction,Is.EqualTo(Vector3.right));
            Assert.That(session.StartCombat(),Is.True); session.BeginPlacement(); Assert.That(session.Mode,Is.EqualTo(EditMode.None));
            Assert.That(session.BeginDirection(instance.Id),Is.False);
        }
        [Test] public void ModuleRemovalAndCatalogValidation()
        {
            var definition = Definition(); var module = Asset<HealthModuleDefinition>();
            definition.Modules.Add(module); definition.Modules.Add(module);
            Assert.That(definition.Validate().Any(x=>x.Contains("Duplicate")),Is.True);
            definition.Modules.Clear();
            var instance = Instance(definition,new TargetRegistry()); Assert.That(instance.Module<HealthModule>(),Is.Null);
            var catalog = Asset<BuildingCatalog>(); catalog.Buildings.Add(definition); catalog.Buildings.Add(definition);
            Assert.That(catalog.Validate().Any(x=>x.Contains("Duplicate building ID")),Is.True);
            definition.ViewPrefab = null; definition.Footprint = Vector2Int.zero;
            Assert.That(definition.Validate().Count,Is.EqualTo(2));
        }
        sealed class Receiver : IDamageReceiver
        { public float Health = 100; public bool Alive => Health > 0; public void ApplyDamage(float amount) => Health -= amount; }
        sealed class FakeFactory : IBuildingFactory
        {
            readonly TargetRegistry registry;
            int staged,activated;
            public bool FailStage,FailActivate;
            public readonly List<BuildingInstance> Live = new();
            public FakeFactory(TargetRegistry registry) { this.registry = registry; }
            public BuildingInstance Stage(int id,PlacementRequest request,Vector3 position)
            {
                if (++staged == 2 && FailStage) throw new Exception("Injected stage failure");
                var instance = new BuildingInstance(id,request.Definition,request.Cell,position,new BuildingServices(registry)); Live.Add(instance); return instance;
            }
            public void Activate(BuildingInstance instance) { if (++activated == 2 && FailActivate) throw new Exception("Injected activation failure"); }
            public void Remove(BuildingInstance instance) => Live.Remove(instance);
        }
    }
}
