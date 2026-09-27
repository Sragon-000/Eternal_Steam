using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using EternalSteam.Demo;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace EternalSteam.OpenWorld
{
    // Presentation objects and persistent button callbacks are authored in the scene.
    // This component only updates values, visibility and game commands.
    [DefaultExecutionOrder(-100)]
    public sealed class CanvasWorldHud:MonoBehaviour
    {
        [Serializable] public struct TextBinding {public string Id;public TMP_Text View;}
        [Serializable] public struct ButtonBinding {public string Id;public UnityEngine.UI.Button View;}
        [Serializable] public struct Section {public string Id;public GameObject Body;}
        [Serializable] public struct CatalogEntry {public BuildingDefinition Definition;public WorldTool Tool;public HordeTowerKind Kind;public BuildingCategory Category;public UnityEngine.UI.Button View;}
        public OpenWorldSandbox Sandbox;public OpenWorldInput Input;
        public TextBinding[] Texts;public ButtonBinding[] Buttons;public Section[] Sections;public CatalogEntry[] Catalog;
        public UnityEngine.UI.GridLayoutGroup CatalogLayout;
        public TMP_InputField Amount;public CanvasWorldMinimap Minimap;public UnityEngine.UI.Image HealthFill;
        public RectTransform ClockHand;public UnityEngine.UI.GraphicRaycaster Raycaster;
        public string[] ResourcePriority=Array.Empty<string>();
        readonly Dictionary<string,TMP_Text> texts=new();readonly Dictionary<string,UnityEngine.UI.Button> buttons=new();readonly Dictionary<string,GameObject> sections=new();
        readonly List<RaycastResult> hits=new();readonly List<ProductionModuleDefinition> resources=new();readonly List<string> resourceNames=new();readonly StringBuilder buffer=new();
        OpenWorldHudActions actions;PointerEventData pointer;bool showAll=true,hasPriority;int category=-1;
        void Start()
        {
            foreach(var b in Texts)texts.Add(b.Id,b.View);foreach(var b in Buttons)buttons.Add(b.Id,b.View);foreach(var b in Sections)sections.Add(b.Id,b.Body);
            actions=new OpenWorldHudActions(Sandbox,Input);Minimap.Initialize(Sandbox);
            var ids=new HashSet<string>();if(Sandbox.ContentCatalog!=null)foreach(var d in Sandbox.ContentCatalog.Buildings)foreach(var module in d.Modules)
                if(module is ProductionModuleDefinition recipe&&ids.Add(recipe.OutputId)){resources.Add(recipe);resourceNames.Add(d.DisplayName.Replace(" 생성기",""));}
            foreach(var r in resources)if(Array.IndexOf(ResourcePriority,r.OutputId)>=0){hasPriority=true;break;}showAll=!hasPriority;Refresh();
        }
        void Text(string id,string value){if(texts.TryGetValue(id,out var t)&&t.text!=value)t.text=value;}
        void Show(string id,bool visible){if(sections.TryGetValue(id,out var go)&&go.activeSelf!=visible)go.SetActive(visible);}
        void Enable(string id,bool enabled){if(buttons.TryGetValue(id,out var b))b.interactable=enabled;}
        void Caption(string id,string value){Text(id+"-caption",value);}
        void EndTyping(){if(Amount!=null&&Amount.isFocused)Amount.DeactivateInputField();EventSystem.current?.SetSelectedGameObject(null);}
        public void Execute(string command)
        {
            if(actions==null)return;EndTyping();
            if(command.StartsWith("fold:")){var id=command.Substring(5);if(sections.TryGetValue(id,out var body)){if(id=="minimap")Minimap.CancelInteraction();body.SetActive(!body.activeSelf);}return;}
            if(command.StartsWith("category:")){category=int.Parse(command.Substring(9));RefreshCatalog();return;}
            if(command.StartsWith("build:")){
                int index=int.Parse(command.Substring(6));if(!Input.IsEditing){Sandbox.Message="수정 버튼을 누른 뒤 건물을 선택하세요.";return;}
                var entry=Catalog[index];if(entry.Definition!=null)Input.SelectContent(entry.Definition);else Input.Select(entry.Tool,entry.Kind);Refresh();return;
            }
            switch(command){
                case "edit":if(Input.IsEditing)Input.Cancel();else Input.BeginEditing();break;
                case "confirm":if(Input.IsEditing){if((Input.Edits?.Count??0)==0)Input.Cancel();else Input.Confirm();}break;
                case "cancel":Input.Cancel();break;
                case "selection-close":Input.ClearSelection();break;
                case "upgrade":actions.TryUpgrade();break;
                case "pause":actions.ToggleProgress();break;
                case "run":actions.ToggleRun();break;
                case "spawn":if(!Input.IsEditing)Sandbox.Spawn(Amount.text);break;
                case "spawn-air":Sandbox.SpawnAir=!Sandbox.SpawnAir;break;
                case "reset":Sandbox.ResetEnemies();break;
                case "day":if(actions.CanInteract(out _)){Sandbox.Clock.SetPhase(DayPhase.Day);Sandbox.Persistence?.Changed();}break;
                case "night":if(actions.CanInteract(out _)){Sandbox.Clock.SetPhase(DayPhase.Night);Sandbox.Persistence?.Changed();}break;
                case "save":Sandbox.Persistence?.Save();break;
                case "load":Sandbox.Persistence?.ContinueSaved();break;
                case "new-game":Show("new-game-confirmation",true);break;
                case "new-game-confirm":Sandbox.Persistence?.NewGame();break;
                case "new-game-cancel":Show("new-game-confirmation",false);break;
                case "resources-all":showAll=!hasPriority||!showAll;break;
                case "orb-craft":if(actions.CanInteract(out _)&&Sandbox.Assault!=null){Sandbox.Message=Sandbox.Assault.Craft()?"완벽한 에너지 오브 제작 · 맵 클리어":"보스 보상과 해당 맵 에너지가 필요합니다.";Sandbox.Persistence?.RequestAutoSave();}break;
            }
            Refresh();
        }
        void RefreshCatalog()
        {
            bool locked=Sandbox.Content.Defeated||Sandbox.Persistence?.Blocked==true;
            if(CatalogLayout!=null){float width=((RectTransform)CatalogLayout.transform).rect.width;var size=new Vector2(Mathf.Max(40,(width-30)/6),60);if(CatalogLayout.cellSize!=size)CatalogLayout.cellSize=size;}
            foreach(var entry in Catalog){entry.View.gameObject.SetActive(category<0||(int)entry.Category==category);entry.View.interactable=Input.IsEditing&&!locked;}
        }
        public void Refresh()
        {
            if(actions==null)return;bool editing=Input.IsEditing,locked=Sandbox.Content.Defeated||Sandbox.Persistence?.Blocked==true;
            Text("message",Sandbox.Message);Text("placement-hint",Input.PlacementHint);Show("placement-hint",!string.IsNullOrEmpty(Input.PlacementHint));Text("mode",actions.Mode);
            var clock=Sandbox.Clock;long seconds=(long)Math.Ceiling(clock.RemainingSeconds);
            Text("date",$"{clock.Day}일차 · {(clock.Phase==DayPhase.Day?"낮":"밤")}");Text("remaining",$"전환까지 {seconds/60:00}:{seconds%60:00}"+(editing?" · 수정 중 정지":clock.Paused?" · 시간 정지":""));
            if(ClockHand!=null)ClockHand.localRotation=Quaternion.Euler(0,0,-360*(float)((clock.Phase==DayPhase.Day?0:.5)+clock.PhaseProgress*.5));
            Caption("pause",actions.ProgressLabel);Caption("run",actions.RunLabel);Caption("edit",editing?"수정 종료":"수정");Caption("spawn-air",Sandbox.SpawnAir?"공중 적: 켬":"공중 적: 끔");
            bool can=actions.CanInteract(out _);Enable("pause",can);Enable("run",can);Enable("edit",!locked);Enable("spawn",!editing&&!locked);Enable("reset",!locked);Enable("day",can);Enable("night",can);Enable("spawn-air",!locked);
            Show("edit-actions",editing);Show("run",Sandbox.Assault==null);Show("developer",Application.isEditor||Debug.isDebugBuild);
            Text("pending",editing?$"수정 중 · 임시 작업 {Input.Edits?.Count??0}개":"건물 선택: 상세 정보 · 수정: 설치와 회수");RefreshCatalog();
            var main=Sandbox.Content.MainBase;var hp=main?.Module<HealthModule>();bool hasHp=main!=null&&main.Active&&!main.Disposed&&hp!=null;
            Text("main-health",hasHp?$"{hp.Current:0.#} / {hp.Maximum:0.#}":Sandbox.Content.Defeated?"메인 기지 파괴":"메인 기지 없음");float healthRatio=hasHp?Mathf.Clamp01(hp.Current/Mathf.Max(1,hp.Maximum)):0;HealthFill.rectTransform.anchorMax=new Vector2(healthRatio,1);
            buffer.Clear();for(int i=0;i<resources.Count;i++){var r=resources[i];if(!showAll&&Array.IndexOf(ResourcePriority,r.OutputId)<0)continue;buffer.Append(resourceNames[i]).Append("  ").Append(Sandbox.Content.Resources.Amount(r.OutputId).ToString("N0")).Append('\n');}
            Text("resources",buffer.Length>0?buffer.ToString():"등록된 생산 자원이 없습니다.");Caption("resources-all",showAll?"주요 자원":"전체 자원");Enable("resources-all",hasPriority);
            var selected=Input.SelectedContent??Input.SelectedTower?.building;if(selected?.Disposed==true)selected=null;
            Show("selection",selected!=null&&selected.Active&&!editing);if(selected!=null)RefreshSelection(selected);
            RefreshPower(selected);RefreshProgress(editing,locked);
            var p=Sandbox.Persistence;Show("save-controls",p!=null);if(p!=null){bool save=p.CanSave(out var reason);Text("save-status",p.Status+"\n마지막 저장: "+(p.LastSavedUtc??"없음")+(save?"":"\n"+reason));Enable("save",save);Enable("load",p.HasContinue&&!locked);}
            Text("counts",$"토대 {Sandbox.Foundations.Platforms.Count} · 포탑 {Sandbox.Towers.Count}\n적 {Sandbox.Enemies.Alive:N0} / {Sandbox.Enemies.MaxCount:N0} · 처치 {Sandbox.Enemies.Killed:N0}\n소환 대기 {Sandbox.SpawnStream.Pending:N0}");
        }
        void RefreshSelection(BuildingInstance b)
        {
            var level=b.Module<IUpgradeControl>();Text("selection-title",b.DisplayName+(level==null?"":$" · Lv.{level.Level}/{level.MaximumLevel}"));buffer.Clear();
            buffer.AppendLine(b.Operational?"가동 중":b.OperationBlock.HasFlag(OperationBlock.BaseLost)?"기지 없음 / 상실 · 비작동":"유효 범위 밖 · 비작동");
            var hp=b.Module<HealthModule>();if(hp!=null)buffer.AppendLine($"체력 {hp.Current:0.#} / {hp.Maximum:0.#}");
            var weapon=b.Module<WeaponRuntime>();if(weapon!=null)buffer.AppendLine($"피해 {weapon.Damage:0.#}\n사거리 {weapon.Range:0.#}m\n공격 간격 {weapon.Interval:0.##}초");
            var rotation=b.Module<ITurretRotation>();if(rotation!=null)buffer.AppendLine($"회전 {rotation.DegreesPerSecond:0.#}°/초");
            var area=b.Module<IBuildArea>();if(area!=null)buffer.AppendLine(area.Shape==BuildAreaShape.Square?$"가동 영역 {area.Radius:0.#} × {area.Radius:0.#}칸":$"가동 영역 반경 {area.Radius:0.#}m");
            if(Sandbox.MeetingConstructionRules)buffer.AppendLine("소속 기지 "+Short(b.OwnerBaseId));
            if(b.Module<IBaseIdentity>()!=null&&Sandbox.Regions!=null)foreach(var r in Sandbox.Regions)if(r!=null&&r.Contains(b.Position)){buffer.AppendLine(r.DisplayName+" · 예정 자원 "+r.ResourceId);break;}
            Text("selection-stats",buffer.ToString());var state=actions.ReadUpgrade();Enable("upgrade",state.Available);Show("upgrade",level!=null);
            Caption("upgrade",state.Available?$"강화 Lv.{state.Level} → {state.Level+1}":"선택 건물 강화");Text("upgrade-status",state.Available?(state.VerificationFree?"검증용 무료":"설정된 강화 비용 적용"):state.Reason);
        }
        static string Short(string id)=>id==null?"없음":id.Substring(0,Math.Min(6,id.Length));
        void RefreshPower(BuildingInstance selected)
        {
            var device=selected?.Module<PowerModule>();var storage=device?.Role==PowerRole.Storage?device:device?.Supply;
            if(storage==null&&Sandbox.Content.Bases.SelectedBaseId is string id&&Sandbox.Content.Bases.Bases.TryGetValue(id,out var context))storage=context.Nexus.Module<PowerModule>();
            Text("power",storage==null?"연결된 기지 없음":$"{storage.Owner.DisplayName} [{Short(storage.Owner.OwnerBaseId)}]\n{storage.Stored:0.#} / {storage.Capacity:0.#}");
            buffer.Clear();if(storage!=null)buffer.AppendLine($"생산 +{storage.Production:0.#}/s · 요청 {storage.Requested:0.#}/s\n실제 소비 {storage.Consumed:0.#}/s");
            if(Sandbox.Content.BaseRules){int subs=0;foreach(var b in Sandbox.Content.Bases.Bases.Values)if(b.Nexus.Module<IBaseRole>()?.Role==BaseRole.Sub)subs++;buffer.AppendLine($"메인 Lv.{Sandbox.Content.LevelCap} · 서브 {subs}/{Sandbox.Content.SubLimit}");}
            if(Sandbox.MeetingConstructionRules)buffer.AppendLine("다음 배치 기지 "+Short(Sandbox.Content.Bases.SelectedBaseId));
            if(device!=null&&device.Role!=PowerRole.Storage)buffer.AppendLine($"전력 {(device.Role==PowerRole.Producer?"생산":"소비")} {device.Rate:0.#}/s · "+(device.Supply==null?"연결 기지 없음":device.Supplied?"공급 중":"공급 대기"));Text("power-details",buffer.ToString());
        }
        void RefreshProgress(bool editing,bool locked)
        {
            var a=Sandbox.Assault;Show("map-progress",a!=null);if(a==null)return;var e=a.Energy;
            Text("energy",$"{a.EnergyName} · 확보 {100d*e.Earned/e.Target:0.0}%\n추출 {e.Extracted/1000d:0.##} · 처치 {e.KillReward/1000d:0.##} · 보스 {e.BossReward/1000d:0.##}\n잔고 {e.Balance/1000d:0.##} / 목표 {e.Target/1000d:0.##}");
            Text("stage",(e.Stage switch {MapStage.Gathering=>"기지 영역에서 에너지 추출 중",MapStage.WaitingForNight=>"90% 확보 · 오늘 밤 보스 출현",MapStage.BossBattle=>a.BossSpawnFailure??$"보스전 · 체력 {a.BossHealth}",MapStage.AwaitingCraft=>"불완전 오브 획득 · 제작 가능",MapStage.Cleared=>"맵 클리어",_=>"메인 기지 상실"})+$"\n소환 지점 {a.Planner.Points.Count} · 야간 대기 {a.Planner.Pending:N0}");Enable("orb-craft",!editing&&!locked&&e.Stage==MapStage.AwaitingCraft);
        }
        void Update()
        {
            if(actions==null)return;var mouse=Mouse.current;bool over=false;
            if(mouse!=null&&EventSystem.current!=null){pointer??=new PointerEventData(EventSystem.current);pointer.position=mouse.position.ReadValue();hits.Clear();Raycaster.Raycast(pointer,hits);over=hits.Count>0;}
            Input.PointerOverUI=over||Minimap.Interacting;Sandbox.CameraRig.BlockPointer=Input.PointerOverUI||Input.Dragging;
            Sandbox.CameraRig.BlockKeyboard=Amount.isFocused||Input.Dragging||Minimap.Interacting||(mouse!=null&&Minimap.gameObject.activeInHierarchy&&RectTransformUtility.RectangleContainsScreenPoint(Minimap.rectTransform,mouse.position.ReadValue()));
            if(Keyboard.current?.escapeKey.wasPressedThisFrame??false)EndTyping();Refresh();
        }
        void OnDisable(){if(Input!=null)Input.PointerOverUI=false;if(Sandbox?.CameraRig!=null){Sandbox.CameraRig.BlockPointer=false;Sandbox.CameraRig.BlockKeyboard=false;}Minimap?.CancelInteraction();}
    }
}
