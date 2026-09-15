using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    [Flags] public enum TargetKind { Ground = 1, Air = 2, All = Ground | Air }

    public readonly struct TargetHandle : IEquatable<TargetHandle>
    {
        public readonly int Id;
        public readonly int Generation;
        public TargetHandle(int id, int generation) { Id = id; Generation = generation; }
        public bool Equals(TargetHandle other) => Id == other.Id && Generation == other.Generation;
        public override bool Equals(object obj) => obj is TargetHandle other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Id, Generation);
    }
    public interface IDamageReceiver { bool Alive { get; } void ApplyDamage(float amount); }
    public readonly struct TargetInfo
    {
        public readonly TargetHandle Handle;
        public readonly Vector3 Position;
        public readonly TargetKind Kind;
        public readonly IDamageReceiver Receiver;
        public TargetInfo(TargetHandle handle, Vector3 position, TargetKind kind, IDamageReceiver receiver)
        { Handle = handle; Position = position; Kind = kind; Receiver = receiver; }
    }
    public interface ITargetQuery
    {
        bool TryGet(TargetHandle handle, out TargetInfo target);
        void Query(Vector3 origin, float range, List<TargetInfo> results);
    }

}
