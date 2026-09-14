using System;
using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam
{
    [CreateAssetMenu(menuName = "Eternal Steam/Modules/Attack")]
    public sealed class AttackModuleDefinition : BuildingModuleDefinition
    {
        public float Range = 18;
        [Range(0, 360)] public float Angle = 90;
        public float Interval = 0.5f;
        public float Damage = 10;
        public TargetKind Targets = TargetKind.All;
        public override IBuildingModule CreateRuntime() => new AttackModule(Range, Angle, Interval, Damage, Targets, new NearestTargetSelector(), new InstantDamage());
        public override void Validate(List<string> errors)
        {
            if (!float.IsFinite(Range) || Range <= 0 || !float.IsFinite(Angle) || Angle < 0 || Angle > 360
                || !float.IsFinite(Interval) || Interval <= 0 || !float.IsFinite(Damage) || Damage <= 0)
                errors.Add("Attack range/interval/damage must be positive; angle must be 0–360; all must be finite.");
            if (Targets == 0 || (Targets & ~TargetKind.All) != 0) errors.Add("Choose ground, air or both targets.");
        }
    }

    public interface ITargetSelector
    {
        bool Select(ITargetQuery query, Vector3 position, Vector3 direction, float range, float angle, TargetKind kinds, out TargetInfo target);
    }
    public interface IAttackExecution { void Execute(TargetInfo target, float damage); }
    public sealed class InstantDamage : IAttackExecution
    { public void Execute(TargetInfo target, float damage) => target.Receiver.ApplyDamage(damage); }
    public static class Sector
    {
        public static bool Contains(Vector3 origin, Vector3 forward, float range, float angle, TargetKind kinds, TargetInfo target)
        {
            if (target.Receiver == null || !target.Receiver.Alive || (target.Kind & kinds) == 0) return false;
            var delta = target.Position - origin;
            delta.y = 0;
            if (delta.sqrMagnitude > range * range) return false;
            if (delta.sqrMagnitude < 0.000001f || angle >= 360) return true;
            return Vector3.Dot(forward, delta.normalized) + 0.000001f >= Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
        }
    }
    public sealed class NearestTargetSelector : ITargetSelector
    {
        readonly List<TargetInfo> candidates = new();
        public bool Select(ITargetQuery query, Vector3 position, Vector3 direction, float range, float angle, TargetKind kinds, out TargetInfo target)
        {
            query.Query(position, range, candidates);
            target = default;
            float best = float.PositiveInfinity;
            bool found = false;
            foreach (var candidate in candidates)
            {
                if (!Sector.Contains(position, direction, range, angle, kinds, candidate)) continue;
                var delta = candidate.Position - position;
                delta.y = 0;
                float distance = delta.sqrMagnitude;
                if (distance > best || (distance == best && found && candidate.Handle.Id >= target.Handle.Id)) continue;
                best = distance;
                target = candidate;
                found = true;
            }
            return found;
        }
    }
    public sealed class AttackModule : IBuildingModule
    {
        readonly ITargetSelector selector;
        readonly IAttackExecution execution;
        readonly float interval, damage;
        BuildingInstance owner;
        ITargetQuery query;
        float cooldown;
        TargetKind kinds;
        public float Range { get; }
        public float Angle { get; }
        public TargetHandle? CurrentTarget { get; private set; }
        public TargetKind Kinds { get => kinds; set { kinds = value; ClearTarget(); } }
        public AttackModule(float range, float angle, float interval, float damage, TargetKind kinds, ITargetSelector selector, IAttackExecution execution)
        { Range = range; Angle = angle; this.interval = interval; this.damage = damage; this.kinds = kinds; this.selector = selector; this.execution = execution; }
        public void Initialize(BuildingInstance owner, BuildingServices services)
        { this.owner = owner; query = services.Targets ?? throw new InvalidOperationException("Attack requires ITargetQuery."); }
        public void Activate() { }
        public void ClearTarget() => CurrentTarget = null;
        public void Tick(float dt)
        {
            cooldown = Mathf.Max(0, cooldown - dt);
            TargetInfo target = default;
            if (CurrentTarget.HasValue && (!query.TryGet(CurrentTarget.Value, out target)
                || !Sector.Contains(owner.Position, owner.Direction, Range, Angle, kinds, target))) ClearTarget();
            if (!CurrentTarget.HasValue)
            {
                if (!selector.Select(query, owner.Position, owner.Direction, Range, Angle, kinds, out target)) return;
                CurrentTarget = target.Handle;
            }
            if (cooldown > 0) return;
            if (!query.TryGet(CurrentTarget.Value, out target) || !Sector.Contains(owner.Position, owner.Direction, Range, Angle, kinds, target))
            { ClearTarget(); return; }
            cooldown = interval;
            execution.Execute(target, damage);
            owner.ReportShot(target.Position);
        }
        public void Dispose() => ClearTarget();
    }
}
