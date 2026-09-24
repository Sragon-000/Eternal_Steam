using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    public enum PowerRole { Storage, Producer, Consumer }
    [CreateAssetMenu(menuName="Eternal Steam/Modules/Base Power")]
    public sealed class PowerModuleDefinition:BuildingModuleDefinition
    {
        public PowerRole Role;
        [Min(0)] public double Capacity=200,CapacityPerLevel=20,Rate=5;
        public bool VerificationSettings=true;
        [Tooltip("Only for attacks executed by the legacy adapter outside building modules.")]
        public bool ExternalAttackAdapter;
        public override IBuildingModule CreateRuntime()=>new PowerModule(Role,Capacity,CapacityPerLevel,Rate,ExternalAttackAdapter);
        public override void Validate(List<string> errors){if(!Enum.IsDefined(typeof(PowerRole),Role)||!double.IsFinite(Capacity)||Capacity<0||!double.IsFinite(CapacityPerLevel)||CapacityPerLevel<0||!double.IsFinite(Rate)||Rate<0)errors.Add("Invalid power settings");}
        public override void ValidateComposition(BuildingDefinition definition,List<string> errors){if(Role==PowerRole.Storage){bool normal=false;foreach(var m in definition.Modules)if(m is BaseModuleDefinition b&&(b.Role==BaseRole.Main||b.Role==BaseRole.Sub))normal=true;if(!normal)errors.Add("Power storage requires a normal base");}}
    }
    public sealed class PowerModule:IBuildingModule,ICombatPermission
    {
        public PowerRole Role {get;}
        public double Rate {get;}
        readonly double capacity,growth;readonly bool externalAttack;
        public bool RequestsSupply=>Role!=PowerRole.Consumer||externalAttack||Owner.Module<ICombatModule>()!=null;
        public BuildingInstance Owner {get;private set;}
        public PowerModule Supply {get;internal set;}
        [Saved] public bool Supplied {get;internal set;}
        [Saved(0)] public double Stored {get;internal set;}
        public double Capacity=>capacity+growth*((Owner?.Module<IUpgradeControl>()?.Level??1)-1);
        public double Production {get;internal set;}
        public double Requested {get;internal set;}
        [Saved(0)] public double Consumed {get;internal set;}
        [Saved(0)] internal double PendingProduction,PendingDemand;
        public PowerModule(PowerRole role,double capacity,double growth,double rate,bool externalAttack=false){this.externalAttack=externalAttack;Role=role;this.capacity=capacity;this.growth=growth;Rate=rate;}
        public void Initialize(BuildingInstance owner,BuildingServices services){Owner=owner;}
        public void Activate(){}public void Tick(float dt){}
        public void Dispose(){Supply=null;Supplied=false;Stored=0;PendingProduction=PendingDemand=0;}
        public void RestoreSupply(PowerModule supply){Supply=supply;}
        public bool AllowsCombat=>Role!=PowerRole.Consumer||!RequestsSupply||(Supplied&&Supply!=null&&Supply.Owner.Active&&!Supply.Owner.Disposed);
    }
}
