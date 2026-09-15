using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    [CreateAssetMenu(menuName = "Eternal Steam/Attacks/Area")]
    public sealed class AreaAttackDefinition : AttackExecutionDefinition
    {
        public float Radius = 3;
        public override IAttackExecution CreateRuntime() => new Runtime(Radius);
        public override void Validate(List<string> errors)
        { if (!float.IsFinite(Radius) || Radius <= 0) errors.Add("Area radius must be positive and finite."); }
        sealed class Runtime : ImmediateExecution
        {
            readonly float radius;
            readonly List<TargetInfo> candidates = new();
            public Runtime(float radius) { this.radius = radius; }
            public override void Execute(AttackContext context, TargetInfo target, float damage)
            {
                context.Query.Query(target.Position, radius, candidates);
                foreach (var item in candidates)
                    if (Sector.Contains(context.Owner.Position, context.Owner.Direction, context.Range, context.Angle, context.Kinds, item)) context.Hit(item, damage);
            }
        }
    }
}
