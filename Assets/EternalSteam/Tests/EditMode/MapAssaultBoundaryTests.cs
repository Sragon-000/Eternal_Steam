using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using EternalSteam.OpenWorld;
using Object=UnityEngine.Object;

namespace EternalSteam.Tests
{
    public sealed class MapAssaultBoundaryTests
    {
        [Test] public void BossAndFixedWaveSpawnAreasMustBeDistinct()
        {
            var settings=ScriptableObject.CreateInstance<MapAssaultSettings>();
            try
            {
                settings.BossPosition=Vector3.zero;
                settings.BossWavePoints=Enumerable.Range(0,8).Select(i=>new Vector3(20+i*10,0,0)).ToArray();
                Assert.DoesNotThrow(settings.ValidateConfiguration);
                settings.BossWavePoints[0]=Vector3.zero;
                Assert.Throws<InvalidOperationException>(settings.ValidateConfiguration,"A normal wave point cannot occupy the boss footprint");
                settings.BossWavePoints[0]=new Vector3(20,0,0);
                settings.BossWavePoints[1]=settings.BossWavePoints[0];
                Assert.Throws<InvalidOperationException>(settings.ValidateConfiguration,"Fixed wave points cannot overlap one another");
            }
            finally{Object.DestroyImmediate(settings);}
        }

        [Test] public void FixedBossWavePointsReplaceUnsafeLocationAndReturnWithoutChangingBudget()
        {
            var planner=new NightSpawnPlanner(5,73);var eligible=new[]{true,true,true,true,true};
            planner.BeginNight(1,50);planner.Reconcile(eligible,4,new[]{0,1,2,3});
            Assert.That(planner.Points.OrderBy(i=>i),Is.EqualTo(new[]{0,1,2,3}));
            eligible[1]=false;planner.Reconcile(eligible,4,new[]{0,1,2,3});
            Assert.That(planner.Points.Contains(4),Is.True);Assert.That(planner.Points.Contains(1),Is.False);
            Assert.That(planner.Pending,Is.EqualTo(50));
            eligible[1]=true;planner.Reconcile(eligible,4,new[]{0,1,2,3});
            Assert.That(planner.Points.OrderBy(i=>i),Is.EqualTo(new[]{0,1,2,3}));
            Assert.That(planner.Pending,Is.EqualTo(50));
        }

        [Test] public void ThresholdUsesExactEnergyAndBossRewardIsPaidOnce()
        {
            var energy=new MapEnergyState("qa05",1000,2,1000);
            Assert.That(energy.Extract(0,899),Is.EqualTo(899));
            energy.Advance(false);Assert.That(energy.Stage,Is.EqualTo(MapStage.Gathering));
            Assert.That(energy.RewardKill(100),Is.EqualTo(1));
            Assert.That(energy.Earned,Is.EqualTo(900));Assert.That(energy.Remaining[0],Is.EqualTo(101));
            energy.Advance(false);Assert.That(energy.Stage,Is.EqualTo(MapStage.WaitingForNight));
            Assert.That(energy.Extract(1,1000),Is.Zero);
            energy.Advance(true);Assert.That(energy.Stage,Is.EqualTo(MapStage.BossBattle));
            Assert.That(energy.DefeatBoss(),Is.True);Assert.That(energy.DefeatBoss(),Is.False);
            Assert.That(energy.Earned,Is.EqualTo(1000));Assert.That(energy.BossReward,Is.EqualTo(100));
            Assert.That(energy.Craft("qa05"),Is.True);Assert.That(energy.Craft("qa05"),Is.False);
            Assert.That(energy.Stage,Is.EqualTo(MapStage.Cleared));
        }
    }
}
