using UnityEngine.UIElements;
namespace EternalSteam.OpenWorld
{
    public sealed class BasePowerView
    {
        readonly Label summary,details;readonly OpenWorldSandbox sandbox;
        public BasePowerView(VisualElement root,OpenWorldSandbox sandbox){this.sandbox=sandbox;summary=root.Q<Label>("base-power");details=root.Q<Label>("device-power");summary.style.display=sandbox.LegacyPower==null?DisplayStyle.None:DisplayStyle.Flex;}
        public void Refresh(BuildingInstance selected)
        {
            summary.text="전력 · 기지를 설치하고 선택하세요.";details.text="";
            var device=selected?.Module<PowerModule>();PowerModule storage=device?.Role==PowerRole.Storage?device:device?.Supply;
            if(storage==null&&sandbox.Content.Bases.SelectedBaseId is string id&&sandbox.Content.Bases.Bases.TryGetValue(id,out var context))storage=context.Nexus.Module<PowerModule>();
            if(storage!=null)summary.text=$"{storage.Owner.DisplayName} [{storage.Owner.Module<IBaseIdentity>().BaseId.Substring(0,6)}] 전력 { (storage.Capacity>0?storage.Stored/storage.Capacity*100:0):0}% · {storage.Stored:0.#} / {storage.Capacity:0.#}\n생산 +{storage.Production:0.#}/s · 요청 {storage.Requested:0.#}/s · 실제 소비 {storage.Consumed:0.#}/s";
            if(device!=null&&device.Role!=PowerRole.Storage)details.text=$"전력 {(device.Role==PowerRole.Producer?"생산":"소비")} {device.Rate:0.#}/s · "+(device.Supply==null?"연결 기지 없음":device.Supply.Owner.DisplayName+" · "+(device.Role==PowerRole.Producer?"연결됨":device.Supplied?"공급 중":"전력 부족 / 공급 대기"));
        }
    }
}
