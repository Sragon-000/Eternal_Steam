using System;
using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam
{
    public abstract class AttackExecutionDefinition : ScriptableObject
    {
        public abstract IAttackExecution CreateRuntime();
        public virtual void Validate(List<string> errors) { }
    }
    public readonly struct AttackContext
    {
        public readonly BuildingInstance Owner;
        public readonly ITargetQuery Query;
        public readonly TargetKind Kinds;
        public readonly float Range, Angle;
        public readonly IHitEffect[] Effects;
        public AttackContext(BuildingInstance owner, ITargetQuery query, TargetKind kinds, float range, float angle, IHitEffect[] effects)
        { Owner = owner; Query = query; Kinds = kinds; Range = range; Angle = angle; Effects = effects; }
        public void Hit(TargetInfo target, float damage)
        {
            if (!target.Receiver.Alive) return;
            target.Receiver.ApplyDamage(damage);
            foreach (var effect in Effects) if (target.Receiver.Alive) effect.Apply(target);
            Owner.ReportShot(target.Position);
        }
    }
    public interface IAttackExecution : IDisposable
    {
        void Execute(AttackContext context, TargetInfo target, float damage);
        void Tick(float deltaTime);
    }
    public abstract class ImmediateExecution : IAttackExecution
    {
        public abstract void Execute(AttackContext context, TargetInfo target, float damage);
        public void Tick(float deltaTime) { }
        public void Dispose() { }
    }
    public sealed class InstantDamage : ImmediateExecution
    { public override void Execute(AttackContext context, TargetInfo target, float damage) => context.Hit(target, damage); }
}
