using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    [CreateAssetMenu(menuName = "Eternal Steam/Effects/Status")]
    public sealed class StatusEffectDefinition : HitEffectDefinition
    {
        public StatusKind Kind;
        [Range(0,1)] public float Strength=0.5f;
        public float Duration=2;
        public override IHitEffect CreateRuntime() => new Runtime(Kind,Strength,Duration);
        public override void Validate(List<string> errors)
        { if (!Enum.IsDefined(typeof(StatusKind),Kind) || !float.IsFinite(Strength) || Strength<=0 || Strength>1 || !float.IsFinite(Duration) || Duration<=0) errors.Add("Invalid status effect settings."); }
        sealed class Runtime : IHitEffect
        {
            readonly StatusKind kind; readonly float strength,duration;
            public Runtime(StatusKind kind,float strength,float duration) { this.kind=kind; this.strength=strength; this.duration=duration; }
            public void Apply(TargetInfo target) { if(target.Receiver is IStatusReceiver receiver) receiver.ApplyStatus(kind,strength,duration); }
        }
    }
}
