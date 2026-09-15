using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam.Demo
{
    public sealed class HordeTowerCombat
    {
        readonly List<HordeTower> towers;
        readonly bool elevatedEffects;
        readonly HordeEnemyWorld world;
        readonly HordeAttackResolver attacks;
        public HordeTowerCombat(List<HordeTower> towers, HordeEnemyWorld world, HordeAttackResolver attacks, bool elevatedEffects = false)
        { this.elevatedEffects = elevatedEffects; this.towers = towers; this.world = world; this.attacks = attacks; }
        public void Reset()
        {
            foreach (var tower in towers)
            {
                tower.cooldown = 0; tower.shot = 0;
                HordeTowerEffects.Reset(tower);
            }
        }
        public void Update(float dt, bool waveStarted, int stageEnemyTotal)
        {
            foreach (HordeTower tower in towers)
            {
                HordeTowerEffects.Tick(tower, dt, waveStarted, world.Defeated);
                if (!waveStarted || (world.Defeated || world.Killed + world.Escaped == stageEnemyTotal && world.Alive == 0)) continue;
                tower.cooldown -= dt;
                if (tower.cooldown > 0) continue;
                tower.cooldown = HordeTowerStats.Interval(tower.kind);
                tower.shot++;
                float fraction = Mathf.Repeat(tower.shot * 0.6180339f, 1f);
                float angle = Mathf.Lerp(-tower.halfAngle, tower.halfAngle, fraction);
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * tower.head.forward;
                HordeTowerEffects.Shot(tower, direction);
                if (tower.kind == HordeTowerKind.Frost)
                {
                    attacks.ApplyFrostCone(tower.root.transform.position, tower.head.forward, tower.range, tower.halfAngle);
                    HordeTowerEffects.Frost(tower);
                    continue;
                }
                if (tower.kind == HordeTowerKind.Arrow)
                {
                    attacks.ApplyPiercingShot(tower.root.transform.position, direction, tower.range);
                    continue;
                }
                int target = attacks.FindTarget(tower.root.transform.position, direction, tower.range);
                if (tower.kind == HordeTowerKind.MachineGun)
                {
                    if (target >= 0) world.ApplyDamage(target, HordeTowerStats.Damage(tower.kind));
                }
                else
                {
                    Vector3 center = target >= 0 ? world.GetEnemy(target).position : tower.root.transform.position + direction * tower.range;
                    float radius = 3;
                    attacks.ApplyArea(center, radius);
                    HordeTowerEffects.Area(tower, center, radius, elevatedEffects ? tower.root.transform.position.y : 0);
                }
            }
        }
    }
}
