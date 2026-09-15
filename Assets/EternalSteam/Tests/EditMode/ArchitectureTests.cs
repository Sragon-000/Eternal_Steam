using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
namespace EternalSteam.Tests
{
    public sealed class ArchitectureTests
    {
        readonly List<Object> assets=new(); readonly List<BuildingInstance> buildings=new();
        T Asset<T>() where T:ScriptableObject { var item=ScriptableObject.CreateInstance<T>(); assets.Add(item); return item; }
        BuildingDefinition Definition(params BuildingModuleDefinition[] modules)
        {
            var d=Asset<BuildingDefinition>(); d.Id="architecture"; d.DisplayName="Architecture fixture";
            d.ViewPrefab=new GameObject("View"); assets.Add(d.ViewPrefab); d.Modules.AddRange(modules); return d;
        }
        BuildingInstance Create(BuildingDefinition d,BuildingServices services)
        { var b=new BuildingInstance(buildings.Count+1,d,Vector2Int.zero,Vector3.zero,services); buildings.Add(b); b.Activate(); return b; }
        [TearDown] public void Cleanup()
        { foreach(var b in buildings) b.Dispose(); buildings.Clear(); foreach(var a in assets) Object.DestroyImmediate(a); assets.Clear(); }
        [Test] public void RuntimeAndFeatureAssembliesEnforceOneWayDependencies()
        {
            var runtime=typeof(BuildingInstance).Assembly;
            Assert.That(runtime.GetName().Name,Is.EqualTo("EternalSteam.Runtime"));
            Assert.That(runtime.GetReferencedAssemblies().Where(x=>x.Name.StartsWith("EternalSteam.")),Is.Empty);
            var features=new[]{typeof(AttackModule),typeof(UpgradeModule),typeof(ResourceBank),typeof(MovementObstacles),typeof(BuildingView)};
            Assert.That(features.Select(x=>x.Assembly).Distinct().Count(),Is.EqualTo(5));
            foreach(var type in features)
                Assert.That(type.Assembly.GetReferencedAssemblies().Where(x=>x.Name.StartsWith("EternalSteam.")).Select(x=>x.Name),Is.EquivalentTo(new[]{"EternalSteam.Runtime"}),type.Name);
        }
        [Test] public void AttackAcceptsIndependentDamageProviderAndUnsubscribesDirectionHandler()
        {
            var q=new TargetRegistry(); var victim=new FakeHealth(); q.Register(1,Vector3.forward*3,TargetKind.Ground,victim);
            var d=Definition(Asset<AttackModuleDefinition>(),Asset<DamageProviderDefinition>());
            var b=Create(d,new BuildingServices(q)); b.Tick(0); Assert.That(victim.Current,Is.EqualTo(80));
            var attack=b.Module<AttackModule>(); Assert.That(attack.CurrentTarget,Is.Not.Null);
            b.ConfirmDirection(Vector3.right); Assert.That(attack.CurrentTarget,Is.Null);
            // Dispose detaches this module's subscription without depending on building disposal.
            attack.Dispose(); b.ConfirmDirection(Vector3.forward);
        }
        [Test] public void UpgradesUseIndependentLevelPolicyAndHealthReceiver()
        {
            var policy=new LevelPolicy { LevelCap=1 };
            var d=Definition(Asset<UpgradeModuleDefinition>(),Asset<FakeHealthDefinition>());
            var b=Create(d,new BuildingServices(null,levelLimit:policy));
            var upgrade=b.Module<IUpgradeControl>(); Assert.That(upgrade.TryUpgrade(out _),Is.False);
            policy.LevelCap=2; Assert.That(upgrade.TryUpgrade(out _),Is.True);
            Assert.That(b.Module<FakeHealth>().Multiplier,Is.EqualTo(1.2f).Within(0.001));
            Assert.That(b.Module<NexusModule>(),Is.Null); Assert.That(b.Module<HealthModule>(),Is.Null);
        }
        [Test] public void NexusAcceptsReplacementDamageCapabilityAtValidationAndRuntime()
        {
            var d=Definition(Asset<NexusModuleDefinition>(),Asset<FakeHealthDefinition>()); d.Recoverable=false;
            Assert.That(d.Validate(),Is.Empty);
            var state=new NexusState(); var b=Create(d,new BuildingServices(null,state));
            Assert.That(state.HasNexus,Is.True); Assert.That(b.Module<HealthModule>(),Is.Null);
            b.Destroy(); Assert.That(state.Defeated,Is.True);
        }
        sealed class LevelPolicy:ILevelLimit
        { public int LevelCap { get; set; } public bool IsExempt(BuildingInstance b)=>false; }
        public sealed class DamageProviderDefinition:BuildingModuleDefinition
        { public override IBuildingModule CreateRuntime()=>new DamageProvider(); }
        sealed class DamageProvider:IBuildingModule,IDamageModifier
        {
            public float DamageMultiplier=>2;
            public void Initialize(BuildingInstance b,BuildingServices s) { } public void Activate() { } public void Tick(float dt) { } public void Dispose() { }
        }
        public sealed class FakeHealthDefinition:BuildingModuleDefinition
        {
            public override bool Provides(Type capability)=>capability==typeof(IDamageReceiver);
            public override IBuildingModule CreateRuntime()=>new FakeHealth();
        }
        sealed class FakeHealth:IBuildingModule,IDamageReceiver,IHealthScaling
        {
            public float Current=100; public float Multiplier=1;
            public bool Alive=>Current>0; public void ApplyDamage(float amount)=>Current-=amount;
            public void SetMaximumMultiplier(float value)=>Multiplier=value;
            public void Initialize(BuildingInstance b,BuildingServices s) { } public void Activate() { } public void Tick(float dt) { } public void Dispose() { }
        }
    }
}
