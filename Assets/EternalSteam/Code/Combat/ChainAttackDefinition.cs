using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    [CreateAssetMenu(menuName = "Eternal Steam/Attacks/Chain")]
    public sealed class ChainAttackDefinition : AttackExecutionDefinition
    {
        public float JumpRange = 4;
        public int MaximumHits = 3;
        [Range(0,1)] public float DamageRetention = 0.8f;
        public override IAttackExecution CreateRuntime() => new Runtime(JumpRange,MaximumHits,DamageRetention);
        public override void Validate(List<string> errors)
        { if (!float.IsFinite(JumpRange) || JumpRange <= 0 || MaximumHits < 1 || !float.IsFinite(DamageRetention) || DamageRetention < 0 || DamageRetention > 1) errors.Add("Invalid chain settings."); }
        sealed class Runtime : ImmediateExecution
        {
            readonly float range, retention; readonly int maximum;
            readonly HashSet<TargetHandle> visited = new(); readonly List<TargetInfo> candidates = new();
            public Runtime(float range,int maximum,float retention) { this.range=range; this.maximum=maximum; this.retention=retention; }
            public override void Execute(AttackContext context, TargetInfo target, float damage)
            {
                visited.Clear();
                for (int i=0; i<maximum; i++)
                {
                    visited.Add(target.Handle); context.Hit(target,damage); damage *= retention;
                    context.Query.Query(target.Position,range,candidates);
                    bool found=false; float nearest=float.PositiveInfinity; TargetInfo next=default;
                    foreach (var item in candidates)
                    {
                        if (visited.Contains(item.Handle) || !Sector.Contains(context.Owner.Position,context.Owner.Direction,context.Range,context.Angle,context.Kinds,item)) continue;
                        var delta=item.Position-target.Position; delta.y=0; float distance=delta.sqrMagnitude;
                        if (distance>nearest || (distance==nearest && found && item.Handle.Id>=next.Handle.Id)) continue;
                        found=true; nearest=distance; next=item;
                    }
                    if (!found) break;
                    target=next;
                }
            }
        }
    }
}
