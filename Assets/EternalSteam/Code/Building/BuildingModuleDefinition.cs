using System;
using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam
{
    public abstract class BuildingModuleDefinition : ScriptableObject
    {
        public abstract IBuildingModule CreateRuntime();
        public virtual void Validate(List<string> errors) { }
    }

    public interface IBuildingModule : IDisposable
    {
        void Initialize(BuildingInstance owner, BuildingServices services);
        void Activate();
        void Tick(float deltaTime);
    }

    public sealed class BuildingServices
    {
        public ITargetQuery Targets { get; }
        public BuildingServices(ITargetQuery targets) { Targets = targets; }
    }

    public sealed class BuildingInstance : IDisposable
    {
        readonly List<IBuildingModule> modules = new();
        readonly string definitionId;
        public int Id { get; }
        public string DefinitionId => definitionId;
        public string DisplayName { get; }
        public Vector2Int Footprint { get; }
        public Vector2Int Cell { get; }
        public Vector3 Position { get; }
        public Vector3 Direction { get; private set; } = Vector3.forward;
        public bool Recoverable { get; }
        public bool Active { get; private set; }
        public bool Disposed { get; private set; }
        public bool EditingDirection { get; private set; }
        public event Action<BuildingInstance> Destroyed;
        public event Action<Vector3> Shot;

        public BuildingInstance(int id, BuildingDefinition definition, Vector2Int cell, Vector3 position, BuildingServices services)
        {
            Id = id;
            definitionId = definition.Id;
            DisplayName = definition.DisplayName;
            Footprint = definition.Footprint;
            Recoverable = definition.Recoverable;
            Cell = cell;
            Position = position;
            try
            {
                foreach (var definitionModule in definition.Modules)
                {
                    var module = definitionModule.CreateRuntime() ?? throw new InvalidOperationException("Module factory returned null.");
                    modules.Add(module);
                }
                foreach (var module in modules) module.Initialize(this, services);
            }
            catch { Dispose(); throw; }
        }

        public T Module<T>() where T : class
        {
            foreach (var module in modules) if (module is T result) return result;
            return null;
        }

        public void Activate()
        {
            if (Disposed) throw new ObjectDisposedException(nameof(BuildingInstance));
            if (Active) return;
            Active = true;
            foreach (var module in modules) module.Activate();
        }

        public void Tick(float dt)
        {
            if (!Active || Disposed || EditingDirection) return;
            foreach (var module in modules)
            {
                if (Disposed) break;
                module.Tick(dt);
            }
        }

        public void BeginDirectionEdit() { if (!Disposed) EditingDirection = true; }
        public void CancelDirectionEdit() => EditingDirection = false;
        public void ConfirmDirection(Vector3 direction)
        {
            direction.y = 0;
            if (!float.IsFinite(direction.sqrMagnitude) || direction.sqrMagnitude < 0.0001f)
                throw new ArgumentException("Direction must be finite and nonzero.");
            Direction = direction.normalized;
            EditingDirection = false;
            Module<AttackModule>()?.ClearTarget();
        }
        public void ReportShot(Vector3 position) => Shot?.Invoke(position);
        public void Destroy()
        {
            if (Disposed) return;
            var callback = Destroyed;
            Dispose();
            callback?.Invoke(this);
        }
        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            Active = false;
            for (int i = modules.Count - 1; i >= 0; i--) modules[i].Dispose();
            Shot = null;
            Destroyed = null;
        }
    }
}
