using UnityEngine;
using UnityEngine.InputSystem;
using EternalSteam.Demo;
using EternalSteam.Railway;
using System.Linq;
namespace EternalSteam.OpenWorld
{
    public enum WorldTool { Explore, Foundation, Tower, Recover, Direction, Edit, Content }
    public sealed class OpenWorldInput : MonoBehaviour
    {
        public OpenWorldSandbox Sandbox;
        public WorldTool Tool { get; private set; }
        public HordeTowerKind Kind { get; private set; }
        public bool PointerOverUI;
        public BuildingDefinition SelectedDefinition {get;private set;}
        public BuildingInstance SelectedContent {get;private set;}
        Vector3? stationAnchor;
        Vector3 stationDirection=WorldGridGeometry.Rotation*Vector3.forward;
        bool PlacingStation=>SelectedDefinition?.Modules.OfType<RailFacilityDefinition>().Any(r=>r.Kind==RailFacilityKind.Station)==true;
        public bool ChoosingStationDirection=>stationAnchor.HasValue;
        public Vector3 StationDirection=>stationDirection;
        public static Vector3 SnapStationDirection(Vector3 delta)
        {
            delta.y=0;var axes=new[]{WorldGridGeometry.Rotation*Vector3.forward,WorldGridGeometry.Rotation*Vector3.right,WorldGridGeometry.Rotation*Vector3.back,WorldGridGeometry.Rotation*Vector3.left};
            if(delta.sqrMagnitude<.01f)return axes[0];var best=axes[0];float score=float.NegativeInfinity;foreach(var axis in axes){float dot=Vector3.Dot(delta,axis);if(dot>score){score=dot;best=axis;}}return best;
        }
        public void SelectContent(BuildingDefinition definition){
            if(Sandbox.Content.Defeated||Sandbox.Persistence?.Blocked==true)return;
            if(!Sandbox.Content.MeetsBaseLevel(definition,out var reason)){Sandbox.Message=reason;return;}
            if(IsEditing&&Tool==WorldTool.Content&&ReferenceEquals(SelectedDefinition,definition)){Select(WorldTool.Edit);Sandbox.Message="건물 선택 해제 · 좌클릭 드래그: 사각형 회수 선택";return;}
            Select(WorldTool.Content);SelectedDefinition=definition;
            Sandbox.Message=PlacingStation?"첫 클릭: 역 위치 · 마우스 이동: 방향 · 두 번째 클릭: 방향 고정 및 임시 배치":WallPlacementStroke.Supports(definition)?"첫 클릭: 방벽 시작점 · 다음 클릭: 일직선 끝점 · Shift+드래그: 사각형 회수 선택 · 같은 목록 클릭: 선택 해제":"빈 위치 클릭: 배치 · 드래그: 사각형 회수 선택";
        }
        public bool ChoosingDirection => stationAnchor.HasValue;
        public bool IsEditing => Tool!=WorldTool.Explore;
        public WorldEditSession Edits { get; private set; }
        public WorldEditSession.PendingTower SelectedPending { get; private set; }
        public WorldEditSession.PendingFoundation SelectedFoundation {get;private set;}
        public bool HasSelection => SelectedPending!=null || SelectedFoundation!=null;
        public HordeTower SelectedTower { get; private set; }
        public bool Moving {get;private set;}
        ConstructionDragEditor drag;
        public bool Dragging=>drag?.Active==true;
        public void BeginPointer(Vector2 screen,Vector3? world,bool forceRectangle=false){if(IsEditing&&!Moving&&!PointerOverUI)drag?.Begin(screen,world,forceRectangle);}
        public void MovePointer(Vector2 screen,Vector3? world,bool overUI=false)=>drag?.Move(screen,world,overUI);
        public void EndPointer(Vector2 screen,Vector3? world,bool overUI=false)=>drag?.Release(screen,world,overUI);
        public void AbortPointer()=>drag?.Abort();
        [SerializeField] GameObject preview;
        [SerializeField] Material inactivePreviewMaterial;
        [SerializeField] LineRenderer invalidPattern;
        public LineRenderer DragRectangle;
        public string PlacementHint {get;private set;}="";
        [SerializeField] LineRenderer directionLine;
        public void BeginEditing()
        {
            if((Sandbox.Content?.Defeated==true||Sandbox.Persistence?.Blocked==true))return;
            if(IsEditing)return;
            ClearRangeSelection();Tool=WorldTool.Edit;if(Sandbox.Assault==null)Sandbox.Running=false;Sandbox.WorldGrid?.SetVisible(true);Sandbox.BuildAreaHologram?.SetVisible(true);
            Sandbox.Message="기존 건물 클릭: 회수 예정 선택/해제 · 좌클릭 드래그: 사각형 선택 · 새 배치는 목록에서 선택하세요.";
        }
        public void Select(WorldTool tool,HordeTowerKind kind=HordeTowerKind.MachineGun)
        {
            if((Sandbox.Content?.Defeated==true||Sandbox.Persistence?.Blocked==true))return;
            if(tool==WorldTool.Explore){Cancel();return;}
            if(tool==WorldTool.Direction){Sandbox.Message="포탑은 자동 조준합니다.";return;}
            if(IsEditing && (Tool==WorldTool.Recover || tool==WorldTool.Recover) && (Edits?.Count??0)>0) {Sandbox.Message="현재 작업을 확정하거나 취소하세요.";return;}
            AbortPointer();stationAnchor=null;SelectedDefinition=null;ClearRangeSelection();Tool=tool;Kind=kind;Moving=false;SelectedPending=null;SelectedFoundation=null;if(Sandbox.Assault==null)Sandbox.Running=false;
            Sandbox.Message=tool==WorldTool.Foundation?"지형 클릭: 토대 임시 배치 → 확정":tool==WorldTool.Tower?"기지 격자 칸 클릭 → 복수 배치 후 확정 · 자동 조준":"포탑 클릭: 회수 예정 선택/해제 → 확정";
        }
        void Start()
        {
            Edits=new WorldEditSession(Sandbox.Foundations,Sandbox.LineMaterial,Sandbox.Content){legacyGroundTowers=Sandbox.Towers};
            drag=new ConstructionDragEditor(this,Sandbox,Edits);
            if(preview==null||invalidPattern==null||directionLine==null||inactivePreviewMaterial==null)
                throw new System.InvalidOperationException("Assign construction preview, pattern, range and material from the saved scene hierarchy.");
            preview.SetActive(false);invalidPattern.enabled=false;directionLine.enabled=false;
        }
        public void Cancel()
        {
            AbortPointer();stationAnchor=null;ClearRangeSelection();SelectedDefinition=null;Edits?.Cancel();PlacementHint="";if(preview!=null)preview.SetActive(false);
            Tool=WorldTool.Explore;Sandbox.Content?.ShowBuildAreas(false);Sandbox.WorldGrid?.SetVisible(false);Sandbox.BuildAreaHologram?.SetVisible(false);Moving=false;SelectedPending=null;SelectedFoundation=null;
            Sandbox.Message="WASD 이동 · 휠 확대/축소 · 3D 토대 격자 45°";
        }
        public bool Confirm()
        {
            if(Sandbox.Content.Defeated||Sandbox.Persistence?.Blocked==true)return false;
            AbortPointer();var result=Edits.Confirm();
            if(!result.Success){Sandbox.Message=result.Message;return false;}
            Cancel();Sandbox.Message="작업을 확정했습니다.";Sandbox.Persistence?.RequestAutoSave();return true;
        }
        public bool CanConfirm => Edits!=null&&Edits.Count>0;
        public void RemoveSelected() {Edits.Remove(SelectedFoundation);Edits.Remove(SelectedPending);SelectedPending=null;SelectedFoundation=null;Moving=false;}
        public void MoveSelected() {if(HasSelection){Moving=true;Sandbox.Message="이동할 토대 칸 클릭 · 실패 시 기존 예약 유지";}}
        public bool BeginDirection(){Sandbox.Message="포탑은 사거리 안의 적을 자동 조준합니다.";return false;}
        public bool ClickWorld(Vector3 point)
        {
            if(Sandbox.Content.Defeated||Sandbox.Persistence?.Blocked==true)return false;
            string reason;
            if(IsEditing && !Moving) {
                var pendingContent=Edits.ContentAt(point);if(pendingContent!=null){Edits.Remove(pendingContent);Sandbox.Message="임시 배치를 제거했습니다.";return true;}
                if(Sandbox.Content.GroundAt(point,out var groundBuilding)){bool ok=Edits.ToggleGroundRecovery(groundBuilding,out reason);Sandbox.Message=reason;return ok;}
                var pendingTower=Edits.At(point);var pendingFoundation=Edits.FoundationAt(point);
                if(pendingTower!=null || pendingFoundation!=null){Edits.Remove(pendingTower);Edits.Remove(pendingFoundation);SelectedPending=null;SelectedFoundation=null;Sandbox.Message="임시 배치를 제거했습니다.";return true;}
                if(Sandbox.Foundations.FindCell(point,out var platform,out var occupiedCell,out _)) {
                    if(platform.World.Grid.IsOccupied(occupiedCell)) {
                        bool ok=Edits.ToggleRecovery(point,Sandbox.Towers,out reason);Sandbox.Message=reason;return ok;
                    }
                    if(Tool!=WorldTool.Tower && !(Tool==WorldTool.Content&&SelectedDefinition?.Placement.Surface!=BuildingSurface.Ground)) {
                        bool ok=Edits.ToggleFoundationRecovery(platform,Sandbox.Towers,out reason);Sandbox.Message=reason;return ok;
                    }
                }
            }
            if(Tool==WorldTool.Explore || Tool==WorldTool.Edit) {
                SelectedContent=null;SelectedTower=null;
                if(Sandbox.Content.GroundAt(point,out var g))SelectedContent=g;
                else if(Sandbox.Foundations.FindCell(point,out var cp,out var cc,out _)&&cp.World.Grid.OccupantAt(cc) is int ci&&cp.World.TryGet(ci,out var cb)&&Sandbox.Content.Views.ContainsKey(cb))SelectedContent=cb;
                if(SelectedContent!=null){if(SelectedContent.Module<IBaseIdentity>() is IBaseIdentity identity)Sandbox.Content.Bases.Select(identity.BaseId);foreach(var t in Sandbox.Towers)if(ReferenceEquals(t.building,SelectedContent)){SelectedTower=t;SelectedContent=null;Sandbox.Message="포탑 선택 · 원형 사거리 내 자동 공격";return true;}Sandbox.Message=$"{SelectedContent.DisplayName} · Lv.{SelectedContent.Module<IUpgradeControl>()?.Level??1}";return true;}
                SelectedTower=null;
                if(Sandbox.Foundations.FindCell(point,out var p,out var cell,out _) && p.World.Grid.OccupantAt(cell) is int id)
                    foreach(var t in Sandbox.Towers)if(p.World.TryGet(id,out var building)&&ReferenceEquals(t.building,building)){SelectedTower=t;break;}
                Sandbox.Message=SelectedTower==null?"수정 후 기존 건물 클릭: 회수 예정 선택 · 목록 선택: 새 배치":"포탑 선택됨 · 수정 후 클릭하면 회수 예정";return SelectedTower!=null;
            }
            if(Tool==WorldTool.Content) {
                if(SelectedDefinition==null)return false;
                if(PlacingStation){
                    if(!stationAnchor.HasValue){if(!Edits.PreviewContent(SelectedDefinition,point,out _,out _,out _,out reason)){Sandbox.Message=reason;return false;}stationAnchor=point;stationDirection=SnapStationDirection(Vector3.zero);Sandbox.Message="역 위치 선택 · 마우스로 방향을 정하고 다시 클릭해 고정하세요.";return true;}
                    stationDirection=SnapStationDirection(point-stationAnchor.Value);point=stationAnchor.Value;stationAnchor=null;
                }
                bool ok=Edits.AddContent(SelectedDefinition,point,PlacingStation?stationDirection:Vector3.forward,out reason);Sandbox.Message=reason;return ok;
            }
            if(Edits.RecoveryCount>0 && (Tool==WorldTool.Foundation || Tool==WorldTool.Tower)){Sandbox.Message="회수 작업을 먼저 확정하거나 취소하세요.";return false;}
            if(Tool==WorldTool.Foundation){
                if(Moving) {bool moved=Edits.Move(SelectedFoundation,point,out reason);Sandbox.Message=reason;if(moved)Moving=false;return moved;}
                SelectedFoundation=Edits.FoundationAt(point);
                if(SelectedFoundation!=null){Sandbox.Message="임시 토대 선택 · 이동 / 개별 제거 가능";return true;}
                bool ok=Edits.AddFoundation(point,out reason);Sandbox.Message=reason;return ok;
            }
            if(Tool==WorldTool.Recover){bool ok=Edits.ToggleRecovery(point,Sandbox.Towers,out reason);Sandbox.Message=reason;return ok;}
            if(Moving) {bool moved=Edits.Move(SelectedPending,point,out reason);Sandbox.Message=reason;if(moved)Moving=false;return moved;}
            bool added=Edits.AddTower(point,Vector3.forward,Kind,out reason);Sandbox.Message=reason;return added;
        }
        void RangeCircle(Vector3 origin,float range)
        {
            directionLine.enabled=true;directionLine.positionCount=65;origin+=Vector3.up*.25f;
            for(int i=0;i<=64;i++){float a=i*Mathf.PI*2/64;directionLine.SetPosition(i,origin+new Vector3(Mathf.Cos(a)*range,0,Mathf.Sin(a)*range));}
        }
        void Update()
        {
            Sandbox.Content?.ShowBuildAreas(IsEditing&&Sandbox.BuildAreaHologram==null);
            Sandbox.BuildAreaHologram?.SetVisible(IsEditing);
            Sandbox.WorldGrid?.SetVisible(IsEditing);
            PlacementHint="";if(preview!=null)preview.SetActive(false);
            var mouse=Mouse.current;if(mouse==null||Edits==null)return;
            if((Keyboard.current?.escapeKey.wasPressedThisFrame??false)||mouse.rightButton.wasPressedThisFrame){Cancel();return;}
            preview.SetActive(false);directionLine.enabled=false;
            var screen=mouse.position.ReadValue();
            var ray=Sandbox.CameraRig.View.ScreenPointToRay(screen);
            bool hasHit=Physics.Raycast(ray,out var hit,1000);
            Vector3? worldPoint=hasHit?hit.point:(Vector3?)null;
            if(mouse.leftButton.wasPressedThisFrame&&!PointerOverUI){
                if(IsEditing&&!Moving)BeginPointer(screen,worldPoint,(Keyboard.current?.leftShiftKey.isPressed??false)||(Keyboard.current?.rightShiftKey.isPressed??false));
                else if(hasHit)ClickWorld(hit.point);
            }
            if(Dragging){
                if(mouse.leftButton.wasReleasedThisFrame)EndPointer(screen,worldPoint,PointerOverUI);
                else if(mouse.leftButton.isPressed)MovePointer(screen,worldPoint,PointerOverUI);
                else AbortPointer();
            }
            if(!Dragging)drag?.PreviewWall(worldPoint,PointerOverUI);
            if(PointerOverUI||!hasHit)return;
            if(stationAnchor.HasValue)stationDirection=SnapStationDirection(hit.point-stationAnchor.Value);
            Vector3 center=stationAnchor??hit.point;bool valid=false,inactive=false;Quaternion rotation=WorldGridGeometry.Rotation;
            if(Tool!=WorldTool.Explore && Tool!=WorldTool.Edit) {
                if(Tool==WorldTool.Content&&SelectedDefinition!=null){
                    valid=Edits.PreviewContent(SelectedDefinition,stationAnchor??hit.point,out _,out var world,out var cell,out var reason);
                    if(world!=null){
                        center=world.Grid.Center(cell,SelectedDefinition.Footprint);
                        if(world==Sandbox.Content.GroundWorld){if(Sandbox.Content.CheckGround(cell,SelectedDefinition.Footprint,out float h,out _))center.y=h+.05f;else center.y=Sandbox.Ground.SampleHeight(center)+Sandbox.Ground.transform.position.y;}
                        if(stationAnchor.HasValue)Sandbox.RailwayView.PresentPlacementPorts(cell,stationDirection,Sandbox.Content);
                    }
                    var placement=SelectedDefinition.Placement;
                    bool needsArea=placement!=null&&(placement.RequiresOperationalArea||(Sandbox.Content.Bases.AnyNormalBaseCoverage&&placement.RequiresOwnerBase));
                    inactive=valid&&needsArea&&!Sandbox.Content.Bases.Covers(center,(Vector2)SelectedDefinition.Footprint,WorldGridGeometry.Rotation,Sandbox.Content.Bases.SelectedBaseId);
                    bool lostOwner=placement!=null&&placement.RequiresOwnerBase&&!Sandbox.Content.Bases.AnyNormalBaseCoverage&&
                        (Sandbox.Content.Bases.SelectedBaseId==null||!Sandbox.Content.Bases.Bases.TryGetValue(Sandbox.Content.Bases.SelectedBaseId,out var owner)||!owner.Active);
                    inactive|=valid&&lostOwner;
                    var quote=Edits.Quote(SelectedDefinition);var cost=quote.HasItems?" · "+quote.Summary:"";
                    PlacementHint=(!valid?"설치 불가: "+(reason??"설치 위치를 확인하세요."):inactive?"설치 가능 · 비작동 (가동 영역 또는 소속 기지 없음)":needsArea?"설치 가능 · 가동 영역 충족 (전력 등은 확정 후 판정)":"설치 가능")+cost;
                    preview.transform.localScale=new Vector3(SelectedDefinition.Footprint.x*2-.1f,.12f,SelectedDefinition.Footprint.y*2-.1f);
                }
                else if(Tool==WorldTool.Foundation){valid=Edits.PreviewFoundation(hit.point,out center,out var reason);PlacementHint=(valid?"설치 가능 · 토대":"설치 불가: "+reason)+" · "+Edits.Quote(foundation:true).Summary;preview.transform.localScale=new Vector3(7.9f,.12f,7.9f);}
                else if(Tool==WorldTool.Tower){
                    valid=Edits.PreviewTower(hit.point,Vector3.forward,Kind,out var platform,out _,out center,out var reason);
                    if(platform!=null)rotation=platform.World.Grid.Rotation;
                    var placement=Sandbox.Foundations.Definition(Kind).Placement;
                    bool needsArea=placement!=null&&(placement.RequiresOperationalArea||(Sandbox.Content.Bases.AnyNormalBaseCoverage&&placement.RequiresOwnerBase));
                    inactive=valid&&needsArea&&!Sandbox.Content.Bases.Covers(center,Vector2.one,rotation,Sandbox.Content.Bases.SelectedBaseId);
                    PlacementHint=!valid?"설치 불가: "+reason:inactive?"설치 가능 · 비작동 (가동 영역 밖)":"설치 가능 · 가동 조건은 확정 후 판정";
                    PlacementHint+=" · "+Edits.Quote(Sandbox.Foundations.Definition(Kind)).Summary;
                    preview.transform.localScale=new Vector3(1.8f,.12f,1.8f);
                }
                else {
                    if(Sandbox.Foundations.FindTowerCell(hit.point,out var p,out var cell,out center)) {
                        rotation=p.World.Grid.Rotation;
                        valid=Tool==WorldTool.Recover?p.World.Grid.IsOccupied(cell):p.Placement.Validate(new PlacementRequest(-1,Sandbox.Foundations.Definition(Kind),cell)).Success;
                    }
                    preview.transform.localScale=new Vector3(1.8f,.12f,1.8f);
                }
                preview.transform.SetPositionAndRotation(center+Vector3.up*.09f,rotation);
                preview.GetComponent<Renderer>().sharedMaterial=!valid?Sandbox.InvalidMaterial:inactive?inactivePreviewMaterial:Sandbox.ValidMaterial;preview.SetActive(true);
                invalidPattern.enabled=!valid;
                if(!valid){var half=preview.transform.localScale*.5f;var a=center+rotation*new Vector3(-half.x,.2f,-half.z);var b=center+rotation*new Vector3(half.x,.2f,half.z);var c=center+rotation*new Vector3(-half.x,.2f,half.z);var d=center+rotation*new Vector3(half.x,.2f,-half.z);invalidPattern.SetPosition(0,a);invalidPattern.SetPosition(1,b);invalidPattern.SetPosition(2,c);invalidPattern.SetPosition(3,d);}
                if(stationAnchor.HasValue){var origin=center+Vector3.up*.25f;var tip=origin+stationDirection*3;var wing=WorldGridGeometry.Rotation*Vector3.right;directionLine.positionCount=4;directionLine.SetPosition(0,origin);directionLine.SetPosition(1,tip);directionLine.SetPosition(2,tip-stationDirection*.75f+wing*.55f);directionLine.SetPosition(3,tip);directionLine.enabled=true;PlacementHint+=" · 마우스로 방향 선택 → 클릭 고정";}
            }

        }
        public void ClearSelection(){if(!IsEditing)ClearRangeSelection();}
        void ClearRangeSelection(){SelectedContent=null;SelectedTower=null;if(directionLine!=null)directionLine.enabled=false;}
        void LateUpdate()
        {
            if(directionLine==null)return;if(stationAnchor.HasValue)return;directionLine.enabled=false;
            if((Sandbox.Content?.Defeated==true||Sandbox.Persistence?.Blocked==true))return;
            if(IsEditing)return;
            if(SelectedContent!=null&&!SelectedContent.Disposed&&SelectedContent.Module<WeaponRuntime>() is WeaponRuntime weapon)RangeCircle(SelectedContent.Position,weapon.Range);
            else if(SelectedTower?.root!=null&&SelectedTower.building!=null&&!SelectedTower.building.Disposed)RangeCircle(SelectedTower.root.transform.position,SelectedTower.range);
        }
        void OnApplicationFocus(bool focused){if(!focused)AbortPointer();}
        void OnDisable(){PlacementHint="";AbortPointer();if(Sandbox!=null){Sandbox.WorldGrid?.SetVisible(false);Sandbox.BuildAreaHologram?.SetVisible(false);Sandbox.Content?.ShowBuildAreas(false);}if(preview!=null)preview.SetActive(false);}
        void OnDestroy(){drag?.Dispose();Edits?.Dispose();}
    }
}
