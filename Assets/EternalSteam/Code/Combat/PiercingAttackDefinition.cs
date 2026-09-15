using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    [CreateAssetMenu(menuName = "Eternal Steam/Attacks/Piercing")]
    public sealed class PiercingAttackDefinition : AttackExecutionDefinition
    {
        public float Width = 1;
        public int MaximumHits = 4;
        public override IAttackExecution CreateRuntime() => new Runtime(Width, MaximumHits);
        public override void Validate(List<string> errors)
        { if (!float.IsFinite(Width) || Width <= 0 || MaximumHits < 1) errors.Add("Piercing width/hit count must be positive."); }
        sealed class Runtime : ImmediateExecution
        {
            readonly float width; readonly int maximum;
            readonly List<TargetInfo> candidates = new();
            readonly List<TargetInfo> hits = new();
            public Runtime(float width, int maximum) { this.width = width; this.maximum = maximum; }
            public override void Execute(AttackContext context, TargetInfo target, float damage)
            {
                Vector3 origin = context.Owner.Position, direction = target.Position - origin;
                direction.y = 0; direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : context.Owner.Direction;
                context.Query.Query(origin, context.Range, candidates); hits.Clear();
                foreach (var item in candidates)
                {
                    if (!Sector.Contains(origin, context.Owner.Direction, context.Range, context.Angle, context.Kinds, item)) continue;
                    var delta = item.Position - origin; delta.y = 0;
                    float along = Vector3.Dot(delta, direction);
                    if (along >= 0 && (delta - direction * along).sqrMagnitude <= width * width * 0.25f) hits.Add(item);
                }
                hits.Sort((a,b) => { float da = Vector3.Dot(a.Position-origin,direction), db = Vector3.Dot(b.Position-origin,direction); return da == db ? a.Handle.Id.CompareTo(b.Handle.Id) : da.CompareTo(db); });
                for (int i = 0; i < Mathf.Min(maximum,hits.Count); i++) context.Hit(hits[i],damage);
            }
        }
    }
}
