using UnityEngine.UIElements;
namespace EternalSteam.OpenWorld
{
    public sealed class MapProgressView
    {
        readonly VisualElement panel;
        readonly Label energy,status;
        readonly Button craft;
        readonly MapAssaultController assault;
        public MapProgressView(VisualElement root,OpenWorldSandbox sandbox)
        {
            panel=root.Q("map-progress");energy=root.Q<Label>("map-energy");status=root.Q<Label>("map-stage");craft=root.Q<Button>("orb-craft");assault=sandbox.Assault;
            panel.style.display=assault==null?DisplayStyle.None:DisplayStyle.Flex;
            craft.focusable=false;craft.clicked+=()=>{if(!sandbox.GetComponent<OpenWorldInput>().IsEditing&&!sandbox.Content.Defeated&&sandbox.Persistence?.Blocked!=true){sandbox.Message=assault.Craft()?"완벽한 에너지 오브 제작 · 맵 클리어":"보스 보상과 해당 맵 에너지가 필요합니다.";sandbox.Persistence?.RequestAutoSave();}};
        }
        public void Refresh(bool editing)
        {
            if(assault==null)return;var e=assault.Energy;
            energy.text=$"{assault.EnergyName} · 확보 {100d*e.Earned/e.Target:0.0}%\n추출 {e.Extracted/1000d:0.##} ({100d*e.Extracted/e.Target:0.0}%) · 처치 {e.KillReward/1000d:0.##} · 보스 {e.BossReward/1000d:0.##}\n잔고 {e.Balance/1000d:0.##} / 목표 {e.Target/1000d:0.##}";
            status.text=e.Stage switch {MapStage.Gathering=>"기지 영역에서 에너지 추출 중",MapStage.WaitingForNight=>"90% 확보 · 오늘 밤 보스 출현",MapStage.BossBattle=>assault.BossSpawnFailure??$"보스전 · 보스 체력 {assault.BossHealth}",MapStage.AwaitingCraft=>"불완전 오브 획득 · 완벽 오브 제작 가능",MapStage.Cleared=>"맵 클리어 · 완벽 오브 보유",_=>"메인 기지 상실 · 공략 실패"};
            status.text+=$"\n소환 지점 {assault.Planner.Points.Count} · 야간 대기 {assault.Planner.Pending:N0}";
            craft.SetEnabled(!editing&&e.Stage==MapStage.AwaitingCraft);
        }
    }
}
