using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EternalSteam.Demo;
namespace EternalSteam.Tests {
 public sealed class ActiveEnemyIterationTests {
  static List<int> Ids(HordeEnemyWorld w){var result=new List<int>();foreach(int id in w.ActiveIndices)result.Add(id);return result;}
  [TestCase(1)][TestCase(65)][TestCase(130)]public void SparseSlotsRecycleInStableAscendingOrder(int capacity){var w=new HordeEnemyWorld(capacity:capacity);for(int i=0;i<capacity;i++)Assert.That(w.TrySpawn(Vector3.zero,Vector3.forward),Is.True);for(int i=0;i<capacity;i++)if(i%3!=0)w.Remove(i,true);var expected=new List<int>();for(int i=0;i<capacity;i++)if(i%3==0)expected.Add(i);Assert.That(Ids(w),Is.EqualTo(expected));while(w.FreeCount>0)w.TrySpawn(Vector3.zero,Vector3.forward);Assert.That(Ids(w).Count,Is.EqualTo(capacity));for(int i=0;i<capacity;i++)Assert.That(Ids(w)[i],Is.EqualTo(i));w.DespawnAll();Assert.That(Ids(w),Is.Empty);w.TrySpawn(Vector3.zero,Vector3.forward);Assert.That(Ids(w),Is.EqualTo(new[]{0}));}
  [Test]public void RemovalDuringIterationVisitsEveryOriginalLivingSlotOnce(){var w=new HordeEnemyWorld(capacity:130);for(int i=0;i<130;i++)w.TrySpawn(Vector3.zero,Vector3.forward);int visits=0;foreach(int id in w.ActiveIndices){Assert.That(id,Is.EqualTo(visits++));w.Remove(id,true);}Assert.That(visits,Is.EqualTo(130));Assert.That(w.Alive,Is.Zero);Assert.That(w.Killed,Is.EqualTo(130));var empty=w.ActiveIndices.GetEnumerator();Assert.That(empty.MoveNext(),Is.False);Assert.That(empty.MoveNext(),Is.False);}
  [Test]public void ArrivalRemovalAndIndexRebuildLeaveNoGhostTargets(){var w=new HordeEnemyWorld(damageOnEscape:false,capacity:130);for(int i=0;i<130;i++)w.TrySpawn(Vector3.zero,Vector3.zero);w.MoveAndIndex(.1f);Assert.That(w.Alive,Is.Zero);var hits=new List<int>();w.QueryIndices(Vector3.zero,10,hits);Assert.That(hits,Is.Empty);w.TrySpawn(Vector3.zero,Vector3.forward,0);w.MoveAndIndex(.1f);w.QueryIndices(Vector3.zero,10,hits);Assert.That(hits.Count,Is.EqualTo(1));Assert.That(hits[0],Is.EqualTo(129));}
  [Test]public void RemovedFutureSlotAndRespawnRespectForwardIterationOrder(){var w=new HordeEnemyWorld(capacity:4);for(int i=0;i<4;i++)w.TrySpawn(Vector3.zero,Vector3.forward);var visited=new List<int>();foreach(int id in w.ActiveIndices){visited.Add(id);if(id==0){w.Remove(2,true);w.Remove(0,true);w.TrySpawn(Vector3.zero,Vector3.forward);}}Assert.That(visited,Is.EqualTo(new[]{0,1,3}));Assert.That(Ids(w),Is.EqualTo(new[]{0,1,3}));}
 }
}
