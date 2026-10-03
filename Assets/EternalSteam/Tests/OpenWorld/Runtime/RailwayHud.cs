using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EternalSteam.Railway;
using TMPro;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    /// <summary>Saved Canvas controls; runtime only updates text, visibility and commands.</summary>
    public sealed partial class RailwayHud:MonoBehaviour
    {
        public enum EditorStage { Editing, Confirming, DiscardPrompt }
        [Serializable] public struct CommandBinding { public string Command; public UnityEngine.UI.Button Button; }
        public GameObject ConfirmPanel,DiscardPanel;
        public GameObject DraftStopsPanel;
        public UnityEngine.UI.Button[] DraftRows=Array.Empty<UnityEngine.UI.Button>();
        public TMP_Text DraftSelection;
        public TMP_Text SelectedSegment;
        public TMP_Text IssueHeader;
        public UnityEngine.UI.Button[] IssueRows=Array.Empty<UnityEngine.UI.Button>();
        int issuePage,selectedIssue;
        public IReadOnlyList<RouteIssue> DraftIssues=>draftValidation?.Issues??Array.Empty<RouteIssue>();
        public enum PortPick { None, Arrival, Departure }
        PortPick portPick;
        public PortPick PickingPort=>portPick;
        string draftSelectionId;int draftPage;bool loopClosed;
        public string SelectedDraftStationId=>draftSelectionId;
        public bool DraftLoopClosed=>loopClosed;
        bool AnchorLocked=>editing!=null&&(editing.train.segmentPaid||editing.train.status is not (TrainStatus.Stopped or TrainStatus.RouteError));
        bool CanRemoveSelected=>draft.Count>0&&!(AnchorLocked&&stopIndex==0)&&!(editing!=null&&editing.stops[editing.train.stop].stationId==draft[stopIndex].stationId);
        public TMP_Text CommandHints;
        public GameObject StationContextPanel;public TMP_Text StationContextHeader;public UnityEngine.UI.Button[] StationRows=Array.Empty<UnityEngine.UI.Button>();
        int stationPage;string lastRouteId;
        public string ContextStationId=>context;public int SelectedStopIndex=>stopIndex;
        public CommandBinding[] Commands=Array.Empty<CommandBinding>();
        EditorStage stage,previousStage;bool dirty,closeAfterDiscard;
        List<RailLeg> previewLegs;
        RouteValidationResult draftValidation;
        public RouteIssue DraftIssue=>DraftIssues.Count==0?null:DraftIssues[Math.Min(selectedIssue,DraftIssues.Count-1)];
        public EditorStage Stage=>stage;
        public OpenWorldSandbox Sandbox;public GameObject Panel,ListPanel,DetailPanel,DraftPanel;
        public TMP_Text Summary,Title,Feedback;public TMP_Text[] Rows;
        public UnityEngine.UI.Button[] RowButtons;
        public UnityEngine.UI.Button CancelPendingButton;
        public TMP_InputField Resource,Load,Unload;
        public GameObject ArmamentPanel;public TMP_Text ArmamentChoice;
        bool armament;int weaponIndex;
        public BuildingDefinition Track,Station,CoalProducer;
        readonly List<RailStop> draft=new();int page,stopIndex;bool valid;string requestId,context;
        RailRoute selected,editing;bool drafting;
        RailRouteMode DraftMode=>editing?.mode??RailRouteMode.Shuttle;
        bool ShuttleDraft=>DraftMode==RailRouteMode.Shuttle;
        bool DraftConnected=>ShuttleDraft||loopClosed;
        string displayedRoute;int displayedStop=-1,editingRevision,displayedRevision=-1;
        public bool HasDraft=>drafting||connecting;
        public RailRoute SelectedRoute=>selected;
        public IReadOnlyList<RailRoute> OverviewRoutes=>Visible();
        public int RoutePage=>page;
        public bool ShowingArmament=>armament;
        public bool Typing=>(Resource!=null&&Resource.isFocused)||(Load!=null&&Load.isFocused)||(Unload!=null&&Unload.isFocused);
        RailwayNetwork Network=>Sandbox.Content.Railway.Network;
        List<RailRoute> Visible()=>Network.Routes.Where(r=>context==null||r.stops.Any(s=>s.stationId==context)).ToList();
        public bool OpenFromStation(string stationId)
        {
            if(Sandbox?.Content?.Railway==null||drafting||connecting||!Network.StationActive(stationId))return false;
            context=stationId;selected=null;armament=false;displayedRoute=null;page=stationPage=0;Panel.SetActive(true);Refresh();return true;
        }
        bool Stationary=>selected!=null&&!selected.train.segmentPaid&&selected.train.status is TrainStatus.Stopped or TrainStatus.FuelWait or TrainStatus.RouteError;
        public bool CanExecute(string command,out string reason)
        {
            reason=null;
            if(Sandbox?.Content==null){reason="게임 준비 중입니다.";return false;}
            if(Sandbox.Content.Defeated||Sandbox.Persistence?.Blocked==true){reason="진행이 차단되었습니다.";return false;}
            if(!Sandbox.Content.BaseRules){reason="기지 규칙이 적용된 맵에서 사용할 수 있습니다.";return false;}
            if(!Panel.activeSelf&&command is not ("open" or "station" or "rotate")&&!command.StartsWith("build:")){reason="노선 화면을 먼저 여세요.";return false;}
            if(connecting||command=="connect"||command.StartsWith("connection-"))return CanConnectionCommand(command,out reason);
            if(drafting&&draft.Count>0)stopIndex=Math.Max(0,draft.FindIndex(s=>s.stationId==draftSelectionId));
            bool allowed=false;
            if(drafting){
                if(stage==EditorStage.DiscardPrompt)allowed=command is "keep" or "discard";
                else if(command is "close" or "cancel")allowed=true;
                else if(stage==EditorStage.Confirming)allowed=command=="back-edit"||(command=="commit"&&valid);
                else allowed=command switch {
                    "issue-prev"=>issuePage>0,
                    "issue-next"=>(issuePage+1)*IssueRows.Length<DraftIssues.Count,
                    _ when command.StartsWith("issue:")=>int.TryParse(command.Substring(6),out int issueRow)&&issueRow>=0&&issueRow<IssueRows.Length&&issuePage*IssueRows.Length+issueRow<DraftIssues.Count,
                    "map-arrival" or "map-departure"=>!ShuttleDraft&&draft.Count>0&&Network.StationActive(draftSelectionId)&&!Sandbox.GetComponent<OpenWorldInput>().IsEditing,
                    "map-cancel"=>portPick!=PortPick.None,
                    _ when command.StartsWith("port:")=>portPick!=PortPick.None&&draft.Count>0&&Network.StationActive(draftSelectionId)&&!Sandbox.GetComponent<OpenWorldInput>().IsEditing&&int.TryParse(command.Substring(5),out int port)&&port>=0&&port<4,
                    "validate"=>draft.Count>=2&&DraftConnected,
                    "close-loop"=>!ShuttleDraft&&draft.Count>=2&&!loopClosed,
                    "open-loop"=>!ShuttleDraft&&loopClosed,
                    "down"=>stopIndex<draft.Count-1&&(!AnchorLocked||stopIndex>0),
                    "first"=>stopIndex>0&&!AnchorLocked,
                    "draft-prev"=>draftPage>0,
                    "draft-next"=>(draftPage+1)*DraftRows.Length<draft.Count,
                    _ when command.StartsWith("draft-row:")=>int.TryParse(command.Substring(10),out int row)&&row>=0&&row<DraftRows.Length&&draftPage*DraftRows.Length+row<draft.Count,
                    "add"=>Sandbox.GetComponent<OpenWorldInput>().SelectedContent is BuildingInstance b&&Network.StationActive(b.PersistentId)&&!draft.Any(d=>d.stationId==b.PersistentId),
                    "next-stop"=>draft.Count>1,
                    "remove"=>CanRemoveSelected,
                    "up"=>stopIndex>0&&(!AnchorLocked||stopIndex>1),
                    "arrival" or "departure"=>!ShuttleDraft&&draft.Count>0,
                    _=>false};
                if(!allowed)reason=stage==EditorStage.DiscardPrompt?"변경 유지 또는 폐기를 선택하세요.":stage==EditorStage.Confirming?"편집으로 돌아가거나 확인 후 확정하세요.":"역 선택·최소 두 역·예약 기준역 조건을 확인하세요.";
                return allowed;
            }
            if(command.StartsWith("issue")||command.StartsWith("draft-")||command.StartsWith("map-")||command.StartsWith("port:")||command is "down" or "first" or "close-loop" or "open-loop" or "commit" or "validate" or "cancel" or "discard" or "keep" or "back-edit" or "add" or "up" or "remove" or "arrival" or "departure"){reason="편집 화면에서만 사용할 수 있습니다.";return false;}
            if(command.StartsWith("station-row:")){allowed=selected!=null&&!armament&&int.TryParse(command.Substring(12),out int index)&&index>=0&&index<StationRows.Length&&stationPage*StationRows.Length+index<selected.stops.Count;reason=allowed?null:"노선의 역을 선택하세요.";return allowed;}
            if(command=="station-prev")return selected!=null&&stationPage>0;
            if(command=="station-next")return selected!=null&&(stationPage+1)*StationRows.Length<selected.stops.Count;
            if(command=="station-context")return selected!=null;
            if(command=="new"){allowed=selected==null&&!armament&&Network.Stations.Count(b=>Network.StationActive(b.PersistentId))>=2;reason=allowed?null:"활성 기차역이 두 곳 이상 필요합니다.";return allowed;}
            if(command=="start"){
                allowed=selected!=null&&selected.train.status is TrainStatus.Stopped or TrainStatus.FuelWait or TrainStatus.RouteError&&selected.train.HasCargoResource&&!Sandbox.GetComponent<OpenWorldInput>().IsEditing&&Network.ValidateExisting(selected,out _)&&(selected.train.segmentPaid||selected.train.fuel>=Network.FuelCost(selected.CurrentLeg));
                reason=allowed?null:"역 정지·화물 지정·유효 노선·충분한 연료가 필요합니다.";return allowed;
            }
            if(command=="stop"){allowed=selected!=null&&selected.train.status is TrainStatus.Moving or TrainStatus.Dwelling;reason=allowed?null:"운행 중에 중지할 수 있습니다.";return allowed;}
            if(command is "configure" or "fuel"){
                allowed=Stationary&&selected.train.status is TrainStatus.Stopped or TrainStatus.FuelWait;
                if(command=="configure"&&allowed)allowed=double.TryParse(Load.text,out var load)&&double.TryParse(Unload.text,out var unload)&&double.IsFinite(load)&&double.IsFinite(unload)&&load>=0&&load<=RailwayNetwork.CargoCapacity&&unload>=0&&unload<=RailwayNetwork.CargoCapacity&&(!selected.train.HasCargoResource||Resource.text.Trim()==selected.train.resource)&&Sandbox.ContentCatalog.Buildings.SelectMany(d=>d.Modules).OfType<ProductionModuleDefinition>().Any(m=>m.OutputId==Resource.text.Trim());
                if(command=="fuel"&&allowed){
                    var station=Network.Station(selected.stops[selected.train.stop].stationId);
                    var bank=station==null?null:Sandbox.Content.Inventories.Available(station.OwnerBaseId);
                    allowed=!selected.train.waitingForStation&&station!=null&&Network.StationActive(station.PersistentId)&&selected.train.fuel<selected.stops.Count*RailwayNetwork.FuelPerStation&&bank!=null&&(bank.InfiniteResources||bank.Amount(Network.CoalId)>0);
                }
                reason=allowed?null:command=="fuel"?"역 정지·연료칸 여유·정차 역 소속 기지의 석탄을 확인하세요.":"역에서 정지한 뒤 화물 설정을 확인하세요.";return allowed;
            }
            if(command=="cancel-pending"){allowed=selected!=null&&Network.HasPending(selected);reason=allowed?null:"취소할 예약이 없습니다.";return allowed;}
            if(command is "edit" or "next-stop" or "defense"){allowed=selected!=null;reason=allowed?null:"노선을 선택하세요.";return allowed;}
            if(command.StartsWith("weapon-")&&command!="weapon-back"){
                if(!armament||selected==null){reason="노선의 방어칸을 선택하세요.";return false;}
                var defense=Sandbox.Content.Railway.Defense;
                if(command=="weapon-next"){allowed=Stationary&&defense.Choices(selected).Skip(1).Any();reason=allowed?null:"선택할 다른 방어 건물이 없습니다.";return allowed;}
                if(command=="weapon-shape")return defense.CanShape(selected,out reason);
                if(command=="weapon-remove")return defense.CanRemove(selected,out reason);
                if(command=="weapon-upgrade")return defense.CanUpgrade(selected,out reason);
                if(command=="weapon-charge")return defense.CanCharge(selected,out reason);
                if(command=="weapon-install"){
                    var choices=defense.Choices(selected).ToArray();
                    if(choices.Length==0){reason="이 크기의 해금된 방어 건물이 없습니다.";return false;}
                    return defense.CanInstall(selected,choices[weaponIndex%choices.Length],out reason);
                }
                reason="알 수 없는 방어 명령입니다.";return false;
            }
            if(command.StartsWith("row:")){allowed=!armament&&selected==null&&int.TryParse(command.Substring(4),out int index)&&index>=0&&page*Rows.Length+index<Visible().Count;reason=allowed?null:"목록의 유효한 노선을 선택하세요.";return allowed;}
            if(command=="page-prev")return page>0;
            if(command=="page-next")return (page+1)*Rows.Length<Visible().Count;
            return true;
        }
        void LeaveDraft(bool close)
        {
            portPick=PortPick.None;Sandbox.RailwayView.ClearDraftSelection();draft.Clear();draftSelectionId=null;loopClosed=false;previewLegs=null;draftValidation=null;drafting=false;valid=false;dirty=false;stage=EditorStage.Editing;
            if(editing!=null)selected=editing;editing=null;
            if(close){armament=false;Panel.SetActive(false);Sandbox.RailwayView?.Highlight(null,Sandbox.Content);}
        }
        public void Execute(string command)
        {
            if(Sandbox.Content==null||Sandbox.Content.Defeated||Sandbox.Persistence?.Blocked==true)return;
            if(!Sandbox.Content.BaseRules){Message("철도는 기지 규칙이 적용된 StartRegionSandbox에서 사용할 수 있습니다.");return;}
            Sandbox.Content.Railway.Refresh();string error=null;
            if(!CanExecute(command,out error)){Message(error??"현재 화면에서는 사용할 수 없습니다.");Refresh();return;}
            if(connecting||command=="connect"||command.StartsWith("connection-")){ExecuteConnection(command);return;}
            if(command.StartsWith("build:")){
                if(drafting){Message("노선 초안을 확정하거나 취소하세요.");return;}
                Panel.SetActive(false);var input=Sandbox.GetComponent<OpenWorldInput>();input.BeginEditing();input.SelectContent(command=="build:track"?Track:command=="build:station"?Station:CoalProducer);return;
            }
            bool portChanged=false;
            if(!command.StartsWith("port:")&&!command.StartsWith("map-"))portPick=PortPick.None;
            switch(command){
                case "rotate":var pending=Sandbox.GetComponent<OpenWorldInput>().Edits?.ContentPending.LastOrDefault(p=>p.Request.Definition==Station);if(pending==null){Message("기차역을 임시 배치한 뒤 방향을 조정하세요.");break;}pending.Request.Direction=Quaternion.Euler(0,90,0)*pending.Request.Direction;pending.View.transform.rotation=Quaternion.LookRotation(pending.Request.Direction);break;
                case "open":armament=false;if(drafting){Message("작성 중인 초안을 계속 편집하세요.");break;}context=null;selected=null;displayedRoute=null;Panel.SetActive(true);break;
                case "station":if(drafting){Message("작성 중인 초안을 먼저 확정하거나 취소하세요.");break;}if(!OpenFromStation(Sandbox.GetComponent<OpenWorldInput>().SelectedContent?.PersistentId))Message("맵에서 활성 기차역을 선택하세요.");break;
                case "close":case "cancel":
                    if(drafting&&dirty){previousStage=stage;stage=EditorStage.DiscardPrompt;closeAfterDiscard=command=="close";}
                    else LeaveDraft(command=="close");break;
                case "keep":stage=previousStage;break;
                case "discard":LeaveDraft(closeAfterDiscard);break;
                case "back-edit":stage=EditorStage.Editing;valid=false;InspectDraft();break;
                case "list":armament=false;if(drafting){Message("초안을 취소하거나 확정하세요.");return;}lastRouteId=selected?.id;selected=null;break;
                case "station-context":if(selected!=null){context=selected.stops[stopIndex].stationId;lastRouteId=selected.id;selected=null;page=0;}break;
                case "station-prev":stationPage--;break;case "station-next":stationPage++;break;
                case "page-next":page++;break;case "page-prev":page=Math.Max(0,page-1);break;
                case "new":if(Network.Stations.Count()<2){Message("설치된 역이 두 곳 이상 필요합니다.");break;}draft.Clear();drafting=true;editing=null;requestId=Guid.NewGuid().ToString("N");stage=EditorStage.Editing;dirty=false;previewLegs=null;draftValidation=null;selectedIssue=issuePage=0;valid=false;stopIndex=0;draftSelectionId=context;if(context!=null&&Network.StationActive(context)){draft.Add(new RailStop{stationId=context});draftSelectionId=context;}draftPage=0;loopClosed=false;break;
                case "edit":if(selected==null)break;draft.Clear();draft.AddRange((Network.HasPending(selected)?selected.pending.stops:selected.stops).Select(s=>s.Copy()));drafting=true;editing=selected;editingRevision=selected.revision;stage=EditorStage.Editing;dirty=false;previewLegs=null;draftValidation=null;valid=false;stopIndex=Math.Clamp(stopIndex,0,draft.Count-1);draftSelectionId=draft[stopIndex].stationId;draftPage=stopIndex/Math.Max(1,DraftRows.Length);loopClosed=true;InspectDraft();Message("역에 정지한 기차는 즉시 적용 · 운행 중에는 기존 첫 역 복귀 시 적용");break;
                case "add":if(!drafting)break;var b=Sandbox.GetComponent<OpenWorldInput>().SelectedContent;if(b==null||Network.Station(b.PersistentId)==null){Message("맵에서 추가할 역을 선택하세요.");break;}if(draft.Any(s=>s.stationId==b.PersistentId)){Message("이미 등록된 역입니다.");break;}draft.Add(new RailStop{stationId=b.PersistentId});stopIndex=draft.Count-1;valid=false;break;
                case "remove":if(drafting&&draft.Count>0){draft.RemoveAt(stopIndex);stopIndex=Math.Max(0,stopIndex-1);valid=false;}break;
                case "next-stop":int count=drafting?draft.Count:selected?.stops.Count??0;if(count>0){stopIndex=(stopIndex+1)%count;if(!drafting)stationPage=stopIndex/Math.Max(1,StationRows.Length);}break;
                case "up":if(drafting&&stopIndex>0){var old=draft[stopIndex];draft.RemoveAt(stopIndex);draft.Insert(--stopIndex,old);valid=false;}break;
                case "down":var moving=draft[stopIndex];draft.RemoveAt(stopIndex);draft.Insert(++stopIndex,moving);break;
                case "first":var rotated=draft.Skip(stopIndex).Concat(draft.Take(stopIndex)).ToList();draft.Clear();draft.AddRange(rotated);stopIndex=0;break;
                case "close-loop":loopClosed=true;break;
                case "open-loop":loopClosed=false;break;
                case "draft-prev":draftPage--;break;
                case "draft-next":draftPage++;break;
                case "issue-prev":issuePage--;break;
                case "issue-next":issuePage++;break;
                case "map-arrival":portPick=PortPick.Arrival;Message("지도에서 진입 포트 번호를 선택하세요.");break;
                case "map-departure":portPick=PortPick.Departure;Message("지도에서 진출 포트 번호를 선택하세요.");break;
                case "map-cancel":portPick=PortPick.None;break;
                case "arrival":if(drafting&&draft.Count>0){draft[stopIndex].arrival=(draft[stopIndex].arrival+1)%4;valid=false;}break;
                case "departure":if(drafting&&draft.Count>0){draft[stopIndex].departure=(draft[stopIndex].departure+1)%4;valid=false;}break;
                case "validate":if(drafting){InspectDraft();valid=draftValidation.Valid;if(valid)stage=EditorStage.Confirming;Message(valid?(ShuttleDraft?"왕복 검증 통과 · 같은 선로로 돌아옵니다.":"순환 검증 통과 · 역 순서와 구간을 확인하고 확정하세요."):draftValidation.Issue.Message);}break;
                case "commit":if(!drafting||!valid){Message("먼저 연결 경로를 검증하세요.");break;}bool ok=editing==null?Network.Commit(requestId,draft,out selected,out error,DraftMode):Network.UpdateRoute(editing,draft,editingRevision,out error);if(ok){if(editing!=null)selected=editing;drafting=false;dirty=false;stage=EditorStage.Editing;previewLegs=null;stopIndex=context==null?0:Math.Max(0,selected.stops.FindIndex(s=>s.stationId==context));lastRouteId=selected.id;Message(Network.HasPending(selected)?"예약 저장 · 기존 노선의 첫 역 복귀 때 재검증하여 적용":"노선 저장 · 기차 정지 대기");}else {valid=false;stage=EditorStage.Editing;InspectDraft();Message(error);}break;
                case "cancel-pending":Message(Network.CancelPending(selected)?"예약 변경 취소 · 기존 노선 유지":"취소할 예약 변경이 없습니다.");break;
                
                case "configure":if(selected==null)break;string resource=Resource.text.Trim();bool allowed=Sandbox.ContentCatalog.Buildings.SelectMany(d=>d.Modules).OfType<ProductionModuleDefinition>().Any(m=>m.OutputId==resource);
                    if(!allowed||!double.TryParse(Load.text,out var load)||!double.TryParse(Unload.text,out var unload)){Message("일반 자원 ID와 0~1,000 수량을 입력하세요.");break;}Network.Configure(selected,resource,stopIndex,load,unload,out error);Message(error??"역 화물 설정 저장");break;
                case "start":if(Sandbox.GetComponent<OpenWorldInput>().IsEditing){Message("건설 작업을 먼저 확정하거나 취소하세요.");break;}Network.Start(selected,out error);Message(error??"운행 시작");break;
                case "defense":armament=selected!=null;weaponIndex=0;break;
                case "weapon-back":armament=false;break;
                case "weapon-next":weaponIndex++;break;
                case "weapon-shape":Sandbox.Content.Railway.Defense.Shape(selected,out error);weaponIndex=0;Message(error??"빈 방어칸 형태 변경");break;
                case "weapon-install":var choices=Sandbox.Content.Railway.Defense.Choices(selected).ToArray();if(choices.Length==0){Message("이 크기의 해금된 방어 건물이 없습니다.");break;}Sandbox.Content.Railway.Defense.Install(selected,choices[weaponIndex%choices.Length],out error);Message(error??"방어 장착 · 정차 역 기지 철 10 차감");break;
                case "weapon-remove":Sandbox.Content.Railway.Defense.Remove(selected,out error);Message(error??"방어 해제 · 환급 0 · 축전지 유지");break;
                case "weapon-upgrade":Sandbox.Content.Railway.Defense.Upgrade(selected,out error);Message(error??$"방어 강화 · 철 {TrainArmament.UpgradeIronPerLevel:0} × 이전 레벨 차감");break;
                case "weapon-charge":Sandbox.Content.Railway.Defense.Charge(selected,out error);Message(error??"정차 역 소속 기지의 저장 전력을 축전지로 이동");break;
                case "stop":Network.Stop(selected);break;
                case "fuel":Network.Refuel(selected,RailwayNetwork.RefuelBatch,out error);Message(error??$"정차 역 소속 기지에서 최대 석탄 {RailwayNetwork.RefuelBatch:0}개 보급");break;
                default:if(command.StartsWith("issue:")&&int.TryParse(command.Substring(6),out int issueRowIndex)){selectedIssue=issuePage*IssueRows.Length+issueRowIndex;var issue=DraftIssues[selectedIssue];if(issue.StopIndex>=0&&issue.StopIndex<draft.Count){stopIndex=issue.StopIndex;draftSelectionId=draft[stopIndex].stationId;}}else if(command.StartsWith("port:")&&int.TryParse(command.Substring(5),out int portIndex)){if(portPick==PortPick.Arrival){portChanged=draft[stopIndex].arrival!=portIndex;draft[stopIndex].arrival=portIndex;}else {portChanged=draft[stopIndex].departure!=portIndex;draft[stopIndex].departure=portIndex;}portPick=PortPick.None;}else if(command.StartsWith("draft-row:")&&int.TryParse(command.Substring(10),out int rowIndex)){stopIndex=draftPage*DraftRows.Length+rowIndex;}else if(command.StartsWith("station-row:")&&int.TryParse(command.Substring(12),out int stationRow)){stopIndex=stationPage*StationRows.Length+stationRow;}else if(command.StartsWith("row:")&&int.TryParse(command.Substring(4),out int index)){var list=Visible();index+=page*Rows.Length;if(index>=0&&index<list.Count){selected=list[index];lastRouteId=selected.id;stopIndex=context==null?0:Math.Max(0,selected.stops.FindIndex(s=>s.stationId==context));stationPage=stopIndex/Math.Max(1,StationRows.Length);}}break;
            }
            if(drafting){
                if(draft.Count<2)loopClosed=false;
                draftSelectionId=draft.Count==0?null:draft[stopIndex].stationId;
                if(command is not ("draft-prev" or "draft-next"))draftPage=DraftRows.Length==0?0:stopIndex/DraftRows.Length;
            }
            if(portChanged||command is "add" or "remove" or "up" or "down" or "first" or "close-loop" or "open-loop" or "arrival" or "departure"){dirty=true;valid=false;InspectDraft();}
            Sandbox.Content.Railway.Tick(0);if(selected==null&&!drafting)Sandbox.RailwayView.Highlight(null,Sandbox.Content);Sandbox.Persistence?.Changed();Refresh();
        }
        void SelectDraftStation(int index)
        {
            stopIndex=index;draftSelectionId=draft[index].stationId;draftPage=index/Math.Max(1,DraftRows.Length);portPick=PortPick.None;Refresh();
        }
        public bool TryHandlePortScreenClick(Vector2 screen)
        {
            if(!Panel.activeSelf||!drafting||stage!=EditorStage.Editing||portPick==PortPick.None||Sandbox.GetComponent<OpenWorldInput>().IsEditing)return false;
            var view=Sandbox.RailwayView;
            for(int i=0;i<view.PortLabels.Length;i++)if(view.PortLabels[i].gameObject.activeInHierarchy&&RectTransformUtility.RectangleContainsScreenPoint(view.PortLabels[i].rectTransform,screen,Sandbox.CameraRig.View)){
                Execute("port:"+i);return true;
            }
            return false;
        }
        public bool TryHandleDraftWorldClick(Vector3 point)
        {
            if(TryConnectionWorldClick(point))return true;
            if(!Panel.activeSelf||!drafting||Sandbox.GetComponent<OpenWorldInput>().IsEditing)return false;
            if(stage!=EditorStage.Editing)return true;
            if(Sandbox.Content.Defeated||Sandbox.Persistence?.Blocked==true)return true;
            var cell=Sandbox.Content.GroundWorld.Grid.WorldToCell(point);
            if(portPick!=PortPick.None){
                var station=Network.Station(draftSelectionId);
                if(station!=null)for(int i=0;i<4;i++)if(RailwayNetwork.Port(station,i)==cell){Execute("port:"+i);return true;}
                Message("선택한 역의 포트 번호를 누르세요. 선택 취소로 돌아갈 수 있습니다.");return true;
            }
            if(Sandbox.Content.GroundAt(point,out var building)){
                int index=draft.FindIndex(s=>s.stationId==building.PersistentId);if(index>=0){SelectDraftStation(index);return true;}
            }
            if(DraftConnected&&draftValidation!=null)foreach(var segment in draftValidation.Segments)if(segment.Status==RouteSegmentStatus.Valid&&segment.Path.cells.Contains(cell)){SelectDraftStation(segment.Index);return true;}
            return false;
        }
        void InspectDraft()
        {
            draftValidation=Network.InspectDraft(draft,editing?.id,DraftMode);selectedIssue=0;issuePage=0;
            previewLegs=draftValidation.Valid&&DraftConnected?draftValidation.Legs.ToList():null;
        }
        static string IssueLabel(RouteIssueCode code)=>code switch {
            RouteIssueCode.TooFewStations=>"역 부족",RouteIssueCode.DuplicateStation=>"중복 역",RouteIssueCode.InactiveStation=>"역 비활성",RouteIssueCode.InvalidQuantity=>"수량 오류",RouteIssueCode.InvalidPort=>"포트 오류",RouteIssueCode.MissingTrack=>"선로 단절",RouteIssueCode.RepeatedTrack=>"중복 선로",RouteIssueCode.OccupiedTrack=>"타 노선 점유",RouteIssueCode.Branch=>"분기/교차",RouteIssueCode.InsufficientTankRange=>"연료칸 거리 초과",_=>"도착 포트 초과 연결"};
        void Message(string text){Feedback.text=text??"";Sandbox.Message=text??"";}
        public static string Status(TrainStatus s)=>s switch{TrainStatus.Stopped=>"정지",TrainStatus.Moving=>"구간 이동 중",TrainStatus.Dwelling=>"역 정차 중",TrainStatus.StopRequested=>"중지 요청",TrainStatus.FuelWait=>"연료 부족 대기",_=>"노선 오류 정지"};
        void Refresh()
        {
            if(Sandbox.Content==null)return;
            foreach(var binding in Commands)if(binding.Button!=null){
                bool legacyOnly=binding.Command is "arrival" or "departure" or "map-arrival" or "map-departure" or "close-loop" or "open-loop";
                if(legacyOnly)binding.Button.gameObject.SetActive(!ShuttleDraft);
                binding.Button.interactable=CanExecute(binding.Command,out _);
            }
            if(!Panel.activeSelf){if(!Sandbox.GetComponent<OpenWorldInput>().ChoosingStationDirection)Sandbox.RailwayView.ClearDraftSelection();return;}
            ConnectionPanel.SetActive(connecting);
            if(connecting){foreach(var panel in new[]{ListPanel,DetailPanel,ArmamentPanel,DraftPanel,ConfirmPanel,DiscardPanel,DraftStopsPanel,StationContextPanel})panel.SetActive(false);Sandbox.RailwayView.ClearDraftSelection();RefreshConnection();return;}
            ListPanel.SetActive(!drafting&&selected==null);DetailPanel.SetActive(!drafting&&selected!=null&&!armament);ArmamentPanel.SetActive(!drafting&&selected!=null&&armament);DraftPanel.SetActive(drafting&&stage==EditorStage.Editing);ConfirmPanel.SetActive(drafting&&stage==EditorStage.Confirming);DiscardPanel.SetActive(drafting&&stage==EditorStage.DiscardPrompt);
            DraftStopsPanel.SetActive(drafting&&stage==EditorStage.Editing);
            if(StationContextPanel!=null)StationContextPanel.SetActive(!drafting&&selected!=null&&!armament);
            bool showMap=drafting&&stage==EditorStage.Editing&&draft.Count>0;
            var segmentResult=showMap&&draftValidation!=null&&draftValidation.Segments.Count>0?draftValidation.Segments[Math.Min(stopIndex,draftValidation.Segments.Count-1)]:null;
            RailLeg selectedLeg=DraftConnected&&segmentResult?.Status==RouteSegmentStatus.Valid?segmentResult.Path:null;
            if(showMap){
                var stop=draft[stopIndex];Sandbox.RailwayView.PresentDraftSelection(Network.Station(stop.stationId),stop,selectedLeg,Sandbox.Content,ShuttleDraft);
                string selection=portPick==PortPick.Arrival?"진입 포트 선택 중":portPick==PortPick.Departure?"진출 포트 선택 중":"지도 역/선로 클릭으로 구간 선택";
                if(ShuttleDraft&&draft.Count>1){int index=Math.Min(stopIndex,draft.Count-2);SelectedSegment.text=$"같은 선로 왕복 · 연결구 자동 선택\n{Network.StationName(draft[index].stationId)} ↔ {Network.StationName(draft[index+1].stationId)}\n{(selectedLeg!=null?$"{selectedLeg.cells.Count}칸 · 편도 {selectedLeg.cells.Count}초":"연결 오류를 확인하세요.")}";}
                else if(draft.Count>1){var next=draft[(stopIndex+1)%draft.Count];SelectedSegment.text=$"{selection}\n선택 구간 {stopIndex+1}{(stopIndex==draft.Count-1?" · 복귀":"")}\n{Network.StationName(stop.stationId)} · 진출 {stop.departure+1}\n→ {Network.StationName(next.stationId)} · 진입 {next.arrival+1}\n{(!loopClosed?"순환 연결 필요":selectedLeg!=null?$"{selectedLeg.cells.Count}칸 · {selectedLeg.cells.Count}초":segmentResult?.Status==RouteSegmentStatus.Blocked?"역 설정 수정 필요":"경로 오류")}";}
                else SelectedSegment.text=selection+"\n다음 역을 추가하세요.";
            }else {Sandbox.RailwayView.ClearDraftSelection();SelectedSegment.text="역을 선택하세요.";}
            if(drafting){
                draftPage=Math.Min(draftPage,Math.Max(0,(draft.Count-1)/Math.Max(1,DraftRows.Length)));
                for(int i=0;i<DraftRows.Length;i++){int index=draftPage*DraftRows.Length+i;var button=DraftRows[i];button.gameObject.SetActive(index<draft.Count);if(index<draft.Count){var stop=draft[index];button.GetComponentInChildren<TMP_Text>().text=$"{(stop.stationId==draftSelectionId?"> ":"")}{index+1}. {Network.StationName(stop.stationId)}";}}
                DraftSelection.text=$"역 선택 · {draftPage+1}페이지\n{(ShuttleDraft?"단일 선로 왕복 · 복귀선 불필요":loopClosed?"순환 연결됨":"순환 열림 · 연결 후 검증 가능")}\n{(AnchorLocked?"운행 중: 기존 첫 역 고정":"정지: 첫 역 변경 가능 · 정차 역 삭제 불가")}";
            }
            if(drafting){
                IssueHeader.text=$"오류 {DraftIssues.Count}개 · {issuePage+1}페이지";
                for(int i=0;i<IssueRows.Length;i++){int index=issuePage*IssueRows.Length+i;var button=IssueRows[i];button.gameObject.SetActive(index<DraftIssues.Count);if(index<DraftIssues.Count){var issue=DraftIssues[index];button.GetComponentInChildren<TMP_Text>().text=$"{(index==selectedIssue?">":"!")} {(issue.LegIndex>=0?$"구간 {issue.LegIndex+1}":issue.StopIndex>=0?$"역 {issue.StopIndex+1}":"노선")} · {IssueLabel(issue.Code)}";}}
            }
            bool writable=!drafting&&Stationary&&selected.train.status is TrainStatus.Stopped or TrainStatus.FuelWait;Resource.readOnly=!writable||selected.train.HasCargoResource;Load.readOnly=Unload.readOnly=!writable;
            var hints=new List<string>();foreach(var command in new[]{drafting?"validate":"new","start","configure"})if(!CanExecute(command,out var why)&&why!=null&&!hints.Contains(why))hints.Add(why);CommandHints.text=string.Join(" · ",hints);CommandHints.gameObject.SetActive(drafting||selected==null);
            if(CancelPendingButton!=null)CancelPendingButton.interactable=!drafting&&Network.HasPending(selected);
            if(armament&&selected!=null){selected.train.armament??=new TrainArmament();var a=selected.train.armament;var choices=Sandbox.Content.Railway.Defense.Choices(selected).ToArray();weaponIndex=choices.Length==0?0:weaponIndex%choices.Length;ArmamentChoice.text=$"방어칸 {a.footprint.x}×{a.footprint.y} · 장착 {(string.IsNullOrEmpty(a.definition)?"없음":a.definition)}\n축전지 {a.battery:0.##}/{TrainArmament.Capacity} · 후보 {(choices.Length==0?"없음":choices[weaponIndex].DisplayName)}\n설치 철 {TrainArmament.InstallIron:0} / 강화 철 {TrainArmament.UpgradeIronPerLevel:0}×현재 레벨 / 해제 환급 0";}
            var text=new StringBuilder();string contextName=context==null?null:Network.StationName(context);Title.text=drafting?(stage==EditorStage.Confirming?"노선 확정 확인":stage==EditorStage.DiscardPrompt?"미저장 변경을 폐기할까요?":"노선 편집 · 확정 전 초안"):selected!=null?(contextName==null?selected.name:contextName+" · "+selected.name):contextName==null?"전체 노선":contextName+" · 노선";
            if(!drafting&&selected==null){var list=Visible();page=Math.Min(page,Math.Max(0,(list.Count-1)/Rows.Length));for(int i=0;i<Rows.Length;i++){int index=page*Rows.Length+i;RowButtons[i].gameObject.SetActive(index<list.Count);if(index<list.Count){var r=list[index];Rows[i].text=$"{(r.id==lastRouteId?"> ":"")}{r.name} · {r.stops.Count}역 · {(r.train.waitingForStation?"역 진입 대기":Status(r.train.status))} · {r.ExpectedSeconds:0}s{(Network.HasPending(r)?" · 예약 변경":"")}";}}
                text.AppendLine($"등록 {list.Count}개 · 페이지 {page+1}");
                if(context!=null)text.AppendLine(list.Count==0?"연결된 노선 없음 · 새 노선에서 이 역이 첫 정차역으로 추가됩니다.":"선택 역의 노선을 고르거나 새 노선을 만드세요.");
                foreach(var route in list.Skip(page*Rows.Length).Take(Rows.Length)){text.AppendLine(route.name+": "+string.Join(" → ",route.stops.Select(v=>Network.StationName(v.stationId)))+(route.IsShuttle?" · 같은 선로 왕복":" → 첫 역"));if(context!=null){int at=route.stops.FindIndex(v=>v.stationId==context);int prior=route.IsShuttle?(at==0?1:at-1):(at+route.stops.Count-1)%route.stops.Count;int next=route.IsShuttle?(at==route.stops.Count-1?at-1:at+1):(at+1)%route.stops.Count;text.AppendLine($"방문 {at+1}번 · 이전 {Network.StationName(route.stops[prior].stationId)} / 다음 {Network.StationName(route.stops[next].stationId)}");}text.AppendLine(string.IsNullOrEmpty(route.error)?"연결 유효":route.error);}
            }else{
                var stops=drafting?draft:selected.stops;stopIndex=Math.Min(stopIndex,Math.Max(0,stops.Count-1));
                bool compactDetail=!drafting&&selected!=null&&StationContextPanel!=null;
                if(!compactDetail)for(int i=0;i<stops.Count;i++){var s=stops[i];text.AppendLine($"{(drafting&&DraftIssues.Any(issue=>issue.StationId==s.stationId)?"[오류] ":"")}{(i==stopIndex?">":"")} {i+1}. {Network.StationName(s.stationId)}{(Network.StationActive(s.stationId)?"":" [비활성]")}{((drafting?ShuttleDraft:selected.IsShuttle)?" · 양방향 연결":$" · 진입 {s.arrival+1} / 진출 {s.departure+1}")}");}
                if(selected!=null&&!drafting){if(!compactDetail)for(int leg=0;leg<selected.legs.Count;leg++)text.AppendLine($"구간 {leg+1} → {(leg+1)%stops.Count+1}: {selected.legs[leg].cells.Count}칸 · {(string.IsNullOrEmpty(selected.error)?"연결 유효":"노선 오류 확인")}");text.AppendLine($"총 선로 {selected.legs.Sum(l=>l.cells.Count)}칸 · {stops.Count}개 역");text.AppendLine("편성 운전칸 1 · 화물칸 1 · 방어칸 1");}
                if(!compactDetail&&(drafting?ShuttleDraft:selected.IsShuttle))text.AppendLine("같은 선로 왕복 · 연결구 자동 선택 · 별도 복귀선 없음");
                else if(!compactDetail)text.AppendLine((drafting&&!loopClosed?"순환 열림 · 마지막 → 첫 역 연결 필요":"마지막 역 → 첫 역으로 순환")+" / 포트 1:서 2:동 3:남 4:북 (배치 방향 기준)");
                if(selected!=null&&!drafting){var t=selected.train;var rule=stops[stopIndex];
                    if(StationContextPanel!=null){StationContextHeader.text=$"선택 역 · {Network.StationName(rule.stationId)}\n{(Network.StationActive(rule.stationId)?"연결됨":"비활성")} · {stopIndex+1}/{stops.Count} 정차";stationPage=Math.Min(stationPage,Math.Max(0,(stops.Count-1)/Math.Max(1,StationRows.Length)));for(int i=0;i<StationRows.Length;i++){int index=stationPage*StationRows.Length+i;var row=StationRows[i];row.gameObject.SetActive(index<stops.Count);if(index<stops.Count)row.GetComponentInChildren<TMP_Text>().text=$"{(index==stopIndex?"> ":"")}{index+1}. {Network.StationName(stops[index].stationId)}";}}
                    if(displayedRoute!=selected.id||displayedStop!=stopIndex||displayedRevision!=selected.revision){Resource.SetTextWithoutNotify(t.HasCargoResource?t.resource:"iron");Load.SetTextWithoutNotify(rule.load.ToString());Unload.SetTextWithoutNotify(rule.unload.ToString());displayedRoute=selected.id;displayedStop=stopIndex;displayedRevision=selected.revision;}text.AppendLine($"{(t.waitingForStation?"역 진입 대기":Status(t.status))}{(selected.IsShuttle?(t.reverse?" · 귀환":" · 전진"):"")} · 현재 {t.stop+1}번 역/구간\n화물 {(t.HasCargoResource?t.resource:"미지정")} {t.cargo:0.##}/{RailwayNetwork.CargoCapacity:0} · 석탄 {t.fuel:0.##}/{stops.Count*RailwayNetwork.FuelPerStation}\n선택 역 적재 {rule.load:0.##} / 하역 {rule.unload:0.##}\n예상 {selected.ExpectedSeconds:0}초 · 정차 남음 {t.dwell:0.0}초\n{selected.error}");if(Network.HasPending(selected)){text.AppendLine(selected.pending.failed?"예약 적용 실패 · 기존 노선 유지: "+selected.pending.error:"예약 변경 대기 · 기존 첫 역 복귀 때 적용");text.AppendLine("예약: "+string.Join(" → ",selected.pending.stops.Select(v=>Network.StationName(v.stationId))));}text.AppendLine("다음 역: "+Network.StationName(stops[selected.NextStopIndex].stationId));if(t.status==TrainStatus.StopRequested)text.AppendLine("중지 예정: "+Network.StationName(stops[selected.NextStopIndex].stationId));Sandbox.RailwayView?.Highlight(selected,Sandbox.Content);}
                else {if(editing!=null)text.AppendLine(AnchorLocked?"예약 변경: 기존 첫 역 유지 · 기존 역 설정 보존":"정지 편집: 순서·첫 역 변경 가능 · 정차 역 유지");text.AppendLine(valid?"검증 통과 · 확정 가능":ShuttleDraft?"연결 경로를 확인하고 검증하세요. 복귀선은 필요하지 않습니다.":"미검증 · 포트와 순환 연결을 확인하세요.");}
            }
            if(drafting&&draftValidation!=null)foreach(var segment in draftValidation.Segments)text.AppendLine($"구간 {segment.Index+1}: {(segment.Status==RouteSegmentStatus.Valid?"[정상]":segment.Status==RouteSegmentStatus.Error?"[오류]":"[검사 불가] 역 설정 확인")}");
            if(drafting&&previewLegs!=null){
                for(int i=0;i<previewLegs.Count;i++)text.AppendLine($"구간 {i+1} → {(i+1)%draft.Count+1}: {previewLegs[i].cells.Count}칸 · {previewLegs[i].cells.Count}초");
                text.AppendLine($"총 {previewLegs.Sum(l=>l.cells.Count)}칸 · 예상 {previewLegs.Sum(l=>l.cells.Count)*(ShuttleDraft?2:1)+(ShuttleDraft?2*(draft.Count-1):draft.Count)*RailwayNetwork.DwellSeconds}초 (역당 10초 정차 포함)");
                text.AppendLine(editing==null?"확정하면 기차가 정지 상태로 생성됩니다. 자동 운행하지 않습니다.":"확정 시 최신 연결 상태를 다시 검사합니다.");
                Sandbox.RailwayView.HighlightPreview(draft,previewLegs,Sandbox.Content);
            }else if(drafting)Sandbox.RailwayView.Highlight(null,Sandbox.Content);
            if(drafting&&DraftIssue!=null){
                var issue=DraftIssue;
                text.AppendLine($"오류 · {issue.Message}");
                if(issue.LegIndex>=0)text.AppendLine($"{(issue.ReturnLeg?"복귀 구간":"구간")} {issue.LegIndex+1}: {Network.StationName(issue.StationId)} → {Network.StationName(issue.DestinationId)}");
                if(issue.ConflictingRouteId!=null)text.AppendLine("점유 노선: "+(Network.Routes.FirstOrDefault(r=>r.id==issue.ConflictingRouteId)?.name??issue.ConflictingRouteId));
                text.AppendLine("수정 안내: "+issue.Recovery);
                Sandbox.RailwayView.HighlightError(issue.Cell,Sandbox.Content);
                if(issue.StationId==draftSelectionId)Sandbox.RailwayView.HighlightIssuePort(issue.Port);
            }
            if(drafting&&stage==EditorStage.DiscardPrompt)text.AppendLine("계속 편집하면 초안을 유지합니다. 폐기하면 실제 노선은 변경하지 않습니다.");
            Summary.text=text.ToString();var rect=Summary.rectTransform;float height=Math.Max(222,Summary.preferredHeight+12);rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);((RectTransform)rect.parent).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);
        }
        double nextRefresh;
        void Update(){if(Panel.activeSelf&&Panel.activeInHierarchy&&Time.unscaledTimeAsDouble>=nextRefresh){nextRefresh=Time.unscaledTimeAsDouble+.1;Refresh();}}
    }
}
