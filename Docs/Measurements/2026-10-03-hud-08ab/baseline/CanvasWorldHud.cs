using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using EternalSteam.Demo;
using EternalSteam.Railway;
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
        [Serializable] public struct CatalogEntry {public BuildingDefinition Definition;public WorldTool Tool;public HordeTowerKind Kind;public BuildingCategory Category;public UnityEngine.UI.Button View;public UnityEngine.UI.Image Icon;}
        [Serializable] public struct ResourceIconBinding {public string Id;public RectTransform View;}
        [Serializable] public struct PortraitBinding {public string DefinitionId;public Sprite Sprite;}
        public ResourceIconBinding[] ResourceIcons=Array.Empty<ResourceIconBinding>();
        public PortraitBinding[] Portraits=Array.Empty<PortraitBinding>();
        public Sprite CardFrame,SelectedFrame,CategoryFrame;
        public UnityEngine.UI.Image SelectionPortrait,SelectionHealthFill,PowerFill;
        public OpenWorldSandbox Sandbox;public OpenWorldInput Input;
        public TextBinding[] Texts;public ButtonBinding[] Buttons;public Section[] Sections;public CatalogEntry[] Catalog;
        public UnityEngine.UI.GridLayoutGroup CatalogLayout;
        public CanvasHudLayout Layout;
        public bool ShowDevelopmentControls;
        public HudModeGroups Groups;
        public BaseResourceAccordion BaseResources;
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
        void Text(string id,string value){value=value?.Replace("검증비 ","").Replace(" · 검증용","");if(texts.TryGetValue(id,out var t)&&t.text!=value)t.text=value;}
        void Show(string id,bool visible){if(sections.TryGetValue(id,out var go)&&go.activeSelf!=visible)go.SetActive(visible);}
        void Enable(string id,bool enabled){if(buttons.TryGetValue(id,out var b))b.interactable=enabled;}
        void Caption(string id,string value){Text(id+"-caption",value);}
        void EndTyping(){if(Amount!=null&&Amount.isFocused)Amount.DeactivateInputField();EventSystem.current?.SetSelectedGameObject(null);}
        public void Execute(string command)
        {
            if(actions==null)return;EndTyping();
            if(Sandbox.RailwayHud?.HasDraft==true&&(command=="edit"||command.StartsWith("build:"))){Sandbox.Message="철도 연결·노선 편집을 먼저 확정하거나 닫아 주세요.";return;}
            if(command.StartsWith("fold:")){var id=command.Substring(5);if(sections.TryGetValue(id,out var body)){if(id=="minimap")Minimap.CancelInteraction();body.SetActive(!body.activeSelf);Layout?.Apply(true);}return;}
            if(command.StartsWith("category:")){category=int.Parse(command.Substring(9));RefreshCatalog();return;}
            if(command.StartsWith("build:")){
                int index=int.Parse(command.Substring(6));if(index<0||index>=Catalog.Length)return;
                if(Sandbox.Content.Defeated||Sandbox.Persistence?.Blocked==true)return;
                if(!Input.IsEditing)Input.BeginEditing();
                var entry=Catalog[index];if(entry.Definition!=null)Input.SelectContent(entry.Definition);else Input.Select(entry.Tool,entry.Kind);Refresh();return;
            }
            switch(command){
                case "edit":if(Input.IsEditing&&(Input.Edits?.Count??0)>0){Sandbox.Message="대기 작업은 확정 또는 전체 취소로 마무리하세요.";break;}if(Input.IsEditing)Input.Cancel();else Input.BeginEditing();break;
                case "confirm":if(Input.IsEditing){if((Input.Edits?.Count??0)==0)Input.Cancel();else Input.Confirm();}break;
                case "cancel":Input.Cancel();break;
                case "selection-close":Input.ClearSelection();break;
                case "upgrade":actions.TryUpgrade();break;
                case "station-railway":if(!Input.IsEditing&&Input.SelectedContent?.Module<RailFacility>()?.Kind==RailFacilityKind.Station)Sandbox.RailwayHud?.OpenFromStation(Input.SelectedContent.PersistentId);break;
                case "pause":actions.ToggleProgress();break;
                case "run":actions.ToggleRun();break;
                case "spawn":if(!Input.IsEditing)Sandbox.Spawn(Amount.text);break;
                case "spawn-air":Sandbox.SpawnAir=!Sandbox.SpawnAir;break;
                case "reset":Sandbox.ResetEnemies();break;
                case "infinite-resources":if(!Sandbox.Content.Defeated&&Sandbox.Persistence?.Blocked!=true){Sandbox.Content.InfiniteResources=!Sandbox.Content.InfiniteResources;Sandbox.Message="테스트 · 자원 무한 "+(Sandbox.Content.InfiniteResources?"ON":"OFF");}break;
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
            foreach(var entry in Catalog){
                bool available=entry.Definition==null||Sandbox.Content.MeetsBaseLevel(entry.Definition,out _);
                bool visible=available&&(category<0||(category==4?entry.Definition?.Id.StartsWith("railway.")==true:(int)entry.Category==category));
                if(entry.View.gameObject.activeSelf!=visible)entry.View.gameObject.SetActive(visible);
                entry.View.interactable=available&&!locked&&Sandbox.RailwayHud?.HasDraft!=true;
                bool selected=Input.IsEditing&&(entry.Definition!=null?ReferenceEquals(Input.SelectedDefinition,entry.Definition):Input.Tool==entry.Tool&&(entry.Tool!=WorldTool.Tower||Input.Kind==entry.Kind));
                if(entry.View.targetGraphic is UnityEngine.UI.Image frame&&CardFrame!=null)frame.sprite=selected?SelectedFrame:CardFrame;
            }
            for(int i=-1;i<5;i++)if(buttons.TryGetValue("category-"+i,out var tab)&&tab.targetGraphic is UnityEngine.UI.Image frame&&CardFrame!=null){
                frame.sprite=category==i?CategoryFrame:CardFrame;
                if(texts.TryGetValue("category-"+i+"-caption",out var label))label.color=category==i?new Color(1,.82f,.38f):new Color(.7f,.8f,.84f);
            }
        }
        public void Refresh()
        {
            if(actions==null)return;bool editing=Input.IsEditing,locked=Sandbox.Content.Defeated||Sandbox.Persistence?.Blocked==true;
            Text("message",Sandbox.Message);Text("placement-hint",Input.PlacementHint);Show("placement-hint",!string.IsNullOrEmpty(Input.PlacementHint));Text("mode",actions.Mode);
            var clock=Sandbox.Clock;long seconds=(long)Math.Ceiling(clock.RemainingSeconds);
            Text("date",$"{clock.Day}일차 · {(clock.Phase==DayPhase.Day?"낮":"밤")}");Text("remaining",$"{(clock.Phase==DayPhase.Day?"밤":"낮")}까지 {seconds/60:00}:{seconds%60:00}");
            if(ClockHand!=null)ClockHand.localRotation=Quaternion.Euler(0,0,-360*(float)((clock.Phase==DayPhase.Day?0:.5)+clock.PhaseProgress*.5));
            Caption("pause",actions.ProgressLabel);Caption("run",actions.RunLabel);Caption("edit",editing?"편집 종료":"회수 선택");Caption("spawn-air",Sandbox.SpawnAir?"공중 적: 켬":"공중 적: 끔");
            Caption("infinite-resources",Sandbox.Content.InfiniteResources?"자원 무한: ON":"자원 무한: OFF");Show("test-shortcuts",ShowDevelopmentControls&&(Application.isEditor||Debug.isDebugBuild));Enable("infinite-resources",!locked);
            bool can=actions.CanInteract(out _);Enable("quick-day",can);Enable("quick-night",can);Enable("pause",can);Enable("run",can);Enable("edit",!locked);Enable("spawn",!editing&&!locked);Enable("reset",!locked);Enable("day",can);Enable("night",can);Enable("spawn-air",!locked);
            Show("edit-actions",editing);Enable("edit",!locked&&(Input.Edits?.Count??0)==0);Enable("confirm",!locked&&Input.CanConfirm&&(Input.Edits.RecoveryCount>0||Input.Edits.Quote().Affordable));Enable("cancel",editing);Caption("cancel","전체 취소");Show("run",Sandbox.Assault==null);Show("developer",ShowDevelopmentControls&&(Application.isEditor||Debug.isDebugBuild));
            Text("pending",editing?$"대기 작업 {Input.Edits?.Count??0}개":"카드를 선택하면 배치를 시작합니다.");RefreshConstructionSummary();RefreshCatalog();
            var main=Sandbox.Content.MainBase;var hp=main?.Module<HealthModule>();bool hasHp=main!=null&&main.Active&&!main.Disposed&&hp!=null;
            Text("main-health",hasHp?$"{hp.Current:0.#} / {hp.Maximum:0.#}":Sandbox.Content.Defeated?"메인 기지 파괴":"메인 기지 없음");float healthRatio=hasHp?Mathf.Clamp01(hp.Current/Mathf.Max(1,hp.Maximum)):0;HealthFill.rectTransform.anchorMax=new Vector2(healthRatio,1);
            if(BaseResources==null){
            foreach(var icon in ResourceIcons){bool visible=showAll||Array.IndexOf(ResourcePriority,icon.Id)>=0;if(icon.View.gameObject.activeSelf!=visible)icon.View.gameObject.SetActive(visible);}int visibleResourceRows=0;
            buffer.Clear();for(int i=0;i<resources.Count;i++){var r=resources[i];if(!showAll&&Array.IndexOf(ResourcePriority,r.OutputId)<0)continue;foreach(var icon in ResourceIcons)if(icon.Id==r.OutputId){icon.View.anchoredPosition=new Vector2(0,-visibleResourceRows*26-2);}
                visibleResourceRows++;buffer.Append(resourceNames[i]).Append('\n');}
            Text("resources",buffer.Length>0?"<line-height=26>"+buffer.ToString():"등록된 생산 자원이 없습니다.");buffer.Clear();int resourceRows=0;var selectedInventory=Sandbox.Content.Inventories.Available(Sandbox.Content.Bases.SelectedBaseId);for(int i=0;i<resources.Count;i++){var r=resources[i];if(!showAll&&Array.IndexOf(ResourcePriority,r.OutputId)<0)continue;buffer.Append((selectedInventory?.Amount(r.OutputId)??0).ToString("N0")).Append('\n');resourceRows++;}
            Text("resource-values","<line-height=26>"+buffer.ToString());Text("resource-fold-caption","보유 자원 · "+BaseName(Sandbox.Content.Bases.SelectedBaseId));
            if(texts.TryGetValue("resources",out var resourceText)){var content=(RectTransform)resourceText.transform.parent;float h=Mathf.Max(26,resourceRows*26);if(!Mathf.Approximately(content.sizeDelta.y,h))content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,h);}
            Caption("resources-all",hasPriority?(showAll?"주요 자원":"전체 자원"):"전체 자원 표시 중");Enable("resources-all",hasPriority);
            }
            var selected=Input.SelectedContent??Input.SelectedTower?.building;if(selected?.Disposed==true)selected=null;
            if(buttons.TryGetValue("station-railway",out var stationButton)){bool isStation=selected?.Module<RailFacility>()?.Kind==RailFacilityKind.Station&&!editing&&!locked;stationButton.gameObject.SetActive(isStation);stationButton.interactable=isStation;}
            Show("selection",selected!=null&&selected.Active&&!editing);if(selected!=null)RefreshSelection(selected);
            RefreshPower(selected);RefreshProgress(editing,locked);
            var p=Sandbox.Persistence;Show("save-controls",p!=null);if(p!=null){bool save=p.CanSave(out var reason);Text("save-status",p.Status+(string.IsNullOrEmpty(p.CompatibilityNotice)?"":"\n"+p.CompatibilityNotice)+"\n마지막 저장: "+(p.LastSavedUtc??"없음")+(save?"":"\n"+reason));Enable("save",save);Enable("load",p.HasContinue&&!locked);}
            Text("counts",$"토대 {Sandbox.Foundations.Platforms.Count} · 포탑 {Sandbox.Towers.Count}\n적 {Sandbox.Enemies.Alive:N0} / {Sandbox.Enemies.MaxCount:N0} · 처치 {Sandbox.Enemies.Killed:N0}\n소환 대기 {Sandbox.SpawnStream.Pending:N0}");
        }
        void RefreshConstructionSummary()
        {
            if(!texts.ContainsKey("construction-summary")||Input.Edits==null)return;
            var edits=Input.Edits;var quote=edits.Quote();
            string Describe(WorldEditSession.ConstructionQuote q){
                var lines=new List<string>();foreach(var line in q.Lines){var name=ResourceName(line.Resource);if(name=="미지정")name=line.Resource;lines.Add($"{name} {line.Required:N0} / 보유 {line.Available:N0}");}
                return (lines.Count==0?"비용 없음":string.Join(" · ",lines))+(Sandbox.Content.InfiniteResources?" · 자원 무한":"")+(q.Reason==null?"":"\n"+q.Reason);
            }
            string selected=Input.SelectedDefinition?.DisplayName??(Input.Tool==WorldTool.Foundation?"토대":Input.Tool==WorldTool.Tower?Input.Kind.ToString():"없음");
            selected=selected.Split(" · ")[0];
            string value=$"선택: {selected} · 대기 {edits.Count}개 · 지불: "+(Sandbox.Content.BaseRules?BaseName(Sandbox.Content.Bases.SelectedBaseId):"공용 재고");
            value+="\n확정 비용: "+(edits.RecoveryCount>0?"회수 "+edits.RecoveryCount+"개":Describe(quote));
            if(Input.SelectedDefinition!=null||Input.Tool==WorldTool.Foundation||Input.Tool==WorldTool.Tower){
                var candidate=Input.SelectedDefinition??(Input.Tool==WorldTool.Tower?Sandbox.Foundations.Definition(Input.Kind):null);
                value+="\n다음 1개 추가 후: "+Describe(edits.Quote(candidate,foundation:Input.Tool==WorldTool.Foundation));
            }else if(!Input.IsEditing)value+="\n건물 카드: 설치 · 회수 선택: 기존 건물 선택";
            Text("construction-summary",value);
        }
        void RefreshSelection(BuildingInstance b)
        {
            var level=b.Module<IUpgradeControl>();Text("selection-title",b.DisplayName+(level==null?"":$"\nLv.{level.Level} / {level.MaximumLevel}"));buffer.Clear();
            if(SelectionPortrait!=null){Sprite portrait=null;foreach(var p in Portraits)if(p.DefinitionId==b.DefinitionId){portrait=p.Sprite;break;}SelectionPortrait.sprite=portrait;SelectionPortrait.enabled=portrait!=null;}
            buffer.AppendLine(BuildingOperationStatus.Describe(b,Sandbox.Content.Bases));
            var hp=b.Module<HealthModule>();if(SelectionHealthFill!=null){SelectionHealthFill.transform.parent.gameObject.SetActive(hp!=null);SelectionHealthFill.rectTransform.anchorMax=new Vector2(hp==null?0:Mathf.Clamp01(hp.Current/Mathf.Max(1,hp.Maximum)),1);}if(hp!=null)buffer.AppendLine($"체력 {hp.Current:0.#} / {hp.Maximum:0.#}");
            var weapon=b.Module<WeaponRuntime>();if(weapon!=null)buffer.AppendLine($"피해 {weapon.Damage:0.#}\n사거리 {weapon.Range:0.#}m\n공격 간격 {weapon.Interval:0.##}초");
            var rotation=b.Module<ITurretRotation>();if(rotation!=null)buffer.AppendLine($"회전 {rotation.DegreesPerSecond:0.#}°/초");
            var area=b.Module<IBuildArea>();if(area!=null)buffer.AppendLine(area.Shape==BuildAreaShape.Square?$"가동 영역 {area.Radius:0.#} × {area.Radius:0.#}칸":$"가동 영역 반경 {area.Radius:0.#}m");
            if(Sandbox.MeetingConstructionRules)buffer.AppendLine("소속 기지 "+BaseName(b.OwnerBaseId));
            if(b.Module<IBaseIdentity>()!=null&&Sandbox.Regions!=null)foreach(var r in Sandbox.Regions)if(r!=null&&r.Contains(b.Position)){buffer.AppendLine(r.DisplayName+" · 예정 자원 "+ResourceName(r.ResourceId));break;}
            Text("selection-stats",buffer.ToString());var state=actions.ReadUpgrade();Enable("upgrade",state.Available);Show("upgrade",level!=null);
            Caption("upgrade",state.Available?$"강화 Lv.{state.Level} → {state.Level+1}":"선택 건물 강화");Text("upgrade-status",UpgradeCostStatus(b,level,state));
        }
        string UpgradeCostStatus(BuildingInstance building,IUpgradeControl level,UpgradeHudState state)
        {
            if(state.VerificationFree)return state.Available?"비용 없음":state.Reason;
            if(level==null||!level.CanUpgrade(out _)||Sandbox.UpgradeCosts==null||!Sandbox.UpgradeCosts.TryQuote(building.DefinitionId,level.Level+1,out var costs,out _))
                return state.Available?"설정된 강화 비용 적용":state.Reason;
            var totals=new SortedDictionary<string,double>(StringComparer.Ordinal);
            foreach(var cost in costs){if(cost==null||string.IsNullOrWhiteSpace(cost.ResourceId)||!double.IsFinite(cost.Amount)||cost.Amount<0)return state.Reason;
                totals.TryGetValue(cost.ResourceId,out var amount);double total=amount+cost.Amount;if(!double.IsFinite(total))return state.Reason;totals[cost.ResourceId]=total;}
            var bank=Sandbox.Content.PaymentBankFor(building);
            var parts=new List<string>();foreach(var pair in totals){var name=ResourceName(pair.Key);if(name=="미지정")name=pair.Key;
                parts.Add($"{name} {pair.Value:N0}/보유 {(bank?.Amount(pair.Key)??0):N0}");}
            string summary=parts.Count==0?"강화 비용 없음":string.Join(" · ",parts);
            if(state.Available)return summary;
            if(bank==null)return summary+" · 기지 없음";
            if(state.Reason?.StartsWith("자원이 부족합니다:",StringComparison.Ordinal)==true)return summary+" · 부족";
            return summary+" · "+state.Reason;
        }
        string BaseName(string id)=>id==null?"없음":Sandbox.Content.Bases.Bases.TryGetValue(id,out var context)&&context.Active?context.Nexus.DisplayName:"상실";
        string ResourceName(string id){for(int i=0;i<resources.Count;i++)if(resources[i].OutputId==id)return resourceNames[i];return "미지정";}
        void RefreshPower(BuildingInstance selected)
        {
            var device=selected?.Module<PowerModule>();var storage=device?.Role==PowerRole.Storage?device:device?.Supply;
            if(storage==null&&Sandbox.Content.Bases.SelectedBaseId is string id&&Sandbox.Content.Bases.Bases.TryGetValue(id,out var context))storage=context.Nexus.Module<PowerModule>();
            Text("power",storage==null?"연결된 기지 없음":$"전력  {storage.Stored:0.#} / {storage.Capacity:0.#}");
            Text("power-rate",storage==null?"":$"{storage.Production-storage.Consumed:+0.#;-0.#;0}/s");
            if(PowerFill!=null)PowerFill.rectTransform.anchorMax=new Vector2(storage==null?0:Mathf.Clamp01((float)(storage.Stored/Math.Max(1,storage.Capacity))),1);
            buffer.Clear();if(storage!=null)buffer.AppendLine($"생산 +{storage.Production:0.#}/s · 요청 {storage.Requested:0.#}/s\n실제 소비 {storage.Consumed:0.#}/s");
            if(Sandbox.Content.BaseRules){int subs=0;foreach(var b in Sandbox.Content.Bases.Bases.Values)if(b.Nexus.Module<IBaseRole>()?.Role==BaseRole.Sub)subs++;buffer.AppendLine($"메인 Lv.{Sandbox.Content.LevelCap} · 서브 {subs}/{Sandbox.Content.SubLimit}");}
            if(Sandbox.MeetingConstructionRules)buffer.AppendLine("다음 배치 기지 "+BaseName(Sandbox.Content.Bases.SelectedBaseId));
            if(device!=null&&device.Role!=PowerRole.Storage)buffer.AppendLine($"전력 {(device.Role==PowerRole.Producer?"생산":"소비")} {device.Rate:0.#}/s · "+(device.Supply==null?"연결 기지 없음":device.Supplied?"공급 중":"공급 대기"));Text("power-details",buffer.ToString());
        }
        void RefreshProgress(bool editing,bool locked)
        {
            var a=Sandbox.Assault;Show("map-progress",a!=null);Show("first-loop",a!=null);if(a==null)return;var e=a.Energy;
            Text("first-loop-objective",$"첫 회차 · 에너지 {100d*e.Earned/e.Target:0.0}% · "+(e.Stage==MapStage.Cleared?"맵 클리어":e.Stage==MapStage.AwaitingCraft?"보스 처치 완료":"생산 → 운송 → 방어 → 오브 제작")+"\n"+FirstLoopObjective(e.Stage));
            Text("energy",$"{a.EnergyName} · 확보 {100d*e.Earned/e.Target:0.0}%\n추출 {e.Extracted/1000d:0.##} · 처치 {e.KillReward/1000d:0.##} · 보스 {e.BossReward/1000d:0.##}\n잔고 {e.Balance/1000d:0.##} / 목표 {e.Target/1000d:0.##}");
            Text("stage",(e.Stage switch {MapStage.Gathering=>"기지 영역에서 에너지 추출 중",MapStage.WaitingForNight=>"90% 확보 · 오늘 밤 보스 출현",MapStage.BossBattle=>a.BossSpawnFailure??$"보스전 · 체력 {a.BossHealth}",MapStage.AwaitingCraft=>"불완전 오브 획득 · 제작 가능",MapStage.Cleared=>"맵 클리어",_=>"메인 기지 상실"})+"\n"+FirstLoopObjective(e.Stage)+$"\n소환 지점 {a.Planner.Points.Count} · 야간 대기 {a.Planner.Pending:N0}");Enable("orb-craft",!editing&&!locked&&e.Stage==MapStage.AwaitingCraft);
        }
        string FirstLoopObjective(MapStage stage)
        {
            if(stage==MapStage.Cleared)return "진행·저장 → 저장하거나 새 게임으로 시작할 수 있습니다.";
            if(stage==MapStage.AwaitingCraft)return "목표 · 진행·저장 → 아래로 스크롤 → 오브 제작";
            if(stage==MapStage.BossBattle||stage==MapStage.WaitingForNight)return "목표 · 발전 전력 유지 · 공중 공격 가능한 방어로 보스 처치";
            if(stage==MapStage.Failed)return "새 게임으로 다시 시작하세요.";
            int iron=0,coal=0,generators=0,weapons=0;
            foreach(var b in Sandbox.Content.Bases.Buildings){
                if(!b.Operational)continue;
                if(b.DefinitionId=="resource.iron")iron++;
                if(b.DefinitionId=="railway.coal")coal++;
                if(b.Module<PowerModule>()?.Role==PowerRole.Producer)generators++;
                if(b.Module<WeaponRuntime>()!=null)weapons++;
            }
            if(iron==0||coal==0)return "목표 · 건설 메뉴 → 메인 기지 앞 파란 가동 영역에 철·석탄 생성기 → 확정";
            if(generators==0)return "목표 · 건설 메뉴 → 건물 전체가 파란 가동 영역 안에 들어가도록 발전기 설치 → 확정";
            if(Sandbox.Content.LevelCap<3)return "목표 · 메인 기지 선택 → 강화 Lv.3 · 플라즈마 레이저 해금";
            if(weapons==0)return "목표 · 파란 가동 영역 안에 방어 건물 전체를 배치 · 전력 공급 확인";
            var network=Sandbox.Content.Railway?.Network;
            if(network!=null){
                int stations=0;foreach(var station in network.Stations)stations++;
                if(stations<2)return "목표 · 서브 기지와 역 2개 설치 · 선로로 왕복 연결";
                if(network.Routes.Count==0)return "목표 · 역 선택 → 철도 상세 → 노선·화물 설정";
                var train=network.Routes[0].train;
                if(train.status==TrainStatus.FuelWait||train.fuel<=0)return "목표 · 철도 상세에서 석탄 보급 → 운행 시작";
                if(train.status==TrainStatus.Stopped)return "목표 · 철도 상세에서 화물 설정 → 운행 시작";
            }
            return "목표 · 생산·운송 유지 · 야간 방어 · 에너지 90% 확보";
        }
        void Update()
        {
            if(actions==null)return;var mouse=Mouse.current;bool over=false;
            if(mouse!=null&&EventSystem.current!=null){pointer??=new PointerEventData(EventSystem.current);pointer.position=mouse.position.ReadValue();hits.Clear();Raycaster.Raycast(pointer,hits);over=hits.Count>0;}
            Input.PointerOverUI=over||Minimap.Interacting||(Groups!=null&&Groups.Transitioning);Sandbox.CameraRig.BlockPointer=Input.PointerOverUI||Input.Dragging;
            Sandbox.CameraRig.BlockKeyboard=(Groups!=null&&Groups.Transitioning)||(Sandbox.RailwayHud?.Typing??false)||Amount.isFocused||Input.Dragging||Minimap.Interacting||(mouse!=null&&Minimap.gameObject.activeInHierarchy&&RectTransformUtility.RectangleContainsScreenPoint(Minimap.rectTransform,mouse.position.ReadValue()));
            if(Keyboard.current?.escapeKey.wasPressedThisFrame??false)EndTyping();Refresh();
        }
        void OnDisable(){if(Input!=null)Input.PointerOverUI=false;if(Sandbox?.CameraRig!=null){Sandbox.CameraRig.BlockPointer=false;Sandbox.CameraRig.BlockKeyboard=false;}Minimap?.CancelInteraction();}
    }
}
