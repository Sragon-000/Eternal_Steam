using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EternalSteam.Demo;

namespace EternalSteam.Tests
{
    public sealed class MapClearEnemyTests
    {
        [Test] public void ClearDespawnsWithoutRewardsOrEscapeDamageAndSlotsCanBeReused()
        {
            var world=new HordeEnemyWorld(capacity:4);int kills=0,defeats=0;
            world.KilledEnemy+=(id,generation)=>kills++;world.Defeat+=()=>defeats++;
            Assert.That(world.TrySpawn(Vector3.zero,Vector3.forward),Is.True);
            world.ApplyDamage(0,100);
            Assert.That(world.TrySpawn(Vector3.zero,Vector3.forward),Is.True);
            Assert.That(world.TrySpawn(Vector3.right,Vector3.forward),Is.True);
            var oldGeneration=world.Generation(0);world.MoveAndIndex(0);
            world.DespawnAll();world.DespawnAll();
            Assert.That(world.Alive,Is.Zero);Assert.That(world.FreeCount,Is.EqualTo(4));
            Assert.That(world.Killed,Is.EqualTo(1));Assert.That(world.Spawned,Is.EqualTo(3));
            Assert.That(world.Escaped,Is.Zero);Assert.That(world.Health,Is.EqualTo(HordeEnemyWorld.MaxHealth));
            Assert.That(kills,Is.EqualTo(1));Assert.That(defeats,Is.Zero);
            var hits=new List<int>();world.QueryIndices(Vector3.zero,10,hits);Assert.That(hits,Is.Empty);
            for(int i=0;i<4;i++)Assert.That(world.TrySpawn(Vector3.zero,Vector3.forward),Is.True);
            Assert.That(world.Generation(0),Is.GreaterThan(oldGeneration));
            Assert.That(world.Alive,Is.EqualTo(4));Assert.That(world.TrySpawn(Vector3.zero,Vector3.forward),Is.False);
        }
    }
}
