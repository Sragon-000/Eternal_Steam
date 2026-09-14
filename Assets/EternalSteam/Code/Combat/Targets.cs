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

    // Sample registry; the horde adapter can supply the same contract using its spatial index.
    public sealed class TargetRegistry : ITargetQuery
    {
        readonly Dictionary<int, TargetInfo> targets = new();
        readonly Dictionary<int, int> generations = new();
        public int Count => targets.Count;
        public TargetHandle Register(int id, Vector3 position, TargetKind kind, IDamageReceiver receiver)
        {
            if (targets.ContainsKey(id)) throw new ArgumentException("Target ID already registered.");
            int generation = generations.TryGetValue(id, out var previous) ? previous + 1 : 1;
            generations[id] = generation;
            var handle = new TargetHandle(id, generation);
            targets.Add(id, new TargetInfo(handle, position, kind, receiver));
            return handle;
        }
        public bool TryGet(TargetHandle handle, out TargetInfo target)
        {
            if (targets.TryGetValue(handle.Id, out target) && target.Handle.Equals(handle) && target.Receiver.Alive) return true;
            target = default;
            return false;
        }
        public void Unregister(TargetHandle handle)
        {
            if (targets.TryGetValue(handle.Id, out var value) && value.Handle.Equals(handle)) targets.Remove(handle.Id);
        }
        public void Move(TargetHandle handle, Vector3 position)
        {
            if (TryGet(handle, out var value)) targets[handle.Id] = new TargetInfo(handle, position, value.Kind, value.Receiver);
        }
        public void Query(Vector3 origin, float range, List<TargetInfo> results)
        {
            results.Clear();
            foreach (var value in targets.Values)
            {
                var delta = value.Position - origin;
                delta.y = 0;
                if (value.Receiver.Alive && delta.sqrMagnitude <= range * range) results.Add(value);
            }
        }
    }
}
