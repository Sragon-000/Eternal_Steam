using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
namespace EternalSteam.OpenWorld
{
    [DefaultExecutionOrder(-100)]
    public sealed class OpenWorldHud : MonoBehaviour
    {
        public OpenWorldSandbox Sandbox;
        public OpenWorldInput Input;
        VisualElement root,workspace;
        [Tooltip("기본 노출 자원 ID. 비어 있으면 전체 자원을 표시합니다.")] public string[] ResourcePriority=System.Array.Empty<string>();
        SelectedBuildingView selectedView;
        TextField amount;
        SpawnQuantityInput quantity;
        public BuildingInventoryView Inventory {get;private set;}
        Label message,counts,mode;
        Button run;
        WorldClockView clockView;
        ResourceStockView stockView;
        MapProgressView progressView;
        BasePowerView powerView;
        OpenWorldHudActions actions;
        public OpenWorldMinimap Minimap {get;private set;}
        bool confirmingNewGame;
        void Start()
        {
            root=GetComponent<UIDocument>().rootVisualElement;workspace=root.Q("hud-workspace");amount=root.Q<TextField>("amount");
            quantity=new SpawnQuantityInput(root,amount);
            actions=new OpenWorldHudActions(Sandbox,Input);
            message=root.Q<Label>("message");counts=root.Q<Label>("counts");mode=root.Q<Label>("mode");run=root.Q<Button>("run");
            Inventory=new BuildingInventoryView(root,Entries(),entry=>{
                quantity.EndEdit();
                if(Input.IsEditing){if(entry.Definition!=null)Input.SelectContent(entry.Definition);else Input.Select(entry.Tool,entry.Kind);}
                else Sandbox.Message="수정 버튼을 누른 뒤 배치할 건물을 선택하세요.";
            });
            Bind("edit",()=>{
                if(Input.IsEditing){Input.Cancel();return;}
                Input.BeginEditing();
                Inventory.ClearSelection();
            });
            Bind("confirm",()=>{
                if(!Input.IsEditing)return;
                if((Input.Edits?.Count??0)==0 && Input.Tool!=WorldTool.Direction)Input.Cancel();
                else Input.Confirm();
            });
            Bind("inventory-toggle",()=>Inventory.SetVisible(!Inventory.Visible));
            Bind("inventory-fold",()=>Inventory.SetVisible(!Inventory.Visible));
            BindFold("resource");BindFold("clock");BindFold("test");BindFold("menu");BindFold("status");BindFold("power");
            Bind("cancel",()=>Input.Cancel());Bind("selection-close",()=>Input.ClearSelection());
            root.Q<Toggle>("spawn-air").RegisterValueChangedCallback(e=>Sandbox.SpawnAir=e.newValue);
            Bind("spawn",()=>{if(!Input.IsEditing && Sandbox.Spawn(amount.value)){var m=Sandbox.Message;Input.Cancel();Sandbox.Message=m;}});
            Bind("reset",()=>Sandbox.ResetEnemies());
            Bind("upgrade",()=>actions.TryUpgrade());
            Bind("save",()=>Sandbox.Persistence?.Save());
            Bind("load",()=>Sandbox.Persistence?.ContinueSaved());
            Bind("new-game",()=>confirmingNewGame=true);
            Bind("new-game-confirm",()=>Sandbox.Persistence?.NewGame());
            Bind("new-game-cancel",()=>confirmingNewGame=false);
            Bind("run",()=>actions.ToggleRun());
            Minimap=new OpenWorldMinimap(root,Sandbox);
            stockView=new ResourceStockView(root.Q<Label>("resources"),Sandbox.Content.Resources,Sandbox.ContentCatalog,ResourcePriority);
            selectedView=new SelectedBuildingView(root,Sandbox);
            Bind("resources-all",()=>stockView.SetShowAll(!stockView.ShowAll));
            root.Q("resources-all").EnableInClassList("is-hidden",!stockView.HasPriority);
            clockView=new WorldClockView(root);
            progressView=new MapProgressView(root,Sandbox);powerView=new BasePowerView(root,Sandbox);
            root.Q("clock-dev").style.display=(Application.isEditor||Debug.isDebugBuild)?DisplayStyle.Flex:DisplayStyle.None;
            Bind("clock-day",()=>{Sandbox.Clock.SetPhase(DayPhase.Day);Sandbox.Persistence?.Changed();});
            Bind("clock-night",()=>{Sandbox.Clock.SetPhase(DayPhase.Night);Sandbox.Persistence?.Changed();});
            Bind("clock-pause",()=>actions.ToggleProgress());
            Refresh();
        }
        IEnumerable<InventoryBuilding> Entries()
        {
            if(Sandbox.ContentCatalog!=null)foreach(var d in Sandbox.ContentCatalog.Buildings)
                if(!(Sandbox.StartingBase!=null&&OpenWorldContent.RoleOf(d)==BaseRole.Main))yield return new InventoryBuilding{Id=d.Id,Name=$"{d.DisplayName}\n기지 Lv.{d.Placement?.RequiredNexusLevel??1}",Category=d.Category,Tool=WorldTool.Content,Definition=d};
            // Registered scene prefabs are the legacy adapter's source of available content.
            foreach(var prefab in Sandbox.TowerPrefabs) {
                if(prefab==null || !prefab.TryGetComponent<SceneTower>(out var tower))continue;
                yield return new InventoryBuilding{Id=tower.Kind.ToString(),Name=EternalSteam.Demo.HordeTowerStats.Name(tower.Kind),Category=BuildingCategory.Defense,Tool=WorldTool.Tower,Kind=tower.Kind};
            }
            if(Sandbox.FoundationPrefab!=null)yield return new InventoryBuilding{Id="foundation",Name="토대\n8×8m · 45°",Category=BuildingCategory.Installation,Tool=WorldTool.Foundation};
        }
        void BindFold(string name)
        {
            var body=root.Q(name+"-body");var container=root.Q(name+"-panel");
            Bind(name+"-fold",()=>{bool folded=!container.ClassListContains("folded");container.EnableInClassList("folded",folded);body.EnableInClassList("is-hidden",folded);root.Q<Button>(name+"-fold").text=folded?"▼":"▲";root.Q<Button>(name+"-fold").tooltip=folded?"펼치기":"접기";});
        }
        void OnDestroy(){quantity?.Dispose();Minimap?.Dispose();}
        void Bind(string name,System.Action action)
        {var b=root.Q<Button>(name);b.focusable=false;b.clicked+=()=>{quantity.EndEdit();action();Refresh();};}
        public void Refresh()
        {
            if(root==null || Inventory==null)return;
            bool editing=Input.IsEditing;
            var persistence=Sandbox.Persistence;bool locked=Sandbox.Content.Defeated||persistence?.Blocked==true;
            root.Q("save-controls").style.display=persistence==null?DisplayStyle.None:DisplayStyle.Flex;
            root.Q("new-game-confirmation").style.display=confirmingNewGame?DisplayStyle.Flex:DisplayStyle.None;
            if(persistence!=null){bool eligible=persistence.CanSave(out var why);root.Q<Label>("save-status").text=persistence.Status+"\n마지막 저장: "+(persistence.LastSavedUtc??"없음")+(eligible?"":"\n"+why);root.Q<Button>("save").SetEnabled(eligible);root.Q<Button>("load").SetEnabled(persistence.HasContinue);root.Q<Button>("save").style.display=locked?DisplayStyle.None:DisplayStyle.Flex;root.Q<Button>("load").style.display=locked?DisplayStyle.None:DisplayStyle.Flex;}
            root.Q("clock-dev").SetEnabled(!locked);root.Q<Button>("reset").SetEnabled(!locked);

            progressView?.Refresh(editing);
            clockView?.Refresh(Sandbox.Clock,editing);
            root.Q<Button>("edit").EnableInClassList("selected",editing);root.Q<Button>("confirm").SetEnabled(editing);
            root.Q<Button>("edit").SetEnabled(!locked);
            root.Q<Button>("spawn").SetEnabled(!editing&&!locked);
            bool canControl=actions.CanInteract(out var controlReason);
            run.SetEnabled(canControl);run.tooltip=controlReason;
            root.Q<Button>("clock-pause").SetEnabled(canControl);root.Q<Button>("clock-pause").tooltip=controlReason;
            root.Q<Button>("clock-pause").text=actions.ProgressLabel;
            root.Q<Button>("inventory-toggle").text=Inventory.Visible?"건물 인벤토리 접기":"건물 인벤토리 펼치기";
            root.Q<Button>("inventory-fold").text=Inventory.Visible?"▼":"▲";
            root.Q<Button>("inventory-fold").tooltip=Inventory.Visible?"카탈로그 접기":"카탈로그 펼치기";
            root.Q("confirm").EnableInClassList("is-hidden",!editing);root.Q("cancel").EnableInClassList("is-hidden",!editing);
            root.Q<Button>("cancel").SetEnabled(editing);
            run.EnableInClassList("is-hidden",Sandbox.Assault!=null);
            root.Q<Button>("resources-all").text=stockView.ShowAll?"주요 자원만 보기":"전체 자원 보기";
            root.Q<Label>("pending").text=editing?$"수정 중 · 임시 작업 {Input.Edits?.Count??0}개":"건물을 선택해 정보를 확인하세요. 설치·회수는 수정 모드에서 진행합니다.";
            message.text=Sandbox.Message;
            var placementHint=root.Q<Label>("placement-hint");placementHint.text=Input.PlacementHint;placementHint.EnableInClassList("is-hidden",string.IsNullOrEmpty(Input.PlacementHint));
            stockView?.Refresh();
            var selected=Input.SelectedContent??Input.SelectedTower?.building;if(selected?.Disposed==true)selected=null;
            powerView?.Refresh(selected);selectedView.Refresh(selected,editing);
            var main=Sandbox.Content.MainBase;var mainHealth=main?.Module<HealthModule>();
            bool hasHealth=mainHealth!=null&&main.Active&&!main.Disposed;
            root.Q<Label>("main-health").text=hasHealth?$"{mainHealth.Current:0.#} / {mainHealth.Maximum:0.#}":Sandbox.Content.Defeated?"메인 기지 파괴":main!=null&&main.Active&&!main.Disposed?main.DisplayName:"메인 기지 없음";
            var bar=root.Q<ProgressBar>("main-health-bar");bar.EnableInClassList("is-hidden",!hasHealth);if(hasHealth)bar.value=100*mainHealth.Current/mainHealth.Maximum;
            var upgradeState=actions.ReadUpgrade();var upgradeButton=root.Q<Button>("upgrade");
            upgradeButton.SetEnabled(upgradeState.Available);upgradeButton.tooltip=upgradeState.Reason;
            upgradeButton.text=upgradeState.Available?$"강화 Lv.{upgradeState.Level} → {upgradeState.Level+1}":"선택 건물 강화";
            root.Q<Label>("upgrade-status").text=upgradeState.Available?(upgradeState.VerificationFree?"검증용 무료":"설정된 강화 비용 적용"):upgradeState.Reason;
            counts.text=Sandbox.Enemies==null?"":$"토대 {Sandbox.Foundations.Platforms.Count} · 포탑 {Sandbox.Towers.Count}\n적 {Sandbox.Enemies.Alive:N0} / {Sandbox.Enemies.MaxCount:N0} · 처치 {Sandbox.Enemies.Killed:N0}\n소환 대기 {Sandbox.SpawnStream.Pending:N0} · 적은 도착 후 건물 공격";
            mode.text=actions.Mode;
            run.text=actions.RunLabel;
        }
        public static bool IsShown(VisualElement element)
        {
            if(element?.panel==null)return false;
            for(var current=element;current!=null;current=current.parent)
                if(current.ClassListContains("is-hidden")||current.ClassListContains("minimap-hidden")||current.resolvedStyle.display==DisplayStyle.None||current.resolvedStyle.visibility==Visibility.Hidden)return false;
            return true;
        }
        public bool IsPointerOverHud(Vector2 pointer)
        {
            if(root?.panel==null)return false;
            for(var hit=root.panel.Pick(pointer);hit!=null;hit=hit.parent)
                if(hit.ClassListContains("hud-surface")&&IsShown(hit))return true;
            return false;
        }
        void Update()
        {
            if(root?.panel==null||Inventory==null)return;var mouse=Mouse.current;
            var pointer=mouse==null?Vector2.negativeInfinity:RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(mouse.position.x.ReadValue(),Screen.height-mouse.position.y.ReadValue()));
            workspace.EnableInClassList("compact",workspace.layout.height<760||workspace.layout.width<1200);
            bool over=IsPointerOverHud(pointer);
            Minimap?.Refresh(Time.unscaledTimeAsDouble);over|=Minimap?.Interacting==true;
            Input.PointerOverUI=over;Sandbox.CameraRig.BlockPointer=over||Input.Dragging;
            if(!IsShown(amount)||(mouse!=null&&mouse.leftButton.wasPressedThisFrame&&!amount.worldBound.Contains(pointer)))quantity.EndEdit();
            if(Keyboard.current?.escapeKey.wasPressedThisFrame??false)quantity.EndEdit();
            var mapPanel=root.Q("minimap-panel");
            Sandbox.CameraRig.BlockKeyboard=quantity.Editing||Input.Dragging||Minimap?.Interacting==true||(IsShown(mapPanel)&&mapPanel.worldBound.Contains(pointer));Refresh();
        }
    }
}
