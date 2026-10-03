using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EternalSteam.Demo;
using EternalSteam.Railway;
namespace EternalSteam.OpenWorld
{
    // Editing state is separate from combat/input. Reservations survive selection changes.
    public sealed class WorldEditSession : IDisposable
    {
        public sealed class PendingTower
        {
            public FoundationPlacement.Platform Platform;
            public PlacementRequest Request;
            public HordeTowerKind Kind;
            public GameObject View;
            public ConstructionVisual Visual;
            public string BaseId;
        }
        public sealed class PendingFoundation
        { public Vector3 Point;public GameObject View;public ConstructionVisual Visual;public string BaseId; }
        public sealed class PendingContent
        {public BuildingWorld World;public PlacementSession Session;public PlacementRequest Request;public GameObject View;public ConstructionVisual Visual;public string BaseId;}
        public sealed class ConstructionQuote
        {
            public sealed class CostLine
            {public string Resource;public double Required,Available,Shortage;}
            public int Tracks,Stations,Items,VerificationFreeItems;
            public string BaseId,BaseName,Reason;
            public readonly List<CostLine> Lines=new();
            internal readonly List<ConstructionPurchase.Payment> Payments=new();
            public bool Affordable=>Reason==null;
            public bool HasRail=>Tracks+Stations>0;
            public bool HasItems=>Items>0;
            public double Iron=>Lines.Where(v=>v.Resource=="iron").Sum(v=>v.Required);
            public double Available=>Lines.FirstOrDefault(v=>v.Resource=="iron")?.Available??0;
            public double Shortage=>Lines.FirstOrDefault(v=>v.Resource=="iron")?.Shortage??0;
            static string ResourceName(string id)=>id switch {"iron"=>"철","copper"=>"구리","coal"=>"석탄","nanometal"=>"나노메탈","plasma_ore"=>"플라즈마","titanium"=>"티타늄","tungsten"=>"텅스텐","uranium"=>"우라늄",_=>id};
            public string Summary=>!HasItems?"":(HasRail?$"철도 견적 · 선로 {Tracks}칸 / 역 {Stations}개":"건설 견적")+
                (Lines.Count>0?" · "+BaseName+" · "+string.Join(" / ",Lines.Select(v=>$"{ResourceName(v.Resource)} {v.Required:N0} (보유 {v.Available:N0}·부족 {v.Shortage:N0})")):"")+
                (VerificationFreeItems>0?$" · 일반 {VerificationFreeItems}개 검증 무료":"")+(Reason!=null?" · "+Reason:"");
        }
        readonly List<PendingContent> contentPending=new();
        readonly Dictionary<BuildingInstance,ConstructionVisual> contentRecovery=new();
        readonly OpenWorldContent content;
        public IReadOnlyList<HordeTower> legacyGroundTowers=Array.Empty<HordeTower>();
        RecoverySession groundRecovery;
        public IReadOnlyList<PendingContent> ContentPending=>contentPending;
        readonly FoundationPlacement placement;
        readonly Material line;
        readonly List<PendingTower> towers=new();
        readonly List<PendingFoundation> foundations=new();
        readonly Dictionary<FoundationPlacement.Platform,RecoverySession> recovery=new();
        readonly Dictionary<HordeTower,ConstructionVisual> recovering=new();
        readonly Dictionary<FoundationPlacement.Platform,ConstructionVisual> recoveringFoundations=new();
        public IReadOnlyList<PendingTower> Towers => towers;
        public int RecoveryCount { get {int n=recoveringFoundations.Count+(groundRecovery?.Selected.Count??0);foreach(var r in recovery.Values)n+=r.Selected.Count;return n;} }
        public int Count => towers.Count+foundations.Count+contentPending.Count+RecoveryCount;
        public WorldEditSession(FoundationPlacement placement,Material line,OpenWorldContent content=null) {this.placement=placement;this.line=line;this.content=content;}
        public ConstructionQuote Quote(BuildingDefinition candidate=null,int quantity=1,bool foundation=false)
        {
            var quote=new ConstructionQuote();if(content==null)return quote;
            if(quantity<1||quantity>512){quote.Reason="한 번에 최대 512칸까지 예약할 수 있습니다.";return quote;}
            quote.BaseId=content.Bases.SelectedBaseId;
            quote.BaseName=string.IsNullOrWhiteSpace(quote.BaseId)?"선택 기지 없음":content.Railway?.Network.BaseDisplayName(quote.BaseId)??"선택 기지";
            var totals=new Dictionary<string,double>();
            void Add(BuildingDefinition definition,string baseId,int count,bool slab=false)
            {
                quote.Items+=count;
                if(content.BaseRules&&baseId!=quote.BaseId){quote.Reason="건설 예약의 소속·지불 기지가 변경됐습니다. 원래 기지를 선택하거나 예약을 취소하세요.";return;}
                List<ResourceCost> costs=null;
                if(definition?.Modules.OfType<RailFacilityDefinition>().FirstOrDefault() is RailFacilityDefinition rail){
                    if(rail.Kind==RailFacilityKind.Track)quote.Tracks+=count;else quote.Stations+=count;
                    costs=new(){new ResourceCost{ResourceId="iron",Amount=rail.Kind==RailFacilityKind.Track?1:10}};
                }else {
                    string why=null;
                    if(content.ConstructionCosts==null||!content.ConstructionCosts.TryQuote(slab?ConstructionCostTable.FoundationId:definition.Id,out costs,out why)){
                        quote.Reason=content.ConstructionCosts==null?"건설 비용 정책이 없습니다.":why;return;
                    }
                    if(content.ConstructionCosts is VerificationFreeConstruction)quote.VerificationFreeItems+=count;
                }
                if(costs==null){quote.Reason="건설 비용 목록이 없습니다.";return;}
                foreach(var cost in costs){
                    if(cost==null||string.IsNullOrWhiteSpace(cost.ResourceId)||!double.IsFinite(cost.Amount)||cost.Amount<0){quote.Reason="잘못된 건설 비용 설정입니다.";return;}
                    totals.TryGetValue(cost.ResourceId,out double old);double total=old+cost.Amount*count;
                    if(!double.IsFinite(total)){quote.Reason="건설 비용 범위 오류";return;}
                    totals[cost.ResourceId]=total;
                }
            }
            foreach(var pending in contentPending)Add(pending.Request.Definition,pending.BaseId,1);
            foreach(var pending in towers)Add(pending.Request.Definition,pending.BaseId,1);
            foreach(var pending in foundations)Add(null,pending.BaseId,1,true);
            if(candidate!=null||foundation)Add(candidate,quote.BaseId,quantity,foundation);
            var bank=content.BaseRules?content.Inventories.Available(quote.BaseId):content.Resources;
            foreach(var pair in totals.OrderBy(v=>v.Key,StringComparer.Ordinal))if(pair.Value>0){
                var cost=new ConstructionQuote.CostLine{Resource=pair.Key,Required=pair.Value,Available=bank?.Amount(pair.Key)??0};
                cost.Shortage=bank?.InfiniteResources==true?0:Math.Max(0,cost.Required-cost.Available);quote.Lines.Add(cost);
                if(bank==null)quote.Reason??="건설 비용을 지불할 활성 기지를 선택하세요.";
                else if(cost.Shortage>0)quote.Reason??=$"{quote.BaseName}의 {pair.Key}이 {cost.Shortage:N0} 부족합니다.";
                quote.Payments.Add(new ConstructionPurchase.Payment{Bank=bank,Resource=pair.Key,Amount=pair.Value});
            }
            return quote;
        }
        static void DisableGhost(GameObject view)
        {
            foreach(var collider in view.GetComponentsInChildren<Collider>())collider.enabled=false;
            foreach(var renderer in view.GetComponentsInChildren<LineRenderer>())renderer.enabled=false;
        }
        public bool PreviewContent(BuildingDefinition definition,Vector3 point,out PlacementSession session,out BuildingWorld world,out Vector2Int cell,out string reason)
        {
            session=null;world=null;cell=default;reason=null;if(content==null||definition==null)return false;
            if(!content.Resolve(definition,point,out session,out world,out cell,out reason))return false;
            if(RecoveryCount>0){reason="회수를 먼저 확정하거나 취소하세요.";return false;}
            if(world==content.GroundWorld)foreach(var f in foundations){var d=WorldGridGeometry.ToLocal(world.Grid.Center(cell,definition.Footprint)-WorldGridGeometry.Center(FoundationPlacement.Key(f.Point),8));if(Mathf.Abs(d.x)<4+definition.Footprint.x-.001f && Mathf.Abs(d.z)<4+definition.Footprint.y-.001f){reason="임시 토대와 겹칩니다.";return false;}}
            var result=session.Validate(new PlacementRequest(-1,definition,cell));reason=result.Message;if(!result.Success)return false;
            var quote=Quote(definition);if(!quote.Affordable){reason=quote.Reason;return false;}return true;
        }
        public bool AddContent(BuildingDefinition definition,Vector3 point,Vector3 direction,out string reason)
        {
            if(!PreviewContent(definition,point,out var session,out var world,out var cell,out reason))return false;
            var result=session.Add(definition,cell,out var request);if(!result.Success){reason=result.Message;return false;}
            GameObject view=null;try {
                request.Direction=direction;var center=world.Grid.Center(cell,request.Footprint);
                if(world==content.GroundWorld){content.CheckGround(cell,request.Footprint,out float height,out _);center.y=height+.05f;}
                view=UnityEngine.Object.Instantiate(definition.ViewPrefab,center,WorldGridGeometry.Rotation);DisableGhost(view);
                contentPending.Add(new PendingContent{World=world,Session=session,Request=request,View=view,Visual=new ConstructionVisual(view,line,request.Footprint.x*2,WorldGridGeometry.Rotation,Color.cyan),BaseId=content.Bases.SelectedBaseId});
                reason="임시 배치 — 확정하면 기능이 시작됩니다. · "+Quote().Summary;return true;
            }catch{session.Remove(request.Id);if(view!=null)UnityEngine.Object.Destroy(view);throw;}
        }
        public PendingContent ContentAt(Vector3 point)
        {foreach(var p in contentPending){var cell=p.World.Grid.WorldToCell(point);if(cell.x>=p.Request.Cell.x&&cell.y>=p.Request.Cell.y&&cell.x<p.Request.Cell.x+p.Request.Footprint.x&&cell.y<p.Request.Cell.y+p.Request.Footprint.y)return p;}return null;}
        public void Remove(PendingContent p){if(p==null||!contentPending.Remove(p))return;p.Session.Remove(p.Request.Id);p.Visual.Dispose();UnityEngine.Object.Destroy(p.View);}
        void MarkContent(BuildingInstance b,bool selected)
        {if(content==null||!content.Views.TryGetValue(b,out var view))return;if(!selected){if(contentRecovery.Remove(b,out var v))v.Dispose();}else if(!contentRecovery.ContainsKey(b))contentRecovery.Add(b,new ConstructionVisual(view,line,b.Footprint.x*2,WorldGridGeometry.Rotation,new Color(1,.5f,.1f)));}
        public bool ToggleGroundRecovery(BuildingInstance b,out string reason)
        {
            reason="회수 예정 선택/해제 · 확정 전까지 건물 유지";
            if(towers.Count+foundations.Count+contentPending.Count>0){reason="임시 배치를 먼저 확정하거나 취소하세요.";return false;}
            groundRecovery??=new RecoverySession(content.GroundWorld);if(!groundRecovery.Toggle(b.Id)){reason="회수할 수 없습니다.";return false;}
            MarkContent(b,groundRecovery.Selected.Contains(b.Id));
            if(!content.Views.ContainsKey(b))foreach(var tower in legacyGroundTowers)if(ReferenceEquals(tower.building,b)){if(recovering.Remove(tower,out var old))old.Dispose();else recovering.Add(tower,new ConstructionVisual(tower.root,line,2,WorldGridGeometry.Rotation,new Color(1,.5f,.1f)));break;}
            return true;
        }
        public bool PreviewFoundation(Vector3 point,out Vector3 center,out string reason)
        {
            center=point;
            if(RecoveryCount>0){reason="회수 작업을 먼저 확정하거나 취소하세요.";return false;}
            if(!placement.CheckFoundation(point,out center,out reason))return false;
            var quote=Quote(foundation:true);if(!quote.Affordable){reason=quote.Reason;return false;}
            foreach(var p in foundations)if(FoundationPlacement.Key(p.Point)==FoundationPlacement.Key(point)) {reason="임시 토대가 예약한 자리입니다.";return false;}
            return true;
        }
        public bool AddFoundation(Vector3 point,out string reason)
        {
            if(!PreviewFoundation(point,out var center,out reason))return false;
            var view=placement.CreateFoundationPreview(center);DisableGhost(view);
            foundations.Add(new PendingFoundation{Point=point,BaseId=content?.Bases.SelectedBaseId,View=view,Visual=new ConstructionVisual(view,line,8,WorldGridGeometry.Rotation,Color.cyan)});
            reason="토대 임시 배치 · 확정 후 포탑을 설치할 수 있습니다. · "+Quote().Summary;return true;
        }
        public PendingFoundation FoundationAt(Vector3 point) => foundations.Find(f=>FoundationPlacement.Key(f.Point)==FoundationPlacement.Key(point));
        public bool Move(PendingFoundation foundation,Vector3 point,out string reason)
        {
            if(!foundations.Contains(foundation)){reason="임시 토대가 없습니다.";return false;}
            if(!placement.CheckFoundation(point,out var center,out reason))return false;
            var other=FoundationAt(point);if(other!=null&&other!=foundation){reason="다른 임시 토대의 예약입니다.";return false;}
            foundation.Point=point;foundation.View.transform.position=center-Vector3.up*.2f;
            reason="임시 토대 이동 · 확정 전까지 예약 유지";return true;
        }
        public void Remove(PendingFoundation foundation)
        {if(foundation==null||!foundations.Remove(foundation))return;foundation.Visual.Dispose();UnityEngine.Object.Destroy(foundation.View);}
        public PendingTower At(Vector3 point)
        {
            if(!placement.FindTowerCell(point,out var p,out var cell,out _))return null;
            return towers.Find(t=>t.Platform==p&&t.Request.Cell==cell);
        }
        public bool PreviewTower(Vector3 point,Vector3 direction,HordeTowerKind kind,out FoundationPlacement.Platform p,out Vector2Int cell,out Vector3 center,out string reason)
        {
            p=null;cell=default;center=point;
            if(RecoveryCount>0){reason="회수 작업을 먼저 확정하거나 취소하세요.";return false;}
            if(!placement.FindTowerCell(point,out p,out cell,out center)){reason="확정된 토대의 빈 칸을 선택하세요.";return false;}
            direction.y=0;if(!float.IsFinite(direction.sqrMagnitude)||direction.sqrMagnitude<.01f){reason="공격 방향을 지정하세요.";return false;}
            if(p==placement.GroundPlatform)foreach(var f in foundations){var d=WorldGridGeometry.ToLocal(center-WorldGridGeometry.Center(FoundationPlacement.Key(f.Point),8));if(Mathf.Abs(d.x)<5-.001f&&Mathf.Abs(d.z)<5-.001f){reason="임시 토대와 겹칩니다.";return false;}}
            var result=p.Placement.Validate(new PlacementRequest(-1,placement.Definition(kind),cell));reason=result.Message;if(!result.Success)return false;
            var quote=Quote(placement.Definition(kind));if(!quote.Affordable){reason=quote.Reason;return false;}return true;
        }
        public bool AddTower(Vector3 point,Vector3 direction,HordeTowerKind kind,out string reason)
        {
            if(!PreviewTower(point,direction,kind,out var p,out var cell,out var center,out reason))return false;
            direction.y=0;
            var result=p.Placement.Add(placement.Definition(kind),cell,out var request);reason=result.Message;if(!result.Success)return false;
            GameObject view=null;
            try {
                request.Direction=direction.normalized;view=placement.CreateTowerPreview(kind,center,request.Direction);DisableGhost(view);
                towers.Add(new PendingTower{Platform=p,Request=request,Kind=kind,BaseId=content?.Bases.SelectedBaseId,View=view,Visual=new ConstructionVisual(view,line,2,p.World.Grid.Rotation,Color.cyan)});
                reason="포탑 임시 배치 · 다른 칸을 추가하거나 확정하세요. · "+Quote().Summary;return true;
            } catch {p.Placement.Remove(request.Id);if(view!=null)UnityEngine.Object.Destroy(view);throw;}
        }
        public bool Move(PendingTower tower,Vector3 point,out string reason)
        {
            if(!towers.Contains(tower)||!placement.FindTowerCell(point,out var p,out var cell,out var center)) {reason="토대의 빈 칸을 선택하세요.";return false;}
            PlacementResult result;
            if(p==tower.Platform) result=p.Placement.Move(tower.Request.Id,cell);
            else {
                result=p.Placement.Add(tower.Request.Definition,cell,out var replacement);
                if(result.Success) {replacement.Direction=tower.Request.Direction;tower.Platform.Placement.Remove(tower.Request.Id);tower.Platform=p;tower.Request=replacement;}
            }
            reason=result.Success?"임시 위치 변경 · 확정 전까지 예약 유지":result.Message;
            if(result.Success)tower.View.transform.position=center;
            return result.Success;
        }
        public void Remove(PendingTower tower)
        { if(tower==null||!towers.Remove(tower))return;tower.Platform.Placement.Remove(tower.Request.Id);tower.Visual.Dispose();UnityEngine.Object.Destroy(tower.View); }
        public bool ToggleRecovery(Vector3 point,IReadOnlyList<HordeTower> installed,out string reason)
        {
            reason="회수 가능한 포탑을 선택하세요.";
            if(towers.Count+foundations.Count+contentPending.Count>0){reason="임시 배치를 먼저 확정하거나 취소하세요.";return false;}
            if(!placement.FindCell(point,out var p,out var cell,out _) || p.World.Grid.OccupantAt(cell) is not int id)return false;
            if(recoveringFoundations.ContainsKey(p))return ToggleFoundationRecovery(p,installed,out reason);
            if(!recovery.TryGetValue(p,out var session))recovery.Add(p,session=new RecoverySession(p.World));
            if(!session.Toggle(id))return false;
            if(session.Selected.Count==0)recovery.Remove(p);
            if(p.World.TryGet(id,out var contentBuilding))MarkContent(contentBuilding,session.Selected.Contains(id));
            foreach(var tower in installed)if(tower.building?.Id==id && p.World.TryGet(id,out var b)&&ReferenceEquals(tower.building,b)) {
                if(recovering.Remove(tower,out var visual))visual.Dispose();
                else recovering.Add(tower,new ConstructionVisual(tower.root,line,2,p.World.Grid.Rotation,new Color(1,.5f,.1f)));
                break;
            }
            reason="회수 예정 선택/해제 · 확정 전까지 실제 점유와 기능 유지";return true;
        }
        // Rectangle selection adds to the existing recovery set; reselecting never toggles it off.
        public bool SelectRecovery(BuildingWorld world,BuildingInstance building,out string reason)
        {
            reason="회수 예정 선택";
            if(towers.Count+foundations.Count+contentPending.Count>0){reason="임시 배치를 먼저 확정하거나 취소하세요.";return false;}
            if(building==null||building.Disposed||!building.Active||!building.Recoverable||!world.TryGet(building.Id,out var current)||!ReferenceEquals(current,building))
            {reason="회수 가능한 설치 건물이 아닙니다.";return false;}
            if(world==content.GroundWorld){if(groundRecovery?.Selected.Contains(building.Id)==true)return true;return ToggleGroundRecovery(building,out reason);}
            foreach(var platform in placement.Platforms)if(ReferenceEquals(platform.World,world)){
                if(recovery.TryGetValue(platform,out var session)&&session.Selected.Contains(building.Id))return true;
                return ToggleRecovery(building.Position,legacyGroundTowers,out reason);
            }
            reason="건물 월드가 없습니다.";return false;
        }
        public bool ToggleFoundationRecovery(FoundationPlacement.Platform platform,IReadOnlyList<HordeTower> installed,out string reason)
        {
            reason="토대와 위 포탑을 회수 예정으로 선택했습니다. 확정 전까지 유지됩니다.";
            if(towers.Count+foundations.Count+contentPending.Count>0){reason="임시 배치를 먼저 확정하거나 취소하세요.";return false;}
            if(recoveringFoundations.Remove(platform,out var old)) {
                old.Dispose();if(recovery.Remove(platform,out var previous))previous.Cancel();
                foreach(var b in platform.World.Buildings)MarkContent(b,false);
                foreach(var tower in installed)if(platform.World.TryGet(tower.building.Id,out var b)&&ReferenceEquals(b,tower.building)&&recovering.Remove(tower,out var visual))visual.Dispose();
                reason="토대와 위 포탑의 회수 선택을 해제했습니다.";return true;
            }
            if(platform.View==null || !placement.Platforms.Contains(platform)){reason="토대가 없습니다.";return false;}
            var session=new RecoverySession(platform.World);
            foreach(var building in platform.World.Buildings)if(!session.Toggle(building.Id)){reason="회수할 수 없는 포탑이 있습니다.";return false;}
            recovery[platform]=session;
            foreach(var b in platform.World.Buildings)MarkContent(b,true);
            recoveringFoundations.Add(platform,new ConstructionVisual(platform.View,line,8,platform.World.Grid.Rotation,new Color(1,.5f,.1f)));
            foreach(var tower in installed)if(platform.World.TryGet(tower.building.Id,out var b)&&ReferenceEquals(b,tower.building)&&!recovering.ContainsKey(tower))
                recovering.Add(tower,new ConstructionVisual(tower.root,line,2,platform.World.Grid.Rotation,new Color(1,.5f,.1f)));
            return true;
        }
        public void ClearRecovery()
        {
            groundRecovery?.Cancel();groundRecovery=null;foreach(var visual in contentRecovery.Values)visual.Dispose();contentRecovery.Clear();
            foreach(var s in recovery.Values)s.Cancel();foreach(var v in recovering.Values)v.Dispose();
            foreach(var v in recoveringFoundations.Values)v.Dispose();
            recovering.Clear();recovery.Clear();recoveringFoundations.Clear();
        }
        public PlacementResult Confirm(Func<PlacementResult> onInstalled=null)
        {
            if(Count==0)return new PlacementResult("empty","임시 작업이 없습니다.");
            if(RecoveryCount>0) {
                foreach(var p in recoveringFoundations.Keys) {
                    if(p.View==null || !placement.Platforms.Contains(p))return new PlacementResult("recovery-invalid","회수할 토대가 없어졌습니다. 취소하세요.");
                    foreach(var building in p.World.Buildings)if(!recovery[p].Selected.Contains(building.Id))return new PlacementResult("recovery-changed","토대 위 구성이 변경됐습니다. 회수 선택을 다시 하세요.");
                }
                foreach(var session in recovery.Values){var check=session.Validate();if(!check.Success)return check;}
                if(content?.Railway!=null){
                    content.Railway.Refresh();
                    PlacementResult CheckRail(BuildingWorld world,IReadOnlyCollection<int> selected){
                        foreach(var id in selected)if(world.TryGet(id,out var building)&&building.Module<RailFacility>()!=null&&
                            !content.Railway.Network.CanRecover(building,out var error))return new PlacementResult("railway-in-use",error);
                        return PlacementResult.Ok;
                    }
                    if(groundRecovery?.Selected.Count>0){var check=CheckRail(content.GroundWorld,groundRecovery.Selected);if(!check.Success)return check;}
                    foreach(var pair in recovery){var check=CheckRail(pair.Key.World,pair.Value.Selected);if(!check.Success)return check;}
                }
                var platforms=recoveringFoundations.Keys.ToArray();
                if(groundRecovery?.Selected.Count>0&&!content.CanRecoverGround(groundRecovery.Selected.ToArray(),platforms))return new PlacementResult("nexus-in-use","이 기지가 필요한 토대 또는 지면 건물이 남아 있습니다. 함께 회수하거나 다른 기지를 먼저 설치하세요.");
                var recoverySessions=recovery.Values.Where(r=>r.Selected.Count>0).ToList();
                if(groundRecovery?.Selected.Count>0)recoverySessions.Add(groundRecovery);
                var result=recoverySessions.Count==0?PlacementResult.Ok:RecoverySession.ConfirmTogether(recoverySessions);
                if(!result.Success)return result;
                ClearRecovery();foreach(var p in platforms)placement.RemoveFoundation(p);return PlacementResult.Ok;
            }
            bool valid=true;string reason=null;
            foreach(var f in foundations) {bool ok=placement.CheckFoundation(f.Point,out _,out var message);f.Visual.SetColor(ok?Color.cyan:Color.red);if(!ok){valid=false;reason=message;}}
            foreach(var t in towers) {var r=t.Platform.Placement.Validate(t.Request);t.Visual.SetColor(r.Success?Color.cyan:Color.red);if(!r.Success){valid=false;reason=r.Message;}}
            if(!valid)return new PlacementResult("invalid",reason);
            var quote=Quote();if(!quote.Affordable)return new PlacementResult("resource",quote.Reason);
            PlacementResult installation=PlacementResult.Ok;
            bool paid=ConstructionPurchase.TryCommit(quote.Payments,()=>{installation=ConfirmPlacement(onInstalled);return installation.Success;},out reason);
            return paid?installation:installation.Success?new PlacementResult("resource",reason):installation;
        }
        PlacementResult ConfirmPlacement(Func<PlacementResult> onInstalled)
        {
            string reason=null;
            var installedFoundations=new List<FoundationPlacement.Platform>();
            var prepared=new List<GameObject>();var sessions=new List<PlacementSession>();
            try {
                // Terrain slabs have no gameplay modules. Remove them on any later failure.
                foreach(var f in foundations) {
                    if(!placement.AddFoundation(f.Point,out reason))throw new InvalidOperationException(reason);
                    foreach(var platform in placement.Platforms)if(platform.Key==FoundationPlacement.Key(f.Point)){installedFoundations.Add(platform);break;}
                }
                foreach(var t in towers) {
                    var view=placement.CreateTowerPreview(t.Kind,t.View.transform.position,t.Request.Direction);view.SetActive(false);prepared.Add(view);
                    placement.PrepareRequest(t.Platform,t.Request,t.Kind,view);
                    if(!sessions.Contains(t.Platform.Placement))sessions.Add(t.Platform.Placement);
                }
                foreach(var p in contentPending)if(!sessions.Contains(p.Session))sessions.Add(p.Session);
                var result=sessions.Count==0?PlacementResult.Ok:PlacementSession.ConfirmTogether(sessions,onInstalled);
                if(!result.Success)throw new InvalidOperationException(result.Message);
                prepared.Clear();Cancel();return PlacementResult.Ok;
            } catch(Exception e) {
                foreach(var platform in installedFoundations)placement.RemoveFoundation(platform);
                foreach(var view in prepared)if(view!=null)UnityEngine.Object.Destroy(view);
                return new PlacementResult("creation",e.Message);
            } finally {foreach(var t in towers)t.Platform.Factory.ForgetRequest(t.Request.Id);}
        }
        public void Cancel()
        {
            for(int i=contentPending.Count-1;i>=0;i--)Remove(contentPending[i]);
            foreach(var t in towers){t.Platform.Factory.ForgetRequest(t.Request.Id);t.Platform.Placement.Remove(t.Request.Id);t.Visual.Dispose();UnityEngine.Object.Destroy(t.View);}towers.Clear();
            foreach(var f in foundations){f.Visual.Dispose();UnityEngine.Object.Destroy(f.View);}foundations.Clear();ClearRecovery();
        }
        public void Dispose()=>Cancel();
    }
}
