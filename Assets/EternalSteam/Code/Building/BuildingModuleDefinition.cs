using System;
using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam
{
    public abstract class BuildingModuleDefinition : ScriptableObject
    {
        public abstract IBuildingModule CreateRuntime();
        public virtual void Validate(List<string> errors) { }
        public virtual bool Provides(Type capability) => false;
        public virtual void ValidateComposition(BuildingDefinition definition, List<string> errors) { }
    }

    public interface IBuildingModule : IDisposable
    {
        void Initialize(BuildingInstance owner, BuildingServices services);
        void Activate();
        void Tick(float deltaTime);
    }

    public sealed class BuildingServices
    {
        public ICampaignMainLevel Campaign {get;}
        public ITargetQuery Targets { get; }
        public IBaseObjective Nexus { get; }
        public ILevelLimit LevelLimit { get; }
        public IResourceBank Resources { get; }
        public IMovementObstacles Obstacles { get; }
        public BuildingServices(ITargetQuery targets, IBaseObjective nexus = null, IResourceBank resources = null, IMovementObstacles obstacles = null, ILevelLimit levelLimit = null, ICampaignMainLevel campaign = null)
        { Campaign=campaign;Targets = targets; Nexus = nexus; LevelLimit = levelLimit ?? nexus; Resources = resources; Obstacles = obstacles; }
    }

    public sealed class BuildingInstance : IDisposable
    {
        readonly List<IBuildingModule> modules = new();
        readonly string definitionId;
        public int Id { get; }
        public string PersistentId {get;private set;}=Guid.NewGuid().ToString("N");
        public IReadOnlyList<IBuildingModule> Modules=>modules;
        public void RestoreIdentity(string id,string ownerBaseId){if(Active||!Guid.TryParseExact(id,"N",out _))throw new InvalidOperationException("Invalid building restore identity");PersistentId=id;OwnerBaseId=ownerBaseId;}
        public string DefinitionId => definitionId;
        public string DisplayName { get; }
        public Vector2Int Footprint { get; }
        public Vector2Int Cell { get; }
        public Vector3 Position { get; }
        public Vector3 Direction { get; private set; } = Vector3.forward;
        public bool Recoverable { get; }
        public bool RequiresBuildArea { get; }
        public bool RequiresOperationalArea { get; }
        public bool RequiresOwnerBase { get; }
        public string OwnerBaseId { get; private set; }
        public OperationBlock OperationBlock { get; private set; }
        public bool Operational => Active && !Disposed && OperationBlock==OperationBlock.None;
        public event Action OperationChanged;
        internal void AssignBase(string id) { if(OwnerBaseId==null)OwnerBaseId=id; }
        internal void SetOperationBlock(OperationBlock value) { if(value==OperationBlock)return;OperationBlock=value;OperationChanged?.Invoke(); }
        public bool Active { get; private set; }
        public bool Disposed { get; private set; }
        public bool EditingDirection { get; private set; }
        public event Action<float> Damaged;
        public void ReportDamage(float amount)=>Damaged?.Invoke(amount);
        public event Action<BuildingInstance> Destroyed;
        public event Action<BuildingInstance> Destroying;
        public event Action<Vector3> Shot;
        public event Action<Vector3> Aim;
        public void ReportAim(Vector3 position)=>Aim?.Invoke(position);
        public event Action DirectionChanged;
        public event Action<Vector3> Projectile;
        public void ReportProjectile(Vector3 position) => Projectile?.Invoke(position);

        public BuildingInstance(int id, BuildingDefinition definition, Vector2Int cell, Vector3 position, BuildingServices services)
        {
            Id = id;
            definitionId = definition.Id;
            DisplayName = definition.DisplayName;
            Footprint = definition.Footprint;
            Recoverable = definition.Recoverable;
            RequiresBuildArea = definition.Placement!=null && definition.Placement.RequiresBuildArea;
            RequiresOperationalArea = definition.Placement!=null && definition.Placement.RequiresOperationalArea;
            RequiresOwnerBase = definition.Placement!=null && definition.Placement.RequiresOwnerBase;
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

        public void Tick(float dt, bool combatEnabled = true)
        {
            if (!Active || Disposed || EditingDirection) return;
            foreach (var module in modules)
            {
                if (Disposed) break;
                if(module is IOperationalModule && !Operational)continue;
                if(module is ICombatModule){if(!combatEnabled)continue;if(!CombatPermission.Allows(this)){if(module is IUnpoweredCombat ongoing)ongoing.TickUnpowered(dt);continue;}}
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
            DirectionChanged?.Invoke();
        }
        public void ReportShot(Vector3 position) => Shot?.Invoke(position);
        public void Destroy()
        {
            if (Disposed) return;
            Destroying?.Invoke(this);
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
            Damaged = null;
            OperationChanged = null;
            Shot = null;
            Aim = null;
            DirectionChanged = null;
            Projectile = null;
            Destroyed = null;
            Destroying = null;
        }
    }
}
