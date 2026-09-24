using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam.Demo
{
    public sealed class HordeTowerCombat
    {
        readonly List<HordeTower> towers;
        readonly bool elevatedEffects,autoTarget;
        readonly List<int> candidates=new();
        readonly HordeEnemyWorld world;
        readonly HordeAttackResolver attacks;
        public HordeTowerCombat(List<HordeTower> towers, HordeEnemyWorld world, HordeAttackResolver attacks, bool elevatedEffects = false,bool autoTarget = false)
        { this.autoTarget=autoTarget;if(autoTarget)candidates.Capacity=world.MaxCount;this.elevatedEffects = elevatedEffects; this.towers = towers; this.world = world; this.attacks = attacks; }
        public void Reset()
        {
            foreach (var tower in towers)
            {
                tower.cooldown = 0; tower.shot = 0;tower.targetId=-1;
                HordeTowerEffects.Reset(tower);
            }
        }
        bool Target(HordeTower tower,out int id)
        {
            bool Valid(int i){if(i<0||i>=world.MaxCount||!world.GetEnemy(i).alive)return false;if(tower.kind==HordeTowerKind.Frost&&world.GetEnemy(i).slowTime>0)return false;var d=world.GetEnemy(i).position-tower.root.transform.position;d.y=0;return d.sqrMagnitude<=tower.range*tower.range;}
            id=tower.targetId;if(Valid(id)&&tower.targetGeneration==world.Generation(id))return true;
            id=-1;float best=float.PositiveInfinity;world.QueryIndices(tower.root.transform.position,tower.range,candidates);
            foreach(int i in candidates){if(!Valid(i))continue;var d=world.GetEnemy(i).position-tower.root.transform.position;d.y=0;float sq=d.sqrMagnitude;if(sq>best||(sq==best&&id>=0&&i>=id))continue;id=i;best=sq;}
            tower.targetId=id;if(id>=0)tower.targetGeneration=world.Generation(id);return id>=0;
        }
        void AutoAttack(HordeTower tower,float dt)
        {
            tower.cooldown=Mathf.Max(0,tower.cooldown-dt);if(!Target(tower,out int target))return;
            var direction=world.GetEnemy(target).position-tower.root.transform.position;direction.y=0;if(direction.sqrMagnitude<.000001f)direction=tower.head.forward;else direction.Normalize();bool aligned=true;
            if(tower.building?.Module<ITurretRotation>() is ITurretRotation rotation){aligned=rotation.AimAt(world.GetEnemy(target).position,dt);direction=rotation.Direction;}
            tower.head.rotation=Quaternion.LookRotation(direction);
            if(!aligned)return;
            if(tower.cooldown>0)return;tower.cooldown=HordeTowerStats.Interval(tower.kind);tower.shot++;HordeTowerEffects.Shot(tower,direction);
            if(tower.kind==HordeTowerKind.Frost){attacks.ApplyFrostCone(tower.root.transform.position,direction,tower.range,tower.halfAngle);HordeTowerEffects.Frost(tower);}
            else if(tower.kind==HordeTowerKind.Arrow)attacks.ApplyPiercingShot(tower.root.transform.position,direction,tower.range,0);
            else if(tower.kind==HordeTowerKind.MachineGun)world.ApplyDamage(target,HordeTowerStats.Damage(tower.kind));
            else{var center=world.GetEnemy(target).position;attacks.ApplyArea(center,3);HordeTowerEffects.Area(tower,center,3,elevatedEffects?tower.root.transform.position.y:0);}
        }
        public void Update(float dt, bool waveStarted, int stageEnemyTotal)
        {
            foreach (HordeTower tower in towers)
            {
                HordeTowerEffects.Tick(tower, dt, waveStarted, world.Defeated);
                if (!waveStarted || (world.Defeated || world.Killed + world.Escaped == stageEnemyTotal && world.Alive == 0)) continue;
                if(autoTarget){if(tower.building==null||(tower.building.Operational&&CombatPermission.Allows(tower.building)))AutoAttack(tower,dt);continue;}
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
