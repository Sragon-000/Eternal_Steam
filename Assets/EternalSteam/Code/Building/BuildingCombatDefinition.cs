using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    public enum BuildingCombatRole { General, Defense, Nexus, Wall }
    public interface IBuildingCombatBody { BuildingCombatRole Role {get;} bool BlocksGround {get;} }
    [CreateAssetMenu(menuName="Eternal Steam/Modules/Building Combat Body")]
    public sealed class BuildingCombatDefinition:BuildingModuleDefinition
    {
        public BuildingCombatRole Role;
        public bool BlocksGround=true;
        public override IBuildingModule CreateRuntime()=>new Body(Role,BlocksGround);
        public override bool Provides(Type type)=>type==typeof(IBuildingCombatBody);
        public override void Validate(List<string> errors){if(!Enum.IsDefined(typeof(BuildingCombatRole),Role))errors.Add("Invalid building combat role.");}
        public override void ValidateComposition(BuildingDefinition definition,List<string> errors){if(!definition.Provides<IDamageReceiver>())errors.Add("Combat body requires a damage receiver.");}
        sealed class Body:IBuildingModule,IBuildingCombatBody
        {
            public BuildingCombatRole Role{get;} public bool BlocksGround{get;}
            public Body(BuildingCombatRole role,bool blocks){Role=role;BlocksGround=blocks;}
            public void Initialize(BuildingInstance owner,BuildingServices services){}public void Activate(){}public void Tick(float dt){}public void Dispose(){}
        }
    }
}
