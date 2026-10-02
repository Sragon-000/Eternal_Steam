using UnityEngine;
namespace EternalSteam.Railway
{
    public enum RailFacilityKind {Track,Station}
    [CreateAssetMenu(menuName="Eternal Steam/Railway/Facility")]
    public sealed class RailFacilityDefinition:BuildingModuleDefinition
    {
        public RailFacilityKind Kind;
        public override IBuildingModule CreateRuntime()=>new RailFacility(Kind);
    }
    public sealed class RailFacility:IBuildingModule
    {
        public RailFacilityKind Kind {get;}
        public BuildingInstance Owner {get;private set;}
        public RailFacility(RailFacilityKind kind){Kind=kind;}
        public void Initialize(BuildingInstance owner,BuildingServices services){Owner=owner;}
        public void Activate(){} public void Tick(float dt){} public void Dispose(){}
    }
}
