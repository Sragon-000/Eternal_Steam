using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam
{
    [CreateAssetMenu(menuName = "Eternal Steam/Modules/Health")]
    public sealed class HealthModuleDefinition : BuildingModuleDefinition
    {
        public float Maximum = 100;
        public override bool Provides(System.Type capability) => capability == typeof(IDamageReceiver) || capability == typeof(IHealthScaling);
        public override IBuildingModule CreateRuntime() => new HealthModule(Maximum);
        public override void Validate(List<string> errors)
        { if (!float.IsFinite(Maximum) || Maximum <= 0) errors.Add("Health must be positive and finite."); }
    }
    public sealed class HealthModule : IBuildingModule, IDamageReceiver, IHealthScaling
    {
        BuildingInstance owner;
        readonly float baseMaximum;
        public float Maximum { get; private set; }
        public float Current { get; private set; }
        public bool Alive => Current > 0 && owner != null && !owner.Disposed;
        public HealthModule(float maximum) { baseMaximum = Maximum = Current = maximum; }
        public void SetMaximumMultiplier(float multiplier)
        {
            if (!float.IsFinite(multiplier) || multiplier < 1) return;
            float ratio = Current / Maximum; Maximum = baseMaximum * multiplier; Current = Maximum * ratio;
        }
        public void Initialize(BuildingInstance value, BuildingServices services) => owner = value;
        public void Activate() { }
        public void Tick(float dt) { }
        public void ApplyDamage(float amount)
        {
            if (!Alive || !owner.Active || amount <= 0 || !float.IsFinite(amount)) return;
            Current = Mathf.Max(0, Current - amount);
            if (Current == 0) owner.Destroy();
        }
        public void Dispose() { }
    }
}
