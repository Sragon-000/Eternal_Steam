using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    public interface IHitEffect { void Apply(TargetInfo target); }
    public abstract class HitEffectDefinition : ScriptableObject
    {
        public abstract IHitEffect CreateRuntime();
        public virtual void Validate(List<string> errors) { }
    }
    public enum StatusKind { Slow, Stun, ArmorReduction }
    public interface IStatusReceiver { void ApplyStatus(StatusKind kind,float strength,float duration); }
    // Strongest effect wins; each application expires independently. No shared asset state.
    public sealed class StatusState : IStatusReceiver
    {
        struct Entry { public StatusKind Kind; public float Strength,Remaining; }
        readonly List<Entry> entries=new();
        public float Strength(StatusKind kind)
        { float result=0; foreach(var item in entries) if(item.Kind==kind) result=Mathf.Max(result,item.Strength); return result; }
        public float MovementMultiplier => Strength(StatusKind.Stun)>0 ? 0 : 1-Strength(StatusKind.Slow);
        public float ArmorReduction => Strength(StatusKind.ArmorReduction);
        public void ApplyStatus(StatusKind kind,float strength,float duration)
        {
            if (!float.IsFinite(strength) || !float.IsFinite(duration) || strength<=0 || duration<=0) return;
            entries.Add(new Entry { Kind=kind,Strength=Mathf.Clamp01(strength),Remaining=duration });
        }
        public void Tick(float dt)
        { for(int i=entries.Count-1;i>=0;i--) { var e=entries[i]; e.Remaining-=dt; if(e.Remaining<=0) entries.RemoveAt(i); else entries[i]=e; } }
        public void Clear() => entries.Clear();
    }
}
