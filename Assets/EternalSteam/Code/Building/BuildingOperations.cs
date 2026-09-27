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
        readonly List<BuildingInstance> areaProviders=new();
        readonly List<AreaStamp> areaStamps=new();
        int previousLevels;
        bool coverageMode;
        string coverageOwner;
        public int CoverageRevision {get;private set;}
        // Compare geometry, not just normal-base levels: legacy outposts can grow as well.
        readonly struct AreaStamp : IEquatable<AreaStamp>
        {
            readonly bool eligible;readonly float radius;readonly BuildAreaShape shape;
            readonly Vector3 a,b,c,d;
            public AreaStamp(bool eligible,IBuildArea area){this.eligible=eligible;radius=area.Radius;shape=area.Shape;a=area.Boundary(0);b=area.Boundary(.25f);c=area.Boundary(.5f);d=area.Boundary(.75f);}
            public bool Equals(AreaStamp other)=>eligible==other.eligible&&radius.Equals(other.radius)&&shape==other.shape&&a.Equals(other.a)&&b.Equals(other.b)&&c.Equals(other.c)&&d.Equals(other.d);
        }
        bool Eligible(BuildingInstance provider,string ownerId)
        {
            if(!provider.Active||provider.Disposed)return false;
            if(AnyNormalBaseCoverage){if(provider.Module<IBaseRole>() is not IBaseRole role||(role.Role!=BaseRole.Main&&role.Role!=BaseRole.Sub))return false;}
            else if(provider.OwnerBaseId!=ownerId||provider.RequiresOperationalArea)return false;
            return provider.OwnerBaseId!=null&&bases.TryGetValue(provider.OwnerBaseId,out var context)&&context.Active;
        }
        public void RefreshCoverage()
        {
            bool changed=coverageMode!=AnyNormalBaseCoverage||coverageOwner!=SelectedBaseId;
            coverageMode=AnyNormalBaseCoverage;coverageOwner=SelectedBaseId;
            for(int i=0;i<areaProviders.Count;i++) {
                var provider=areaProviders[i];var stamp=new AreaStamp(Eligible(provider,provider.OwnerBaseId),provider.Module<IBuildArea>());
                if(stamp.Equals(areaStamps[i]))continue;areaStamps[i]=stamp;changed=true;
            }
            if(changed){CoverageRevision++;dirty=true;Revision++;}
        }
        // Same full-footprint query for operation, rendering and placement feedback.
        public bool Covers(Vector3 center,Vector2 halfSize,Quaternion orientation,string ownerId)
        {
            foreach(var provider in areaProviders)
                if(Eligible(provider,ownerId)&&provider.Module<IBuildArea>().Contains(center,halfSize,orientation))return true;
            return false;
        }
        public IReadOnlyList<BuildingInstance> Buildings=>buildings;
        public IReadOnlyDictionary<string,BaseContext> Bases => bases;
        public string SelectedBaseId { get; private set; }
        public int Revision { get; private set; }
        public void Invalidate(){dirty=true;Revision++;CoverageRevision++;}
        public BaseRegistry(float cellSize,Quaternion rotation) { this.cellSize=cellSize;this.rotation=rotation; }
        public bool Select(string id) { if(id==null||!bases.ContainsKey(id))return false;SelectedBaseId=id;return true; }
        public void Register(BuildingInstance building)
        {
            if(buildings.Contains(building))return;
            buildings.Add(building);
            if(building.Module<IBuildArea>()!=null){areaProviders.Add(building);areaStamps.Add(default);CoverageRevision++;}
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
            int areaIndex=areaProviders.IndexOf(building);if(areaIndex>=0){areaProviders.RemoveAt(areaIndex);areaStamps.RemoveAt(areaIndex);CoverageRevision++;}
            var identity=building.Module<IBaseIdentity>();
            if(identity!=null) { bases.Remove(identity.BaseId);if(SelectedBaseId==identity.BaseId){SelectedBaseId=null;foreach(var b in bases.Values){SelectedBaseId=b.Id;break;}} }
            dirty=true;Revision++;
        }
        public void Refresh()
        {
            if(AnyNormalBaseCoverage){int levels=0;foreach(var context in bases.Values)levels+=context.Nexus.Module<IUpgradeControl>()?.Level??1;if(levels!=previousLevels){previousLevels=levels;dirty=true;Revision++;}}
            RefreshCoverage();
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
        bool Covered(BuildingInstance target)=>Covers(target.Position,(Vector2)target.Footprint*(cellSize*.5f),rotation,target.OwnerBaseId);
    }
}
