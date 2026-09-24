using UnityEngine;
using UnityEngine.InputSystem;
using EternalSteam.Demo;
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
        public void SelectContent(BuildingDefinition definition){
            if(IsEditing&&Tool==WorldTool.Content&&ReferenceEquals(SelectedDefinition,definition)){Select(WorldTool.Edit);Sandbox.Message="건물 선택 해제 · 좌클릭 드래그: 사각형 회수 선택";return;}
            Select(WorldTool.Content);SelectedDefinition=definition;
            Sandbox.Message=WallPlacementStroke.Supports(definition)?"첫 클릭: 방벽 시작점 · 다음 클릭: 일직선 끝점 · Shift+드래그: 사각형 회수 선택 · 같은 목록 클릭: 선택 해제":"빈 위치 클릭: 배치 · 드래그: 사각형 회수 선택";
        }
        public bool ChoosingDirection => false;
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
        GameObject preview;
        LineRenderer directionLine;
        public void BeginEditing()
        {
            if((Sandbox.Content?.Defeated==true||Sandbox.Persistence?.Blocked==true))return;
            if(IsEditing)return;
            ClearRangeSelection();Tool=WorldTool.Edit;if(Sandbox.Assault==null)Sandbox.Running=false;Sandbox.WorldGrid?.SetVisible(true);
            Sandbox.Message="기존 건물 클릭: 회수 예정 선택/해제 · 좌클릭 드래그: 사각형 선택 · 새 배치는 목록에서 선택하세요.";
        }
        public void Select(WorldTool tool,HordeTowerKind kind=HordeTowerKind.MachineGun)
        {
            if((Sandbox.Content?.Defeated==true||Sandbox.Persistence?.Blocked==true))return;
            if(tool==WorldTool.Explore){Cancel();return;}
            if(tool==WorldTool.Direction){Sandbox.Message="포탑은 자동 조준합니다.";return;}
            if(IsEditing && (Tool==WorldTool.Recover || tool==WorldTool.Recover) && (Edits?.Count??0)>0) {Sandbox.Message="현재 작업을 확정하거나 취소하세요.";return;}
            AbortPointer();SelectedDefinition=null;ClearRangeSelection();Tool=tool;Kind=kind;Moving=false;SelectedPending=null;SelectedFoundation=null;if(Sandbox.Assault==null)Sandbox.Running=false;
            Sandbox.Message=tool==WorldTool.Foundation?"지형 클릭: 토대 임시 배치 → 확정":tool==WorldTool.Tower?"기지 격자 칸 클릭 → 복수 배치 후 확정 · 자동 조준":"포탑 클릭: 회수 예정 선택/해제 → 확정";
        }
        void Start()
        {
            Edits=new WorldEditSession(Sandbox.Foundations,Sandbox.LineMaterial,Sandbox.Content){legacyGroundTowers=Sandbox.Towers};
            drag=new ConstructionDragEditor(this,Sandbox,Edits);
            preview=GameObject.CreatePrimitive(PrimitiveType.Cube);preview.name="45 degree construction preview";
            preview.GetComponent<Collider>().enabled=false;preview.SetActive(false);
            directionLine=HordeVisualPrimitives.MakeLine("Circular range preview",transform,Sandbox.ValidMaterial,.12f,35);directionLine.enabled=false;
        }
        public void Cancel()
        {
            AbortPointer();ClearRangeSelection();SelectedDefinition=null;Edits?.Cancel();
            Tool=WorldTool.Explore;Sandbox.WorldGrid?.SetVisible(false);Moving=false;SelectedPending=null;SelectedFoundation=null;
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
                bool ok=Edits.AddContent(SelectedDefinition,point,Vector3.forward,out reason);Sandbox.Message=reason;return ok;
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
            Sandbox.Content?.ShowBuildAreas(IsEditing);
            Sandbox.WorldGrid?.SetVisible(IsEditing);
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
            Vector3 center=hit.point;bool valid=false;Quaternion rotation=WorldGridGeometry.Rotation;
            if(Tool!=WorldTool.Explore && Tool!=WorldTool.Edit) {
                if(Tool==WorldTool.Content&&SelectedDefinition!=null){
                    if(Sandbox.Content.Resolve(SelectedDefinition,hit.point,out var session,out var world,out var cell,out _)){
                        center=world.Grid.Center(cell,SelectedDefinition.Footprint);var req=new PlacementRequest(-1,SelectedDefinition,cell);valid=session.Validate(req).Success;
                        if(world==Sandbox.Content.GroundWorld){if(Sandbox.Content.CheckGround(cell,SelectedDefinition.Footprint,out float h,out _))center.y=h+.05f;else center.y=Sandbox.Ground.SampleHeight(center)+Sandbox.Ground.transform.position.y;}
                    }
                    preview.transform.localScale=new Vector3(SelectedDefinition.Footprint.x*2-.1f,.12f,SelectedDefinition.Footprint.y*2-.1f);
                }
                else if(Tool==WorldTool.Foundation){valid=Sandbox.Foundations.CheckFoundation(hit.point,out center,out _)&&Edits.FoundationAt(hit.point)==null;preview.transform.localScale=new Vector3(7.9f,.12f,7.9f);}
                else {
                    if(Sandbox.Foundations.FindTowerCell(hit.point,out var p,out var cell,out center)) {
                        rotation=p.World.Grid.Rotation;
                        valid=Tool==WorldTool.Recover?p.World.Grid.IsOccupied(cell):p.Placement.Validate(new PlacementRequest(-1,Sandbox.Foundations.Definition(Kind),cell)).Success;
                    }
                    preview.transform.localScale=new Vector3(1.8f,.12f,1.8f);
                }
                preview.transform.SetPositionAndRotation(center+Vector3.up*.09f,rotation);
                preview.GetComponent<Renderer>().sharedMaterial=valid?Sandbox.ValidMaterial:Sandbox.InvalidMaterial;preview.SetActive(true);
            }

        }
        void ClearRangeSelection(){SelectedContent=null;SelectedTower=null;if(directionLine!=null)directionLine.enabled=false;}
        void LateUpdate()
        {
            if(directionLine==null)return;directionLine.enabled=false;
            if((Sandbox.Content?.Defeated==true||Sandbox.Persistence?.Blocked==true))return;
            if(IsEditing)return;
            if(SelectedContent!=null&&!SelectedContent.Disposed&&SelectedContent.Module<WeaponRuntime>() is WeaponRuntime weapon)RangeCircle(SelectedContent.Position,weapon.Range);
            else if(SelectedTower?.root!=null&&SelectedTower.building!=null&&!SelectedTower.building.Disposed)RangeCircle(SelectedTower.root.transform.position,SelectedTower.range);
        }
        void OnApplicationFocus(bool focused){if(!focused)AbortPointer();}
        void OnDisable()=>AbortPointer();
        void OnDestroy(){drag?.Dispose();Edits?.Dispose();if(preview!=null)Destroy(preview);}
    }
}
