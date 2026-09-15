using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
namespace EternalSteam.Tests
{
    public sealed class ExtensionTests
    {
        readonly List<Object> assets=new(); readonly List<IDisposable> instances=new();
        T Asset<T>() where T:ScriptableObject { var item=ScriptableObject.CreateInstance<T>(); assets.Add(item); return item; }
        BuildingDefinition Definition(params BuildingModuleDefinition[] modules)
        {
            var d=Asset<BuildingDefinition>(); d.Id=Guid.NewGuid().ToString(); d.DisplayName="Test";
            d.ViewPrefab=new GameObject("Test prefab"); assets.Add(d.ViewPrefab); d.Modules.AddRange(modules); return d;
        }
        BuildingInstance Building(BuildingServices services,params BuildingModuleDefinition[] modules)=>Building(services,Definition(modules));
        BuildingInstance Building(BuildingServices services,BuildingDefinition d)
        { var b=new BuildingInstance(instances.Count+1,d,Vector2Int.zero,Vector3.zero,services); instances.Add(b); b.Activate(); return b; }
        sealed class Enemy:IDamageReceiver,IStatusReceiver
        {
            public float Health=100; public readonly StatusState Status=new(); public bool Alive=>Health>0;
            public void ApplyDamage(float amount)=>Health-=amount;
            public void ApplyStatus(StatusKind kind,float strength,float duration)=>Status.ApplyStatus(kind,strength,duration);
        }
        [TearDown] public void Cleanup()
        { for(int i=instances.Count-1;i>=0;i--) instances[i].Dispose(); instances.Clear(); foreach(var a in assets) Object.DestroyImmediate(a); assets.Clear(); }
        [Test] public void AreaFiltersAirAndSectorWhileHittingNeighbours()
        {
            var q=new TargetRegistry(); var primary=new Enemy(); var nearby=new Enemy(); var air=new Enemy(); var behind=new Enemy();
            q.Register(1,Vector3.forward*2,TargetKind.Ground,primary); q.Register(2,new Vector3(1,0,3),TargetKind.Ground,nearby);
            q.Register(3,Vector3.forward*3,TargetKind.Air,air); q.Register(4,Vector3.back,TargetKind.Ground,behind);
            var a=Asset<AttackModuleDefinition>(); a.Execution=Asset<AreaAttackDefinition>(); a.Targets=TargetKind.Ground;
            Building(new BuildingServices(q),a).Tick(0);
            Assert.That(primary.Health,Is.EqualTo(90)); Assert.That(nearby.Health,Is.EqualTo(90)); Assert.That(air.Health,Is.EqualTo(100)); Assert.That(behind.Health,Is.EqualTo(100));
        }
        [Test] public void PiercingUsesOrderedWidthLimitedHits()
        {
            var q=new TargetRegistry(); var first=new Enemy(); var second=new Enemy(); var third=new Enemy(); var side=new Enemy();
            q.Register(1,Vector3.forward*2,TargetKind.Ground,first); q.Register(2,Vector3.forward*3,TargetKind.Ground,second);
            q.Register(3,Vector3.forward*4,TargetKind.Ground,third); q.Register(4,new Vector3(1,0,2),TargetKind.Ground,side);
            var execution=Asset<PiercingAttackDefinition>(); execution.MaximumHits=2;
            var a=Asset<AttackModuleDefinition>(); a.Execution=execution; Building(new BuildingServices(q),a).Tick(0);
            Assert.That(first.Health,Is.EqualTo(90)); Assert.That(second.Health,Is.EqualTo(90)); Assert.That(third.Health,Is.EqualTo(100)); Assert.That(side.Health,Is.EqualTo(100));
        }
        [Test] public void ChainDoesNotRepeatAndUsesDeterministicNearestHop()
        {
            var q=new TargetRegistry(); var first=new Enemy(); var left=new Enemy(); var right=new Enemy();
            q.Register(1,Vector3.forward*2,TargetKind.Ground,first); q.Register(3,new Vector3(-1,0,3),TargetKind.Ground,left); q.Register(2,new Vector3(1,0,3),TargetKind.Ground,right);
            var execution=Asset<ChainAttackDefinition>(); execution.MaximumHits=2; execution.DamageRetention=0.5f;
            var a=Asset<AttackModuleDefinition>(); a.Execution=execution; Building(new BuildingServices(q),a).Tick(0);
            Assert.That(first.Health,Is.EqualTo(90)); Assert.That(right.Health,Is.EqualTo(95)); Assert.That(left.Health,Is.EqualTo(100));
        }
        [Test] public void ProjectileDelaysDamageAndFollowsBeyondRange()
        {
            var q=new TargetRegistry(); var enemy=new Enemy(); var handle=q.Register(1,Vector3.forward*2,TargetKind.Ground,enemy);
            var execution=Asset<ProjectileAttackDefinition>(); execution.Speed=10; execution.Lifetime=10;
            var a=Asset<AttackModuleDefinition>(); a.Execution=execution; a.Interval=100;
            var b=Building(new BuildingServices(q),a); b.Tick(0); Assert.That(enemy.Health,Is.EqualTo(100));
            q.Move(handle,Vector3.forward*25); b.Tick(3); Assert.That(enemy.Health,Is.EqualTo(90));
        }
        [Test] public void ProjectileCannotHitReusedSlotAndDisposeCancelsFlights()
        {
            var q=new TargetRegistry(); var handle=q.Register(1,Vector3.forward*2,TargetKind.Ground,new Enemy());
            var a=Asset<AttackModuleDefinition>(); a.Execution=Asset<ProjectileAttackDefinition>(); a.Interval=100;
            var b=Building(new BuildingServices(q),a); b.Tick(0); q.Unregister(handle);
            var replacement=new Enemy(); q.Register(1,Vector3.forward*2,TargetKind.Ground,replacement); b.Tick(1);
            Assert.That(replacement.Health,Is.EqualTo(100)); b.Dispose(); b.Tick(100); Assert.That(replacement.Health,Is.EqualTo(100));
        }
        [Test] public void StatusDurationsAreIndependentAndSettingsAreCopied()
        {
            var q=new TargetRegistry(); var enemy=new Enemy(); q.Register(1,Vector3.forward*2,TargetKind.Ground,enemy);
            var effect=Asset<StatusEffectDefinition>(); effect.Kind=StatusKind.Slow; effect.Strength=0.5f; effect.Duration=2;
            var a=Asset<AttackModuleDefinition>(); a.Effects.Add(effect); var b=Building(new BuildingServices(q),a); effect.Strength=0.9f;
            b.Tick(0); Assert.That(enemy.Status.MovementMultiplier,Is.EqualTo(0.5f));
            enemy.Status.ApplyStatus(StatusKind.Stun,1,0.5f); Assert.That(enemy.Status.MovementMultiplier,Is.Zero);
            enemy.Status.Tick(0.5f); Assert.That(enemy.Status.MovementMultiplier,Is.EqualTo(0.5f));
            enemy.Status.Tick(1.5f); Assert.That(enemy.Status.MovementMultiplier,Is.EqualTo(1));
        }
        [Test] public void NexusUnlocksUpgradesAndDestructionSignalsOnceButDisposalDoesNot()
        {
            var state=new NexusState(); var services=new BuildingServices(null,state); int lost=0; state.Lost+=()=>lost++;
            var upgrade=Asset<UpgradeModuleDefinition>(); var tower=Building(services,Asset<HealthModuleDefinition>(),upgrade);
            Assert.That(tower.Module<UpgradeModule>().TryUpgrade(out _),Is.False);
            var d=Definition(Asset<HealthModuleDefinition>(),Asset<NexusModuleDefinition>(),upgrade); d.Recoverable=false;
            var core=Building(services,d); Assert.That(core.Module<UpgradeModule>().TryUpgrade(out _),Is.True);
            Assert.That(tower.Module<UpgradeModule>().TryUpgrade(out _),Is.True);
            Assert.That(tower.Module<HealthModule>().Maximum,Is.EqualTo(120).Within(0.001));
            core.Module<HealthModule>().ApplyDamage(1000); core.Destroy(); Assert.That(lost,Is.EqualTo(1)); Assert.That(state.LevelCap,Is.Zero);
            var otherState=new NexusState(); var other=Building(new BuildingServices(null,otherState),d); other.Dispose(); Assert.That(otherState.Defeated,Is.False);
        }
        [Test] public void NexusLossImmediatelyEndsSessionAndUnsubscribesOnDispose()
        {
            var state=new NexusState(); var world=new BuildingWorld(new BuildGrid(new RectInt(0,0,3,3),2),new Factory(new BuildingServices(null,state)));
            using var session=new SandboxSession(world,new PreparationOnly(),state);
            var d=Definition(Asset<HealthModuleDefinition>(),Asset<NexusModuleDefinition>()); d.Recoverable=false;
            session.BeginPlacement(); session.Add(d,Vector2Int.zero,out _); Assert.That(session.Confirm().Success,Is.True); Assert.That(session.StartCombat(),Is.True);
            foreach(var b in new List<BuildingInstance>(world.Buildings)) b.Module<HealthModule>().ApplyDamage(1000);
            Assert.That(session.Phase,Is.EqualTo(SandboxPhase.Result)); Assert.That(session.Defeated,Is.True);
        }
        [Test] public void ProductionWaitsForStorageAndConversionIsAtomic()
        {
            var bank=new ResourceBank(); var services=new BuildingServices(null,null,bank);
            var producer=Building(services,Asset<ProductionModuleDefinition>()); producer.Tick(1); Assert.That(bank.Amount("sample.energy"),Is.Zero);
            var storage=Building(services,Asset<StorageModuleDefinition>()); producer.Tick(0); Assert.That(bank.Amount("sample.energy"),Is.EqualTo(5));
            Assert.That(bank.Exchange("sample.energy",5,"sample.parts",1),Is.False); Assert.That(bank.Amount("sample.energy"),Is.EqualTo(5));
            bank.AddCapacity("sample.parts",10); Assert.That(bank.Exchange("sample.energy",5,"sample.parts",1),Is.True);
            producer.Tick(1); storage.Dispose(); Assert.That(bank.Capacity("sample.energy"),Is.Zero); Assert.That(bank.Amount("sample.energy"),Is.EqualTo(5));
        }
        [Test] public void RemovingOptionalModulesRemovesDependenciesAndMovementRegistration()
        {
            var d=Definition(Asset<ObstacleModuleDefinition>()); var obstacles=new MovementObstacles(2);
            var b=Building(new BuildingServices(null,null,null,obstacles),d); Assert.That(obstacles.At(Vector3.zero),Is.SameAs(b)); b.Dispose(); Assert.That(obstacles.At(Vector3.zero),Is.Null);
            d.Modules.Clear(); var empty=Building(new BuildingServices(null),d); empty.Tick(1); Assert.That(empty.Active,Is.True);
        }
        [Test] public void MissingReferencesAndNexusCompositionAreRejected()
        {
            var d=Definition(Asset<NexusModuleDefinition>()); Assert.That(d.Validate().Count,Is.EqualTo(2));
            var a=Asset<AttackModuleDefinition>(); a.Effects.Add(null); d=Definition(a); Assert.That(d.Validate(),Has.Some.Contains("Missing hit effect"));
            var production=Asset<ProductionModuleDefinition>(); production.InputAmount=1; var errors=new List<string>(); production.Validate(errors); Assert.That(errors,Is.Not.Empty);
        }
        [Test] public void FailedBatchRollsBackNexusStorageAndObstacleRegistration()
        {
            var state=new NexusState(); var bank=new ResourceBank(); var obstacles=new MovementObstacles(2);
            var factory=new Factory(new BuildingServices(null,state,bank,obstacles)) { FailSecondActivation=true };
            using var world=new BuildingWorld(new BuildGrid(new RectInt(0,0,4,4),2),factory);
            using var placement=new PlacementSession(world);
            var core=Definition(Asset<HealthModuleDefinition>(),Asset<NexusModuleDefinition>(),Asset<StorageModuleDefinition>(),Asset<ObstacleModuleDefinition>()); core.Recoverable=false;
            placement.Add(core,Vector2Int.zero,out _); placement.Add(core,Vector2Int.one,out _);
            Assert.That(placement.Confirm().Success,Is.False);
            Assert.That(world.Buildings,Is.Empty); Assert.That(world.Grid.ReservationCount,Is.EqualTo(2));
            Assert.That(state.LevelCap,Is.Zero); Assert.That(state.Defeated,Is.False);
            Assert.That(bank.Capacity("sample.energy"),Is.Zero); Assert.That(obstacles.At(Vector3.one),Is.Null);
            factory.FailSecondActivation=false; Assert.That(placement.Confirm().Success,Is.True);
            Assert.That(state.LevelCap,Is.EqualTo(1)); Assert.That(bank.Capacity("sample.energy"),Is.EqualTo(200));
        }
        sealed class Factory:IBuildingFactory
        {
            public bool FailSecondActivation; int activated;
            readonly BuildingServices services; public Factory(BuildingServices services) { this.services=services; }
            public BuildingInstance Stage(int id,PlacementRequest request,Vector3 position)=>new(id,request.Definition,request.Cell,position,services);
            public void Activate(BuildingInstance b) { if(++activated==2 && FailSecondActivation) throw new InvalidOperationException("Injected failure"); } public void Remove(BuildingInstance b) { }
        }
    }
}
