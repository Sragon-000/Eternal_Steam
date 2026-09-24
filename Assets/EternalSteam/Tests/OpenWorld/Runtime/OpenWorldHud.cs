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
        VisualElement root,panel,testPanel;
        TextField amount;
        SpawnQuantityInput quantity;
        public BuildingInventoryView Inventory {get;private set;}
        Label message,counts,mode;
        Button run;
        WorldClockView clockView;
        ResourceStockView stockView;
        MapProgressView progressView;
        BasePowerView powerView;
        public OpenWorldMinimap Minimap {get;private set;}
        bool confirmingNewGame;
        void Start()
        {
            root=GetComponent<UIDocument>().rootVisualElement;panel=root.Q("panel");testPanel=root.Q("test-panel");amount=root.Q<TextField>("amount");
            quantity=new SpawnQuantityInput(root,amount);
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
            BindFold("resource");BindFold("clock");BindFold("test");
            root.Q<Toggle>("spawn-air").RegisterValueChangedCallback(e=>Sandbox.SpawnAir=e.newValue);
            Bind("spawn",()=>{if(!Input.IsEditing && Sandbox.Spawn(amount.value)){var m=Sandbox.Message;Input.Cancel();Sandbox.Message=m;}});
            Bind("reset",()=>Sandbox.ResetEnemies());
            Bind("upgrade",()=>{if(Input.IsEditing||Sandbox.Content.Defeated||Sandbox.Persistence?.Blocked==true)return;var building=Input.SelectedContent??Input.SelectedTower?.building;var upgrade=building?.Module<IUpgradeControl>();if(upgrade==null){Sandbox.Message="일반 상태에서 강화할 건물을 선택하세요.";return;}string reason;bool ok=Sandbox.Persistence!=null?Sandbox.Persistence.Upgrades.TryUpgrade(building,out reason):upgrade.TryUpgrade(out reason);Sandbox.Message=ok?$"강화 Lv.{upgrade.Level} · 검증용 무료":reason;if(ok)Sandbox.Persistence?.RequestAutoSave();});
            Bind("save",()=>Sandbox.Persistence?.Save());
            Bind("load",()=>Sandbox.Persistence?.ContinueSaved());
            Bind("new-game",()=>confirmingNewGame=true);
            Bind("new-game-confirm",()=>Sandbox.Persistence?.NewGame());
            Bind("new-game-cancel",()=>confirmingNewGame=false);
            Bind("run",()=>{if(Input.IsEditing||Sandbox.Content.Defeated||Sandbox.Persistence?.Blocked==true)return;bool next=Sandbox.Assault!=null?Sandbox.Clock.Paused:!Sandbox.Running;Input.Cancel();Sandbox.Running=next;if(Sandbox.Assault!=null)Sandbox.Clock.Paused=!next;});
            Minimap=new OpenWorldMinimap(root,Sandbox);
            stockView=new ResourceStockView(root.Q<Label>("resources"),Sandbox.Content.Resources,Sandbox.ContentCatalog);
            clockView=new WorldClockView(root);
            progressView=new MapProgressView(root,Sandbox);powerView=new BasePowerView(root,Sandbox);
            root.Q("clock-dev").style.display=(Application.isEditor||Debug.isDebugBuild)?DisplayStyle.Flex:DisplayStyle.None;
            Bind("clock-day",()=>{Sandbox.Clock.SetPhase(DayPhase.Day);Sandbox.Persistence?.Changed();});
            Bind("clock-night",()=>{Sandbox.Clock.SetPhase(DayPhase.Night);Sandbox.Persistence?.Changed();});
            Bind("clock-pause",()=>Sandbox.Clock.Paused=!Sandbox.Clock.Paused);
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
            Bind(name+"-fold",()=>{bool folded=!container.ClassListContains("folded");container.EnableInClassList("folded",folded);body.style.display=folded?DisplayStyle.None:DisplayStyle.Flex;root.Q<Button>(name+"-fold").text=folded?"▼ 펼치기":"▲ 접기";});
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
            root.Q<Button>("spawn").SetEnabled(!editing&&!locked);run.SetEnabled(!editing&&!locked);
            root.Q<Button>("inventory-toggle").text=Inventory.Visible?"건물 인벤토리 접기":"건물 인벤토리 펼치기";
            root.Q<Button>("inventory-fold").text=Inventory.Visible?"▼ 접기":"▲ 펼치기";
            root.Q<Label>("pending").text=$"임시 작업 {Input.Edits?.Count??0}개 · 3D XZ 격자 / Y축 45°";
            message.text=Sandbox.Message;
            stockView?.Refresh();
            var selected=Input.SelectedContent??Input.SelectedTower?.building;if(selected?.Disposed==true)selected=null;var weapon=selected?.Module<WeaponRuntime>();powerView?.Refresh(selected);
            root.Q<Label>("content-stats").text=weapon!=null&&!selected.Disposed?$"{selected.DisplayName} Lv.{selected.Module<IUpgradeControl>()?.Level??1}\n피해 {weapon.Damage:0.#} · 사거리 {weapon.Range:0.#}m · 간격 {weapon.Interval:0.##}초":selected!=null?selected.DisplayName:$"신규 콘텐츠 검증 · 기지 Lv.{Sandbox.Content.LevelCap}";
            if(Sandbox.Content.BaseRules){int subCount=0;foreach(var context in Sandbox.Content.Bases.Bases.Values)if(context.Nexus.Module<IBaseRole>()?.Role==BaseRole.Sub)subCount++;
                root.Q<Label>("content-stats").text+=$"\n메인 기지 Lv.{Sandbox.Content.LevelCap} · 서브 {subCount}/{Sandbox.Content.SubLimit}";
                if(selected?.Module<IBaseRole>() is IBaseRole role&&selected.Module<IBuildArea>() is IBuildArea area)root.Q<Label>("content-stats").text+=$"\n{(role.Role==BaseRole.Main?"메인":"서브")} Lv.{selected.Module<IUpgradeControl>()?.Level??1} · 설치 범위 {area.Radius:0}×{area.Radius:0}칸";
            }
            if(selected?.Module<HealthModule>() is HealthModule health)root.Q<Label>("content-stats").text+=$"\n체력 {health.Current:0.#} / {health.Maximum:0.#}";
            if(selected?.Module<ITurretRotation>() is ITurretRotation rotation)root.Q<Label>("content-stats").text+=$"\n회전 {rotation.DegreesPerSecond:0.#}°/초";
            if(Sandbox.MeetingConstructionRules) {
                var chosen=selected;
                var label=root.Q<Label>("content-stats");
                string owner=chosen?.OwnerBaseId;
                string state=chosen==null?"":chosen.Operational?"가동 중":(chosen.OperationBlock.HasFlag(OperationBlock.BaseLost)?"기지 없음 / 상실":"유효 범위 밖")+" · 비작동";
                label.text+=(chosen!=null?"\n"+state:"")+"\n소속 기지 "+(owner==null?"미지정":owner.Substring(0,6));
                var baseId=Sandbox.Content.Bases.SelectedBaseId;
                label.text+="\n다음 배치 기지 "+(baseId==null?"없음":baseId.Substring(0,6));
                if(chosen?.Module<IBaseIdentity>()!=null && Sandbox.Regions!=null)foreach(var region in Sandbox.Regions)if(region!=null&&region.Contains(chosen.Position)){label.text+="\n"+region.DisplayName+" · 예정 자원 "+region.ResourceId;break;}
            }
            root.Q<Button>("upgrade").SetEnabled(!locked&&!editing&&selected?.Module<IUpgradeControl>()!=null);
            counts.text=Sandbox.Enemies==null?"":$"토대 {Sandbox.Foundations.Platforms.Count} · 포탑 {Sandbox.Towers.Count}\n적 {Sandbox.Enemies.Alive:N0} / {Sandbox.Enemies.MaxCount:N0} · 처치 {Sandbox.Enemies.Killed:N0}\n소환 대기 {Sandbox.SpawnStream.Pending:N0} · 적은 도착 후 건물 공격";
            mode.text=Sandbox.Content.Defeated?"메인 기지 파괴 · 공략 실패":editing?"수정 중":Sandbox.Clock.Paused?"일시정지":Sandbox.Running?"전투 실행 중":"준비";
            run.text=Sandbox.Assault!=null?(Sandbox.Clock.Paused?"계속 진행":"일시정지"):(Sandbox.Running?"일시정지":"전투 실행");
        }
        void Update()
        {
            if(root?.panel==null||Inventory==null)return;var mouse=Mouse.current;
            var pointer=mouse==null?Vector2.negativeInfinity:RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(mouse.position.x.ReadValue(),Screen.height-mouse.position.y.ReadValue()));
            // The tabs protrude above the body; include their bounds in UI hit blocking.
            bool over=((Inventory.Visible&&panel.worldBound.Contains(pointer))||root.Q("categories").parent.worldBound.Contains(pointer))||testPanel.worldBound.Contains(pointer)||root.Q("clock-panel").worldBound.Contains(pointer)||root.Q("resource-panel").worldBound.Contains(pointer);
            over|=Minimap?.Interacting==true;Minimap?.Refresh(Time.unscaledTimeAsDouble);
            Input.PointerOverUI=over;Sandbox.CameraRig.BlockPointer=over||Input.Dragging;
            if(mouse!=null&&mouse.leftButton.wasPressedThisFrame&&!amount.worldBound.Contains(pointer))quantity.EndEdit();
            if(Keyboard.current?.escapeKey.wasPressedThisFrame??false)quantity.EndEdit();
            Sandbox.CameraRig.BlockKeyboard=quantity.Editing||Input.Dragging||Minimap?.Interacting==true;Refresh();
        }
    }
}
