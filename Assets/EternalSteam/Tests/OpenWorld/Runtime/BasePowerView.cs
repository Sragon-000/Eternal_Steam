using UnityEngine.UIElements;
namespace EternalSteam.OpenWorld
{
    public sealed class BasePowerView
    {
        readonly Label summary,flow,details;readonly OpenWorldSandbox sandbox;
        public BasePowerView(VisualElement root,OpenWorldSandbox sandbox){this.sandbox=sandbox;summary=root.Q<Label>("base-power");flow=root.Q<Label>("base-power-details");details=root.Q<Label>("device-power");}
        public void Refresh(BuildingInstance selected)
        {
            summary.text="기지를 설치하고 선택하세요.";flow.text="";details.text="";
            var device=selected?.Module<PowerModule>();PowerModule storage=device?.Role==PowerRole.Storage?device:device?.Supply;
            if(storage==null&&sandbox.Content.Bases.SelectedBaseId is string id&&sandbox.Content.Bases.Bases.TryGetValue(id,out var context))storage=context.Nexus.Module<PowerModule>();
            if(storage!=null){summary.text=$"{storage.Owner.DisplayName} [{storage.Owner.Module<IBaseIdentity>().BaseId.Substring(0,6)}]\n{storage.Stored:0.#} / {storage.Capacity:0.#}";flow.text=$"생산 +{storage.Production:0.#}/s · 요청 {storage.Requested:0.#}/s · 실제 소비 {storage.Consumed:0.#}/s";}
            if(sandbox.Content.BaseRules){int subs=0;foreach(var registered in sandbox.Content.Bases.Bases.Values)if(registered.Nexus.Module<IBaseRole>()?.Role==BaseRole.Sub)subs++;
                flow.text+=$"\n메인 Lv.{sandbox.Content.LevelCap} · 서브 {subs}/{sandbox.Content.SubLimit}";}
            if(sandbox.MeetingConstructionRules){var next=sandbox.Content.Bases.SelectedBaseId;flow.text+="\n다음 배치 기지 "+(next==null?"없음":next.Substring(0,System.Math.Min(6,next.Length)));}
            if(device!=null&&device.Role!=PowerRole.Storage)details.text=$"전력 {(device.Role==PowerRole.Producer?"생산":"소비")} {device.Rate:0.#}/s · "+(device.Supply==null?"연결 기지 없음":device.Supply.Owner.DisplayName+" · "+(device.Role==PowerRole.Producer?"연결됨":device.Supplied?"공급 중":"전력 부족 / 공급 대기"));
        }
    }
}
