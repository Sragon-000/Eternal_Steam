using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // Only confirmed, active area providers are destinations. Pending previews never enter the world.
    public sealed class NexusDestinationQuery
    {
        readonly BuildingWorld world;readonly bool requireBaseIdentity;
        readonly List<BuildingInstance> targets=new(),scratch=new();
        public bool HasTargets=>targets.Count>0;
        public NexusDestinationQuery(BuildingWorld world,bool requireBaseIdentity=false){this.world=world;this.requireBaseIdentity=requireBaseIdentity;}
        public bool Refresh()
        {
            scratch.Clear();foreach(var b in world.Buildings)if(b.Active&&!b.Disposed&&(b.Module<IBaseIdentity>()!=null||(!requireBaseIdentity&&b.Module<IBuildArea>()!=null)))scratch.Add(b);
            bool changed=scratch.Count!=targets.Count;
            if(!changed)for(int i=0;i<scratch.Count;i++)if(!ReferenceEquals(scratch[i],targets[i])){changed=true;break;}
            if(changed){targets.Clear();targets.AddRange(scratch);}return changed;
        }
        public bool TryNearest(Vector3 position,out Vector3 destination)
        {
            destination=default;float best=float.PositiveInfinity;int bestId=int.MaxValue;
            foreach(var b in targets){var d=b.Position-position;d.y=0;float distance=d.sqrMagnitude;
                if(distance>best||(distance==best&&b.Id>=bestId))continue;best=distance;bestId=b.Id;destination=b.Position;}
            return bestId!=int.MaxValue;
        }
    }
}
