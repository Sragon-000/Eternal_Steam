using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    public readonly struct BuildingTargetHandle:IEquatable<BuildingTargetHandle>
    {
        public readonly int Id;
        public BuildingTargetHandle(int id){Id=id;}
        public bool Equals(BuildingTargetHandle other)=>Id==other.Id;
        public override bool Equals(object other)=>other is BuildingTargetHandle h&&Equals(h);
        public override int GetHashCode()=>Id;
    }
    public sealed class BuildingTarget
    {
        public readonly BuildingTargetHandle Handle;
        public readonly BuildingInstance Building;
        public readonly IDamageReceiver Receiver;
        public readonly BuildingCombatRole Role;
        public readonly bool BlocksGround;
        readonly Vector3 center;readonly float axisXX,axisXZ,axisZX,axisZZ;readonly Vector3 half;
        public readonly Vector2Int MinimumCell,MaximumCell;
        public bool Alive=>Building.Active&&!Building.Disposed&&Receiver.Alive;
        public BuildingTarget(int id,BuildingInstance building,float cellSize,Quaternion rotation,float bucketSize){
            Handle=new BuildingTargetHandle(id);Building=building;Receiver=building.Module<IDamageReceiver>();var body=building.Module<IBuildingCombatBody>();Role=body.Role;BlocksGround=body.BlocksGround;
            center=building.Position;var axisX=rotation*Vector3.right;var axisZ=rotation*Vector3.forward;axisXX=axisX.x;axisXZ=axisX.z;axisZX=axisZ.x;axisZZ=axisZ.z;half=new Vector3(building.Footprint.x*cellSize*.5f,0,building.Footprint.y*cellSize*.5f);
            var x=rotation*new Vector3(half.x,0,0);var z=rotation*new Vector3(0,0,half.z);var extent=new Vector3(Mathf.Abs(x.x)+Mathf.Abs(z.x),0,Mathf.Abs(x.z)+Mathf.Abs(z.z));
            MinimumCell=Cell(building.Position-extent,bucketSize);MaximumCell=Cell(building.Position+extent,bucketSize);
        }
        public static Vector2Int Cell(Vector3 p,float size)=>new(Mathf.FloorToInt(p.x/size),Mathf.FloorToInt(p.z/size));
        void Local(Vector3 p,out float x,out float z){float dx=p.x-center.x,dz=p.z-center.z;x=dx*axisXX+dz*axisXZ;z=dx*axisZX+dz*axisZZ;}
        public Vector3 Closest(Vector3 point){Local(point,out float x,out float z);x=Mathf.Clamp(x,-half.x,half.x);z=Mathf.Clamp(z,-half.z,half.z);return new Vector3(center.x+x*axisXX+z*axisZX,center.y,center.z+x*axisXZ+z*axisZZ);}
        public float DistanceSquared(Vector3 point){Local(point,out float x,out float z);float dx=Mathf.Max(0,Mathf.Abs(x)-half.x),dz=Mathf.Max(0,Mathf.Abs(z)-half.z);return dx*dx+dz*dz;}
        public Vector3 Approach(Vector3 point,float reach){var closest=Closest(point);var d=point-closest;d.y=0;return d.sqrMagnitude>.000001f?closest+d.normalized*reach:point;}
        static bool Axis(float p,float v,float size,ref float enter,ref float exit){if(Mathf.Abs(v)<.000001f)return Mathf.Abs(p)<=size;float first=(-size-p)/v,last=(size-p)/v;if(first>last){float tmp=first;first=last;last=tmp;}enter=Mathf.Max(enter,first);exit=Mathf.Min(exit,last);return enter<=exit;}
        public bool Intersect(Vector3 from,Vector3 to,out float fraction){
            Local(from,out float x,out float z);float dx=to.x-from.x,dz=to.z-from.z,enter=0,exit=1;
            bool hit=Axis(x,dx*axisXX+dz*axisXZ,half.x,ref enter,ref exit)&&Axis(z,dx*axisZX+dz*axisZZ,half.z,ref enter,ref exit);fraction=enter;return hit;
        }
    }
    public interface IBuildingTargetQuery
    {
        bool TryGet(BuildingTargetHandle handle,out BuildingTarget target);
        BuildingTarget Nearby(Vector3 position,float range,bool air,bool includeGeneral);
        BuildingTarget NearestNexus(Vector3 position);
        bool FirstBlocker(Vector3 from,Vector3 to,out BuildingTarget target,out float fraction);
        bool HasNexus{get;}
    }
    // Static-building spatial hash. Handles are world-global and never reused during this world's lifetime.
    public interface IBuildingMovementCells { bool TryGetClearCell(Vector3 point,out Rect bounds); }
    public interface IBuildingTargetChanges { int Revision {get;} }
    public sealed class BuildingTargetRegistry:IBuildingTargetQuery,IBuildingTargetChanges,IBuildingMovementCells
    {
        const float BucketSize=8;
        readonly Dictionary<int,BuildingTarget> targets=new();readonly Dictionary<BuildingInstance,int> instances=new();
        readonly Dictionary<Vector2Int,List<BuildingTarget>> cells=new();readonly List<BuildingTarget> nexuses=new();int nextId;
        public int Count=>targets.Count;
        public int Revision {get;private set;}
        public bool HasNexus=>nexuses.Count>0;
        public void Register(BuildingInstance b,float cellSize,Quaternion rotation){
            if(!b.Active||b.Disposed||instances.ContainsKey(b)||b.Module<IBuildingCombatBody>()==null||b.Module<IDamageReceiver>()==null)return;
            var target=new BuildingTarget(++nextId,b,cellSize,rotation,BucketSize);targets.Add(target.Handle.Id,target);instances.Add(b,target.Handle.Id);Revision++;
            for(int z=target.MinimumCell.y;z<=target.MaximumCell.y;z++)for(int x=target.MinimumCell.x;x<=target.MaximumCell.x;x++){var key=new Vector2Int(x,z);if(!cells.TryGetValue(key,out var bucket)){bucket=new List<BuildingTarget>();cells.Add(key,bucket);}bucket.Add(target);}
            if(target.Role==BuildingCombatRole.Nexus)nexuses.Add(target);
        }
        public void Remove(BuildingInstance b){if(!instances.Remove(b,out int id)||!targets.Remove(id,out var t))return;Revision++;
            for(int z=t.MinimumCell.y;z<=t.MaximumCell.y;z++)for(int x=t.MinimumCell.x;x<=t.MaximumCell.x;x++){var key=new Vector2Int(x,z);var bucket=cells[key];bucket.Remove(t);if(bucket.Count==0)cells.Remove(key);}nexuses.Remove(t);
        }
        public bool TryGet(BuildingTargetHandle h,out BuildingTarget t)=>targets.TryGetValue(h.Id,out t)&&t.Alive;
        public BuildingTarget Nearby(Vector3 p,float range,bool air,bool includeGeneral){
            var min=BuildingTarget.Cell(p-new Vector3(range,0,range),BucketSize);var max=BuildingTarget.Cell(p+new Vector3(range,0,range),BucketSize);
            BuildingTarget best=null;int priority=int.MaxValue;float distance=float.PositiveInfinity;
            for(int z=min.y;z<=max.y;z++)for(int x=min.x;x<=max.x;x++)if(cells.TryGetValue(new Vector2Int(x,z),out var bucket))foreach(var t in bucket){
                if(!t.Alive||t.Role==BuildingCombatRole.Nexus||(air&&t.Role==BuildingCombatRole.Wall))continue;
                int rank=t.Role==BuildingCombatRole.Defense?0:t.Role==BuildingCombatRole.General?1:2;if(rank>0&&!includeGeneral)continue;
                float d=t.DistanceSquared(p);if(d>range*range||rank>priority||rank==priority&&(d>distance||d==distance&&best!=null&&t.Handle.Id>=best.Handle.Id))continue;
                best=t;priority=rank;distance=d;
            }return best;
        }
        public BuildingTarget NearestNexus(Vector3 p){BuildingTarget best=null;float distance=float.PositiveInfinity;foreach(var t in nexuses){if(!t.Alive)continue;float d=t.DistanceSquared(p);if(d>distance||d==distance&&best!=null&&t.Handle.Id>=best.Handle.Id)continue;best=t;distance=d;}return best;}
        public bool TryGetClearCell(Vector3 point,out Rect bounds){
            var cell=BuildingTarget.Cell(point,BucketSize);bounds=new Rect(cell.x*BucketSize,cell.y*BucketSize,BucketSize,BucketSize);
            if(cells.TryGetValue(cell,out var bucket))foreach(var t in bucket)if(t.BlocksGround&&t.Alive)return false;
            return true;
        }
        public bool FirstBlocker(Vector3 from,Vector3 to,out BuildingTarget target,out float fraction){
            target=null;fraction=float.PositiveInfinity;
            // Sweep only this movement/attack segment. Never query a whole world-sized route rectangle.
            var min=BuildingTarget.Cell(Vector3.Min(from,to),BucketSize);var max=BuildingTarget.Cell(Vector3.Max(from,to),BucketSize);
            for(int z=min.y;z<=max.y;z++)for(int x=min.x;x<=max.x;x++)if(cells.TryGetValue(new Vector2Int(x,z),out var bucket))foreach(var t in bucket){
                if(!t.Alive||!t.BlocksGround||!t.Intersect(from,to,out float f)||f>fraction||f==fraction&&target!=null&&t.Handle.Id>=target.Handle.Id)continue;target=t;fraction=f;
            }return target!=null;
        }
    }
}
