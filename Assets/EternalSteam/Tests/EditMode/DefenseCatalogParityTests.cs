using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace EternalSteam.Tests
{
    public sealed class DefenseCatalogParityTests
    {
        const string CatalogPath = "Assets/EternalSteam/Content/Buildings/StartLoop/Catalog.asset";

        sealed class Profile
        {
            public readonly string Id;
            public readonly float Damage, Range, Interval;
            public readonly int Size, Unlock, Maximum;
            public readonly TargetKind Targets;
            public readonly WeaponSchedule Schedule;
            public readonly WeaponDelivery Delivery;
            public readonly WeaponStatus Status;
            public Profile(string id, float damage, float range, float interval, int size, int unlock, int maximum,
                TargetKind targets, WeaponSchedule schedule, WeaponDelivery delivery, WeaponStatus status = WeaponStatus.None)
            {
                Id = id; Damage = damage; Range = range; Interval = interval; Size = size;
                Unlock = unlock; Maximum = maximum; Targets = targets; Schedule = schedule;
                Delivery = delivery; Status = status;
            }
        }

        static readonly Profile[] Profiles =
        {
            new("defense.tesla", 80, 15, 4, 1, 1, 10, TargetKind.All, WeaponSchedule.Periodic, WeaponDelivery.Chain),
            new("defense.plasma_laser", 25, 45, .5f, 1, 3, 10, TargetKind.All, WeaponSchedule.Sustained, WeaponDelivery.Instant, WeaponStatus.Overheat),
            new("defense.vulcan_aa", 15, 25, .2f, 1, 5, 10, TargetKind.Air, WeaponSchedule.Burst, WeaponDelivery.Instant),
            new("defense.rail_aa", 250, 100, 5, 1, 7, 10, TargetKind.All, WeaponSchedule.Periodic, WeaponDelivery.Pierce),
            new("defense.arc", 150, 40, 3, 2, 2, 7, TargetKind.All, WeaponSchedule.Periodic, WeaponDelivery.Projectile),
            new("defense.railgun", 300, 60, 5, 2, 4, 7, TargetKind.All, WeaponSchedule.Periodic, WeaponDelivery.Pierce),
            new("defense.sky_plasma", 180, 50, 4, 2, 4, 7, TargetKind.Air, WeaponSchedule.Periodic, WeaponDelivery.Projectile, WeaponStatus.Burn),
            new("defense.smart_missile", 200, 70, 6, 3, 6, 3, TargetKind.All, WeaponSchedule.Periodic, WeaponDelivery.Projectile),
            new("defense.emp", 0, 25, 10, 3, 8, 3, TargetKind.All, WeaponSchedule.Periodic, WeaponDelivery.Pulse, WeaponStatus.Stun)
        };

        sealed class Receiver : IWeaponDamageReceiver
        {
            public bool Alive => true;
            public bool CanAct => true;
            public float Damage;
            public int Effects;
            public void ApplyDamage(float amount) => Damage += amount;
            public void Receive(float damage, WeaponDamageSource source) => Damage += damage;
            public void Inflict(WeaponStatus status, float duration, float strength, float tickDamage) => Effects++;
        }

        static BuildingCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            return catalog;
        }

        static BuildingDefinition Definition(string id) => Catalog().Buildings.Single(d => d.Id == id);

        static IEnumerable<TestCaseData> WeaponCases()
        {
            foreach (var profile in Profiles) yield return new TestCaseData(profile.Id).SetName($"ActiveDefense_{profile.Id}");
        }

        [Test]
        public void ActiveCatalogMatchesNineAuthoredDefenseProfiles()
        {
            var defenses = Catalog().Buildings.Where(d => d.Category == BuildingCategory.Defense).ToArray();
            Assert.That(defenses.Select(d => d.Id), Is.EquivalentTo(Profiles.Select(p => p.Id)));
            foreach (var profile in Profiles)
            {
                var definition = defenses.Single(d => d.Id == profile.Id);
                Assert.That(definition.Validate(), Is.Empty, profile.Id);
                Assert.That(definition.Footprint, Is.EqualTo(new Vector2Int(profile.Size, profile.Size)), profile.Id);
                Assert.That(definition.Placement.RequiredNexusLevel, Is.EqualTo(profile.Unlock), profile.Id);
                var weapon = definition.Modules.OfType<WeaponModuleDefinition>().Single();
                var upgrade = definition.Modules.OfType<PerformanceUpgradeDefinition>().Single();
                Assert.That(weapon.BaseDamage, Is.EqualTo(100), profile.Id);
                Assert.That(weapon.BaseDamage * weapon.DamageCoefficient, Is.EqualTo(profile.Damage).Within(.001), profile.Id);
                Assert.That(weapon.Range, Is.EqualTo(profile.Range).Within(.001), profile.Id);
                Assert.That(weapon.Interval, Is.EqualTo(profile.Interval).Within(.001), profile.Id);
                Assert.That(weapon.Targets, Is.EqualTo(profile.Targets), profile.Id);
                Assert.That(weapon.Schedule, Is.EqualTo(profile.Schedule), profile.Id);
                Assert.That(weapon.Delivery, Is.EqualTo(profile.Delivery), profile.Id);
                Assert.That(weapon.Status, Is.EqualTo(profile.Status), profile.Id);
                Assert.That(upgrade.MaximumLevel, Is.EqualTo(profile.Maximum), profile.Id);
            }
        }

        [TestCaseSource(nameof(WeaponCases))]
        public void ActiveWeaponAttacksAndUpgradeStateRoundTrips(string id)
        {
            var profile = Profiles.Single(p => p.Id == id);
            var source = Definition(id);
            var fixture = ScriptableObject.CreateInstance<BuildingDefinition>();
            fixture.Id = id;
            fixture.DisplayName = id;
            fixture.Footprint = source.Footprint;
            fixture.Modules.Add(source.Modules.OfType<WeaponModuleDefinition>().Single());
            fixture.Modules.Add(source.Modules.OfType<PerformanceUpgradeDefinition>().Single());
            try
            {
                var targets = new TargetRegistry();
                var ground = new Receiver();
                var air = new Receiver();
                targets.Register(1, Vector3.forward * 5, TargetKind.Ground, ground);
                targets.Register(2, Vector3.forward * 6, TargetKind.Air, air);
                using (var building = new BuildingInstance(1, fixture, Vector2Int.zero, Vector3.zero, new BuildingServices(targets)))
                {
                    building.Activate();
                    for (var i = 0; i < 120; i++) building.Tick(.1f);
                    if (profile.Delivery == WeaponDelivery.Pulse)
                    {
                        Assert.That(ground.Effects + air.Effects, Is.GreaterThan(0), id);
                        Assert.That(ground.Damage + air.Damage, Is.Zero, id);
                    }
                    else Assert.That(ground.Damage + air.Damage, Is.GreaterThan(0), id);
                    if (profile.Targets == TargetKind.Air)
                    {
                        Assert.That(ground.Damage, Is.Zero, id);
                        Assert.That(ground.Effects, Is.Zero, id);
                    }

                    var upgrade = building.Module<IUpgradeControl>();
                    while (upgrade.Level < profile.Maximum) Assert.That(upgrade.TryUpgrade(out _), Is.True, id);
                    Assert.That(upgrade.TryUpgrade(out _), Is.False, id);
                    Assert.That(upgrade.Level, Is.EqualTo(profile.Maximum), id);
                    var weapon = building.Module<WeaponRuntime>();
                    var increase = 1 + .1f * (profile.Maximum - 1);
                    var decrease = 1 - .1f * (profile.Maximum - 1);
                    Assert.That(weapon.Damage, Is.EqualTo(profile.Damage * increase).Within(.001), id);
                    Assert.That(weapon.Range, Is.EqualTo(profile.Range * increase).Within(.001), id);
                    Assert.That(weapon.Interval, Is.EqualTo(profile.Interval * decrease).Within(.001), id);

                    var saved = JsonUtility.FromJson<SavedState>(JsonUtility.ToJson(SavedState.Capture(upgrade)));
                    using var restored = new BuildingInstance(2, fixture, Vector2Int.zero, Vector3.zero, new BuildingServices(new TargetRegistry()));
                    restored.Activate();
                    saved.Restore(restored.Module<IUpgradeControl>());
                    Assert.That(restored.Module<IUpgradeControl>().Level, Is.EqualTo(profile.Maximum), id);
                    Assert.That(restored.Module<WeaponRuntime>().Damage, Is.EqualTo(weapon.Damage).Within(.001), id);
                }
            }
            finally { Object.DestroyImmediate(fixture); }
        }
    }
}
