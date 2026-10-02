namespace EternalSteam.OpenWorld
{
    public readonly struct UpgradeHudState
    {
        public readonly bool Available,VerificationFree;
        public readonly int Level,MaximumLevel;
        public readonly string Reason;
        public UpgradeHudState(bool available,bool verificationFree,int level,int maximumLevel,string reason)
        {Available=available;VerificationFree=verificationFree;Level=level;MaximumLevel=maximumLevel;Reason=reason;}
    }

    // UI composition only. The modules and purchase service remain the rule owners.
    public sealed class OpenWorldHudActions
    {
        readonly OpenWorldSandbox sandbox;
        readonly OpenWorldInput input;
        readonly UpgradePurchase fallbackPurchase;
        UpgradePurchase Purchase=>sandbox.Persistence?.Upgrades??fallbackPurchase;
        BuildingInstance Selected=>input.SelectedContent??input.SelectedTower?.building;
        public OpenWorldHudActions(OpenWorldSandbox sandbox,OpenWorldInput input)
        {this.sandbox=sandbox;this.input=input;fallbackPurchase=new UpgradePurchase(sandbox.Content.PaymentBankFor,sandbox.UpgradeCosts!=null?sandbox.UpgradeCosts:new VerificationFreeUpgrade());}

        public bool CanInteract(out string reason)
        {
            reason=null;
            if(sandbox.Content.Defeated){reason="메인 기지 파괴 · 공략 실패";return false;}
            if(sandbox.Persistence?.Blocked==true){reason="복원 오류로 진행이 차단되었습니다.";return false;}
            if(input.IsEditing){reason="수정 작업을 확정하거나 취소하세요.";return false;}
            return true;
        }
        public UpgradeHudState ReadUpgrade()
        {
            var selected=Selected;var upgrade=selected?.Module<IUpgradeControl>();
            bool available=CanInteract(out var reason)&&Purchase.CanUpgrade(selected,out reason);
            return new UpgradeHudState(available,Purchase.IsVerificationFree,upgrade?.Level??0,upgrade?.MaximumLevel??0,reason);
        }
        public bool TryUpgrade()
        {
            if(!CanInteract(out var reason)){sandbox.Message=reason;return false;}
            var selected=Selected;
            if(!Purchase.TryUpgrade(selected,out reason)){sandbox.Message=reason;return false;}
            sandbox.Message=$"강화 Lv.{selected.Module<IUpgradeControl>().Level}"+(Purchase.IsVerificationFree?" · 검증용 무료":"");
            sandbox.Persistence?.RequestAutoSave();
            return true;
        }
        public string ProgressLabel=>sandbox.Clock.Paused?"계속 진행":"일시정지";
        public string RunLabel=>sandbox.Assault!=null?ProgressLabel:sandbox.Running?"전투 일시정지":"전투 실행";
        public string Mode=>sandbox.Content.Defeated?"메인 기지 파괴 · 공략 실패":sandbox.Persistence?.Blocked==true?"복원 오류 · 진행 차단":input.IsEditing?"수정 중":sandbox.Clock.Paused?"일시정지":sandbox.Running?"전투 실행 중":"준비";
        public bool ToggleProgress()
        {
            if(!CanInteract(out var reason)){sandbox.Message=reason;return false;}
            sandbox.Clock.Paused=!sandbox.Clock.Paused;
            if(sandbox.Assault!=null)sandbox.Running=!sandbox.Clock.Paused;
            return true;
        }
        public bool ToggleRun()
        {
            if(sandbox.Assault!=null)return ToggleProgress();
            if(!CanInteract(out var reason)){sandbox.Message=reason;return false;}
            input.Cancel();sandbox.Running=!sandbox.Running;
            return true;
        }
    }
}
