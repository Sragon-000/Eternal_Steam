using System;
using System.Linq;
using System.Reflection;
using EternalSteam.Demo;
using NUnit.Framework;
using UnityEngine;

public sealed class LegacyCombatTests
{
    HordeEnemyWorld world;
    HordeAttackResolver attacks;
    HordeEnemyWorld.Enemy[] data;
    [SetUp] public void SetUp()
    {
        world = new HordeEnemyWorld(); attacks = new HordeAttackResolver(world);
        data = (HordeEnemyWorld.Enemy[])typeof(HordeEnemyWorld).GetField("enemies", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(world);
    }
    void Add(Vector3 position, int health = 30)
    {
        int index = world.Spawned;
        world.Spawn(HordeMapKind.Lane);
        data[index].position = position; data[index].destination = position + Vector3.right * 100;
        data[index].health = health;
    }
    [Test] public void ReusedSlotStartsWithFreshHealthAndSlowState()
    {
        Add(Vector3.zero); data[0].slowTime = 2;
        world.ApplyDamage(0, 30); world.ApplyDamage(0, 30);
        Assert.That(world.Killed, Is.EqualTo(1));
        world.Spawn(HordeMapKind.Lane);
        Assert.That(world.GetEnemy(0).alive, Is.True);
        Assert.That(world.GetEnemy(0).health, Is.EqualTo(20));
        Assert.That(world.GetEnemy(0).slowTime, Is.Zero);
        Assert.That(world.FreeCount, Is.EqualTo(3999));
    }
    [Test] public void CapacityIsIndependentOfStageLimits()
    {
        for(int i=0;i<4001;i++) world.Spawn(HordeMapKind.Lane);
        Assert.That(world.Alive, Is.EqualTo(4000));
        Assert.That(world.Spawned, Is.EqualTo(4000));
        Assert.That(world.FreeCount, Is.Zero);
    }
    [TestCase(HordeMapKind.Lane)]
    [TestCase(HordeMapKind.WideFront)]
    [TestCase(HordeMapKind.Pincer)]
    public void ResetReplaysSeedAndClearsIndex(HordeMapKind map)
    {
        world.Spawn(map); var initial = world.GetEnemy(0);
        world.MoveAndIndex(0.1f); world.Reset();
        Assert.That(attacks.FindTarget(Vector3.zero, Vector3.right, 100), Is.EqualTo(-1));
        world.Spawn(map);
        Assert.That(world.GetEnemy(0).position, Is.EqualTo(initial.position));
        Assert.That(world.GetEnemy(0).destination, Is.EqualTo(initial.destination));
        Assert.That(world.GetEnemy(0).speed, Is.EqualTo(initial.speed));
        Assert.That(world.Health, Is.EqualTo(1000));
    }
    [Test] public void EscapeStopsMovementImmediatelyAtDefeat()
    {
        int events=0; world.Defeat += () => events++;
        for(int i=0;i<1001;i++) { Add(Vector3.zero); data[i].destination=Vector3.zero; }
        world.MoveAndIndex(1f);
        Assert.That(world.Escaped, Is.EqualTo(1000));
        Assert.That(world.Alive, Is.EqualTo(1));
        Assert.That(world.Health, Is.Zero);
        Assert.That(events, Is.EqualTo(1));
    }
    [Test] public void PiercingHitsEveryForwardTargetOnceIncludingRangeBoundary()
    {
        Add(new Vector3(3,0,0)); Add(new Vector3(12,0,0)); Add(new Vector3(-3,0,0)); Add(new Vector3(5,0,1));
        attacks.ApplyPiercingShot(Vector3.zero, Vector3.right, 12);
        Assert.That(data.Select(e=>e.health).Take(4), Is.EqualTo(new[]{10,10,30,30}));
    }
    [Test] public void RayUsesNearestAlongAndPreservesExclusiveFarBoundary()
    {
        Add(new Vector3(12,0,0)); Add(new Vector3(4,0,0)); Add(new Vector3(1,0,0));
        world.MoveAndIndex(0);
        Assert.That(attacks.FindTarget(Vector3.zero,Vector3.right,12), Is.EqualTo(1));
        world.Remove(1,true);
        Assert.That(attacks.FindTarget(Vector3.zero,Vector3.right,12), Is.EqualTo(-1));
    }
    [Test] public void AreaIncludesRadiusBoundaryAndSkipsAlreadyDeadSlots()
    {
        Add(new Vector3(3,0,0),20); Add(new Vector3(3.01f,0,0),20);
        world.MoveAndIndex(0); attacks.ApplyArea(Vector3.zero,3); attacks.ApplyArea(Vector3.zero,3);
        Assert.That(world.Killed, Is.EqualTo(1)); Assert.That(data[1].health, Is.EqualTo(20));
    }
    [Test] public void FrostFiltersSectorAndIntegratesPartialSlowExpiry()
    {
        Add(new Vector3(2,0,0)); Add(new Vector3(-2,0,0)); Add(new Vector3(2,0,3));
        world.MoveAndIndex(0); attacks.ApplyFrostCone(Vector3.zero,Vector3.right,10,30);
        Assert.That(data[0].slowTime, Is.EqualTo(2.5f));
        Assert.That(data[1].slowTime, Is.Zero); Assert.That(data[2].slowTime, Is.Zero);
        data[0].slowTime=.05f; data[0].speed=10;
        world.MoveAndIndex(.1f);
        Assert.That(data[0].position.x, Is.EqualTo(2.7f).Within(.0001f));
        Assert.That(data[0].slowTime, Is.Zero);
    }
    [Test] public void WorldsAndDamageRemainIndependent()
    {
        Add(Vector3.zero); var other = new HordeEnemyWorld(); other.Spawn(HordeMapKind.Lane);
        world.ApplyDamage(0,100);
        Assert.That(other.Alive, Is.EqualTo(1)); Assert.That(other.GetEnemy(0).health, Is.EqualTo(20));
    }
    [Test] public void CustomSpawnFollowsTerrainAndCanEscapeWithoutDefeat()
    {
        var sandbox = new HordeEnemyWorld(p => 3 + p.x * .1f, false);
        Assert.That(sandbox.TrySpawn(Vector3.zero, Vector3.right, 2), Is.True);
        Assert.That(sandbox.GetEnemy(0).position.y, Is.EqualTo(3));
        sandbox.MoveAndIndex(.1f);
        Assert.That(sandbox.GetEnemy(0).position.y, Is.EqualTo(3 + sandbox.GetEnemy(0).position.x * .1f).Within(.0001));
        sandbox.Reset();
        for(int i=0;i<1010;i++) { sandbox.TrySpawn(Vector3.zero,Vector3.zero); sandbox.MoveAndIndex(0); }
        Assert.That(sandbox.Escaped, Is.EqualTo(1010));
        Assert.That(sandbox.Health, Is.EqualTo(1000));
    }
    [Test] public void CustomSpawnRejectsInvalidValuesWithoutConsumingSlots()
    {
        Assert.That(world.TrySpawn(new Vector3(float.NaN,0,0),Vector3.zero), Is.False);
        Assert.That(world.TrySpawn(Vector3.zero,Vector3.zero,-1), Is.False);
        Assert.That(world.TrySpawn(Vector3.zero,Vector3.zero,1,0), Is.False);
        Assert.That(world.Alive, Is.Zero);Assert.That(world.FreeCount, Is.EqualTo(4000));
    }
    [Test] public void CombatAssemblyDoesNotReferenceSimulationOrPresentation()
    {
        var names=typeof(HordeEnemyWorld).Assembly.GetReferencedAssemblies().Select(x=>x.Name);
        Assert.That(names, Does.Not.Contain("EternalSteam.LegacyDemo"));
        Assert.That(names, Does.Not.Contain("EternalSteam.LegacyBuildings"));
        Assert.That(names, Does.Not.Contain("EternalSteam.LegacyPresentation"));
        Assert.That(names, Does.Not.Contain("Unity.InputSystem"));
    }
}
