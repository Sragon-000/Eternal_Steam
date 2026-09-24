using System;
using UnityEngine;
namespace EternalSteam
{
    [CreateAssetMenu(menuName="Eternal Steam/Modules/Shared Main Upgrade")]
    public sealed class MainBaseUpgradeDefinition:BuildingModuleDefinition
    {
        public override IBuildingModule CreateRuntime()=>new Runtime();
        public override bool Provides(Type capability)=>capability==typeof(IUpgradeControl);
        sealed class Runtime:IBuildingModule,IUpgradeControl
        {
            ICampaignMainLevel campaign;BuildingInstance owner;
            public int Level=>campaign.MainLevel;
            public int MaximumLevel=>CampaignProgression.MaximumLevel;
            public void Initialize(BuildingInstance owner,BuildingServices services){this.owner=owner;campaign=services.Campaign??throw new InvalidOperationException("Shared main requires campaign");}
            public bool TryUpgrade(out string reason){reason=null;if(!owner.Active||owner.Disposed){reason="활성 메인 기지가 아닙니다.";return false;}if(!campaign.TryUpgrade(Level)){reason="최대 레벨입니다.";return false;}return true;}
            public void Activate(){}public void Tick(float dt){}public void Dispose(){}
        }
    }
}
