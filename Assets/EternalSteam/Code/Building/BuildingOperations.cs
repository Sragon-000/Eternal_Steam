using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    // Opt-in capability: health, selection and lifecycle continue while production/combat pause.
    public interface IOperationalModule { }
    public interface ICombatModule : IOperationalModule { }
    public interface IUnpoweredCombat { void TickUnpowered(float dt); }
    [Flags] public enum OperationBlock { None=0, OutsideArea=1, BaseLost=2 }

    public sealed class BaseContext
    {
        public string Id { get; }
        public BuildingInstance Nexus { get; }
        public bool Active => Nexus.Active && !Nexus.Disposed;
        public BaseContext(BuildingInstance nexus) { Nexus=nexus; Id=nexus.Module<IBaseIdentity>().BaseId; }
    }

    // Explicitly owned by a world composition; no static state or global service lookup.
    public sealed class BaseRegistry
    {
        readonly Dictionary<string,BaseContext> bases=new();
        readonly List<BuildingInstance> buildings=new();
        readonly Quaternion rotation;
        readonly float cellSize;
        bool dirty;
        public bool AnyNormalBaseCoverage {get;set;}
        int previousLevels;
        public IReadOnlyList<BuildingInstance> Buildings=>buildings;
        public IReadOnlyDictionary<string,BaseContext> Bases => bases;
        public string SelectedBaseId { get; private set; }
        public int Revision { get; private set; }
        public void Invalidate(){dirty=true;Revision++;}
        public BaseRegistry(float cellSize,Quaternion rotation) { this.cellSize=cellSize;this.rotation=rotation; }
        public bool Select(string id) { if(id==null||!bases.ContainsKey(id))return false;SelectedBaseId=id;return true; }
        public void Register(BuildingInstance building)
        {
            if(buildings.Contains(building))return;
            buildings.Add(building);
            var identity=building.Module<IBaseIdentity>();
            if(identity!=null) {
                bases.Add(identity.BaseId,new BaseContext(building));
                building.AssignBase(identity.BaseId);SelectedBaseId=identity.BaseId;
            } else if(SelectedBaseId!=null) building.AssignBase(SelectedBaseId);
            dirty=true;Revision++;
        }
        public void Remove(BuildingInstance building)
        {
            if(!buildings.Remove(building))return;
            var identity=building.Module<IBaseIdentity>();
            if(identity!=null) { bases.Remove(identity.BaseId);if(SelectedBaseId==identity.BaseId){SelectedBaseId=null;foreach(var b in bases.Values){SelectedBaseId=b.Id;break;}} }
            dirty=true;Revision++;
        }
        public void Refresh()
        {
            if(AnyNormalBaseCoverage){int levels=0;foreach(var context in bases.Values)levels+=context.Nexus.Module<IUpgradeControl>()?.Level??1;if(levels!=previousLevels){previousLevels=levels;dirty=true;Revision++;}}
            if(!dirty)return;dirty=false;
            // Unowned buildings bind once. Losing a base never silently changes ownership.
            foreach(var b in buildings) if(b.OwnerBaseId==null) {
                BaseContext nearest=null;float best=float.PositiveInfinity;
                foreach(var context in bases.Values) {
                    float distance=(context.Nexus.Position-b.Position).sqrMagnitude;
                    if(distance<best){best=distance;nearest=context;}
                }
                if(nearest!=null)b.AssignBase(nearest.Id);
            }
            foreach(var b in buildings) {
                OperationBlock reason=OperationBlock.None;
                if(b.RequiresOperationalArea||b.RequiresOwnerBase) {
                    if(!AnyNormalBaseCoverage&&(b.OwnerBaseId==null||!bases.TryGetValue(b.OwnerBaseId,out var context)||!context.Active)) reason|=OperationBlock.BaseLost;
                    if((b.RequiresOperationalArea||(AnyNormalBaseCoverage&&b.RequiresOwnerBase))&&!Covered(b))reason|=OperationBlock.OutsideArea;
                }
                b.SetOperationBlock(reason);
            }
        }
        bool Covered(BuildingInstance target)
        {
            foreach(var provider in buildings) {
                if(AnyNormalBaseCoverage){if(!provider.Active||provider.Disposed||provider.Module<IBaseRole>() is not IBaseRole role||(role.Role!=BaseRole.Main&&role.Role!=BaseRole.Sub))continue;}
                else if(provider.OwnerBaseId!=target.OwnerBaseId||provider.RequiresOperationalArea)continue;
                if(provider.OwnerBaseId==null||!bases.TryGetValue(provider.OwnerBaseId,out var context)||!context.Active)continue;
                if(provider.Module<IBuildArea>() is IBuildArea area&&area.Contains(target.Position,(Vector2)target.Footprint*(cellSize*.5f),rotation))return true;
            }
            return false;
        }
    }
}
