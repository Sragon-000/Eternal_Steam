using System;
using UnityEngine;
namespace EternalSteam
{
    public enum BaseRole { Legacy, Main, Sub }
    public interface IBaseRole { BaseRole Role {get;} }
    public interface IBaseIdentity { string BaseId { get; } }

    // Identity is a role, independent of providing an area. Outposts do not create bases.
    [CreateAssetMenu(menuName="Eternal Steam/Modules/Base Identity")]
    public sealed class BaseModuleDefinition : BuildingModuleDefinition
    {
        public BaseRole Role;
        public override IBuildingModule CreateRuntime() => new Identity(Role);
        public override bool Provides(Type capability) => capability == typeof(IBaseIdentity);
        sealed class Identity : IBuildingModule, IBaseIdentity, IBaseRole
        {
            public BaseRole Role {get;}
            public Identity(BaseRole role){Role=role;}
            [Saved] public string BaseId { get; private set; } = Guid.NewGuid().ToString("N");
            public void Initialize(BuildingInstance owner, BuildingServices services) { }
            public void Activate() { }
            public void Tick(float dt) { }
            public void Dispose() { }
        }
    }
}
