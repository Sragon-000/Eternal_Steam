using System;
using System.Collections.Generic;
using System.Linq;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;
using NUnit.Framework;
using UnityEngine;
namespace EternalSteam.Tests
{
    public sealed class TrainDefenseTests
    {
        readonly List<UnityEngine.Object> assets=new();BuildingDefinition definition;TrainArmament state;TargetRegistry targets;Vector3 position;
        sealed class Receiver:IDamageReceiver{public bool Alive=>true;public float Damage;public void ApplyDamage(float n)=>Damage+=n;}
        T Asset<T>() where T:ScriptableObject{var a=ScriptableObject.CreateInstance<T>();assets.Add(a);return a;}
        [SetUp] public void Setup(){definition=Asset<BuildingDefinition>();definition.Id="test.weapon";definition.Category=BuildingCategory.Defense;definition.Footprint=new Vector2Int(2,2);var weapon=Asset<WeaponModuleDefinition>();weapon.BaseDamage=10;weapon.Range=8;weapon.Interval=.1f;var power=Asset<PowerModuleDefinition>();power.Role=PowerRole.Consumer;power.Rate=5;definition.Modules.AddRange(new BuildingModuleDefinition[]{weapon,Asset<PerformanceUpgradeDefinition>(),Asset<HealthModuleDefinition>(),power});state=new TrainArmament{definition=definition.Id,battery=10};targets=new TargetRegistry();position=Vector3.zero;}
        [TearDown] public void Cleanup(){foreach(var a in assets)UnityEngine.Object.DestroyImmediate(a);assets.Clear();}
        MobileDefenseRuntime Runtime()=>new(state,definition,targets,()=>position);
        [Test] public void MovingWeaponQueriesCurrentPositionAndNeverRegistersAsEnemyTarget(){var near=new Receiver();var far=new Receiver();targets.Register(1,new Vector3(1,0,0),TargetKind.Ground,near);targets.Register(2,new Vector3(101,0,0),TargetKind.Ground,far);using var m=Runtime();m.Tick(.1f,true);Assert.That(near.Damage,Is.GreaterThan(0));float previous=near.Damage;position=new Vector3(100,0,0);m.Tick(.2f,true);Assert.That(far.Damage,Is.GreaterThan(0));Assert.That(near.Damage,Is.EqualTo(previous));var registry=new BuildingTargetRegistry();registry.Register(m.Building,2,Quaternion.identity);Assert.That(registry.Count,Is.Zero);Assert.That(m.Building.Module<IDamageReceiver>(),Is.Null);Assert.That(m.Building.Module<PowerModule>(),Is.Null);}
        [Test] public void DepletedBatteryStopsNewAttacksAndPausedCombatSpendsNothing(){var enemy=new Receiver();targets.Register(1,Vector3.forward,TargetKind.Ground,enemy);state.battery=.5;using var m=Runtime();m.Tick(1,false);Assert.That(state.battery,Is.EqualTo(.5));m.Tick(1,true);Assert.That(state.battery,Is.Zero);float damage=enemy.Damage;m.Tick(1,true);Assert.That(enemy.Damage,Is.EqualTo(damage));}
        [Test] public void ProjectileCompletesAfterBatteryDepletion(){definition.Modules.OfType<WeaponModuleDefinition>().Single().Delivery=WeaponDelivery.Projectile;state.battery=.05;var enemy=new Receiver();targets.Register(1,Vector3.forward*4,TargetKind.Ground,enemy);using var m=Runtime();m.Tick(.01f,true);Assert.That(m.Pending,Is.True);m.Tick(1,true);Assert.That(enemy.Damage,Is.GreaterThan(0));Assert.That(m.Pending,Is.False);}
        [TestCase(9f,TargetKind.Ground)][TestCase(1f,TargetKind.Air)]
        public void StandbyIgnoresOutOfRangeAndWrongKind(float distance,TargetKind kind)
        {
            definition.Modules.OfType<WeaponModuleDefinition>().Single().Targets=TargetKind.Ground;
            var enemy=new Receiver();targets.Register(1,Vector3.forward*distance,kind,enemy);
            using var m=Runtime();m.Tick(60,true);
            Assert.That(state.battery,Is.EqualTo(10));Assert.That(enemy.Damage,Is.Zero);
        }
        [Test] public void StandbyPreservesChargeThenPaysForEngagementAndHonorsMovement()
        {
            using var m=Runtime();m.Tick(60,true);Assert.That(state.battery,Is.EqualTo(10));
            var enemy=new Receiver();var handle=targets.Register(1,Vector3.forward,TargetKind.Ground,enemy);
            m.Tick(.1f,true);Assert.That(state.battery,Is.EqualTo(9.5).Within(.00001));Assert.That(enemy.Damage,Is.GreaterThan(0));
            float damage=enemy.Damage;double charge=state.battery;targets.Move(handle,Vector3.forward*100);m.Tick(60,true);
            Assert.That(state.battery,Is.EqualTo(charge));Assert.That(enemy.Damage,Is.EqualTo(damage));
        }
        [Test] public void PendingProjectileCompletesDuringStandbyWithoutExtraCharge()
        {
            var weapon=definition.Modules.OfType<WeaponModuleDefinition>().Single();weapon.Delivery=WeaponDelivery.Projectile;weapon.ProjectileSpeed=100;
            var enemy=new Receiver();var handle=targets.Register(1,Vector3.forward*4,TargetKind.Ground,enemy);
            using var m=Runtime();m.Tick(.01f,true);Assert.That(m.Pending,Is.True);double charge=state.battery;
            targets.Move(handle,Vector3.forward*12);m.Tick(1,true);
            Assert.That(enemy.Damage,Is.GreaterThan(0));Assert.That(m.Pending,Is.False);Assert.That(state.battery,Is.EqualTo(charge));
        }
        sealed class LevelLimit:ILevelLimit {public int LevelCap{get;set;}=1;public bool IsExempt(BuildingInstance b)=>false;}
        [Test] public void MountedModuleHonorsInjectedMainLevelEvenWhenCalledDirectly()
        {
            var limit=new LevelLimit();using var m=new MobileDefenseRuntime(state,definition,targets,()=>position,limit);
            var u=m.Building.Module<IUpgradeControl>();Assert.That(u.TryUpgrade(out _),Is.False);
            limit.LevelCap=2;Assert.That(u.TryUpgrade(out _),Is.True);Assert.That(u.TryUpgrade(out _),Is.False);
            m.Capture();Assert.That(u.Level,Is.EqualTo(2));
        }
        [Test] public void CaptureRestoresBatteryLevelAndCooldown(){using(var m=Runtime()){Assert.That(m.Building.Module<IUpgradeControl>().TryUpgrade(out _),Is.True);m.Tick(.2f,true);m.Capture();}var encoded=JsonUtility.ToJson(state);state=JsonUtility.FromJson<TrainArmament>(encoded);using var restored=Runtime();Assert.That(restored.Building.Module<IUpgradeControl>().Level,Is.EqualTo(2));restored.Capture();Assert.That(JsonUtility.ToJson(state),Is.EqualTo(encoded));}
        [TestCase(1,1)][TestCase(3,3)][TestCase(3,1)] public void OnlyExactMountDimensionsAreAccepted(int x,int y){Assert.That(MobileDefenseRuntime.Compatible(definition,new Vector2Int(x,y)),Is.False);}
        [Test] public void AllThreeMountShapesAcceptedWithoutRotationShortcut(){foreach(var shape in new[]{new Vector2Int(2,2),new Vector2Int(1,3),new Vector2Int(3,1)}){definition.Footprint=shape;state.footprint=shape;TrainArmament.Validate(state);Assert.That(MobileDefenseRuntime.Compatible(definition,shape),Is.True);}Assert.That(MobileDefenseRuntime.Compatible(definition,new Vector2Int(1,3)),Is.False);}
        [TestCase(double.NaN)][TestCase(-1)][TestCase(201)] public void InvalidBatteryRejected(double n){state.battery=n;Assert.Throws<ArgumentException>(()=>TrainArmament.Validate(state));}
        [Test] public void OlderV2MissingArmamentDefaultsToEmptyMount(){var t=JsonUtility.FromJson<TrainState>("{\"status\":0}");TrainArmament.Validate(t.armament);if(t.armament!=null)Assert.That(t.armament.footprint,Is.EqualTo(new Vector2Int(2,2)));}
        [Test] public void PowerTransferConservesStoredEnergyAndRejectsInvalidRequests(){var def=Asset<BuildingDefinition>();var module=Asset<PowerModuleDefinition>();module.Role=PowerRole.Storage;def.Modules.Add(module);using var b=new BuildingInstance(1,def,default,default,new BuildingServices(null));b.Activate();var power=b.Module<PowerModule>();var saved=SavedState.Capture(power);saved.values.Single(v=>v.name=="Stored").text="30";saved.Restore(power);Assert.That(power.WithdrawStored(double.NaN),Is.Zero);Assert.That(power.WithdrawStored(50),Is.EqualTo(30));Assert.That(power.Stored,Is.Zero);Assert.That(power.WithdrawStored(10),Is.Zero);}
    }
}
