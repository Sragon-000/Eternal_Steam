using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace EternalSteam.Railway
{
    public interface IRailInventory
    {
        bool Available(string baseId);
        double Load(string baseId,string resource,double maximum);
        double Unload(string baseId,string resource,double maximum);
    }
    public enum RailRouteMode { LegacyCycle=0, Shuttle=1 }
    public enum TrainStatus {Stopped,Moving,Dwelling,StopRequested,FuelWait,RouteError}
    [Serializable] public sealed class RailStop
    {
        public string stationId; public int arrival,departure=1; public double load,unload;
        public RailStop Copy()=>new RailStop{stationId=stationId,arrival=arrival,departure=departure,load=load,unload=unload};
    }
    [Serializable] public sealed class RailLeg {public List<Vector2Int> cells=new();public int startPort,endPort;}
    [Serializable] public sealed class TrainState
    {
        public TrainStatus status;public int stop;public double progress,dwell,cargo,fuel;
        public bool serviced,segmentPaid,reverse;public bool waitingForStation,stationEntryReserved;public string resource="";
        // JsonUtility serializes an unset string as empty; legacy null means the same thing.
        public bool HasCargoResource=>!string.IsNullOrEmpty(resource);
        public TrainArmament armament=new();
    }
    [Serializable] public sealed class RailRoute
    {
        public string id,name,trainId;public List<RailStop> stops=new();public List<RailLeg> legs=new();
        public TrainState train=new();public string error;
        public RailRouteMode mode;
        public int revision;public PendingRouteRevision pending=new();
        public bool IsShuttle=>mode==RailRouteMode.Shuttle;
        public int CurrentLegIndex=>IsShuttle?(train.reverse?train.stop-1:train.stop):train.stop;
        public int NextStopIndex=>IsShuttle?train.stop+(train.reverse?-1:1):(train.stop+1)%stops.Count;
        public RailLeg CurrentLeg=>legs[CurrentLegIndex];
        public Vector2Int TravelCell(int index)=>CurrentLeg.cells[IsShuttle&&train.reverse?CurrentLeg.cells.Count-1-index:index];
        public double ExpectedSeconds=>legs.Sum(l=>l.cells.Count)*(IsShuttle?2:1)+(IsShuttle?2*(stops.Count-1):stops.Count)*RailwayNetwork.DwellSeconds;
    }
    public enum RouteIssueCode { TooFewStations, DuplicateStation, InactiveStation, InvalidQuantity, InvalidPort, MissingTrack, RepeatedTrack, OccupiedTrack, Branch, ArrivalOverrun, InsufficientTankRange }
    /// <summary>Indices are zero based; -1 means that a field does not apply. Not persisted in saves.</summary>
    public sealed class RouteIssue
    {
        public RouteIssueCode Code {get;}
        public int StopIndex {get;} public int LegIndex {get;} public int Port {get;}
        public string StationId {get;} public string DestinationId {get;} public string ConflictingRouteId {get;}
        public Vector2Int? Cell {get;} public bool ReturnLeg {get;}
        public string Message {get;} public string Recovery {get;}
        public RouteIssue(RouteIssueCode code,string message,string recovery,int stopIndex=-1,int legIndex=-1,
            string stationId=null,string destinationId=null,int port=-1,Vector2Int? cell=null,string conflictingRouteId=null,bool returnLeg=false)
        {Code=code;Message=message;Recovery=recovery;StopIndex=stopIndex;LegIndex=legIndex;StationId=stationId;DestinationId=destinationId;Port=port;Cell=cell;ConflictingRouteId=conflictingRouteId;ReturnLeg=returnLeg;}
    }
    public enum RouteSegmentStatus { Valid, Error, Blocked }
    public sealed class RouteSegmentResult
    {
        public int Index {get;}
        public RouteSegmentStatus Status {get; internal set;}
        public RailLeg Path {get;}
        public RouteIssue Issue {get; internal set;}
        internal RouteSegmentResult(int index,RouteSegmentStatus status,RailLeg path,RouteIssue issue=null){Index=index;Status=status;Path=path;Issue=issue;}
    }
    public sealed class RouteValidationResult
    {
        public IReadOnlyList<RouteIssue> Issues {get;}
        public RouteIssue Issue=>Issues.Count==0?null:Issues[0];
        public bool Valid=>Issues.Count==0;
        public IReadOnlyList<RailLeg> Legs {get;}
        public IReadOnlyList<RouteSegmentResult> Segments {get;}
        internal RouteValidationResult(List<RouteSegmentResult> segments,List<RouteIssue> issues){Segments=segments.AsReadOnly();Issues=issues.AsReadOnly();Legs=segments.TakeWhile(s=>s.Status==RouteSegmentStatus.Valid).Select(s=>s.Path).ToList().AsReadOnly();}
    }
    /// <summary>Fixed routes; no automatic building, rerouting, shared track or frame-time pathfinding.</summary>
    public sealed partial class RailwayNetwork
    {
        readonly IRailInventory inventory;
        readonly Func<string,string> baseName;
        [Serializable] public sealed class StationLabel {public string stationId,baseId;public int number;}
        readonly List<StationLabel> labels=new();
        [Serializable] public sealed class BaseLabel {public string baseId,name;public int number;}
        readonly List<BaseLabel> baseLabels=new();
        public string BaseDisplayName(string id){var label=baseLabels.Find(b=>b.baseId==id);return label==null?(baseName?.Invoke(id)??"기지"):label.name+" "+label.number;}
        void RegisterBaseLabel(string id){if(baseLabels.Any(b=>b.baseId==id))return;int next=checked(baseLabels.Select(b=>b.number).DefaultIfEmpty(0).Max()+1);var name=baseName?.Invoke(id);baseLabels.Add(new BaseLabel{baseId=id,name=string.IsNullOrWhiteSpace(name)?"기지":name,number=next});}
        public string StationName(string id){var record=labels.Find(l=>l.stationId==id);return record==null?"알 수 없는 역":BaseDisplayName(record.baseId)+" 기차역 "+record.number;}
        readonly Dictionary<string,BuildingInstance> stations=new(StringComparer.Ordinal);
        readonly Dictionary<Vector2Int,BuildingInstance> tracks=new();
        readonly Dictionary<Vector2Int,string> assignments=new();
        readonly List<RailRoute> routes=new();
        public IReadOnlyList<RailRoute> Routes=>routes;
        public IEnumerable<BuildingInstance> Stations=>stations.Values;
        public double CoalPerUnit {get;}
        public string CoalId {get;}
        public Vector2Int? InvalidCell {get;private set;}
        public const double CargoCapacity=1000,FuelPerStation=50,RefuelBatch=50,DwellSeconds=10;
        public RailwayNetwork(IRailInventory inventory,string coalId,double coalPerUnit,Func<string,string> baseName=null)
        {this.baseName=baseName;if(inventory==null||string.IsNullOrWhiteSpace(coalId)||!double.IsFinite(coalPerUnit)||coalPerUnit<=0)throw new ArgumentException("철도 연료 설정 오류");this.inventory=inventory;CoalId=coalId;CoalPerUnit=coalPerUnit;}
        public static Vector2Int Port(BuildingInstance station,int port)
            =>PortCell(station.Cell,station.Direction,port);
        public static Vector2Int PortCell(Vector2Int cell,Vector3 direction,int port)
        {
            if(port<0||port>3)throw new ArgumentOutOfRangeException(nameof(port));
            // Four authored perimeter sockets. Rotation is expressed in grid space.
            double angle=Math.Atan2(direction.x,direction.z)*180/Math.PI-45;
            int turns=(((int)Math.Round(angle/90,MidpointRounding.AwayFromZero)%4)+4)%4;
            var p=port switch {0=>new Vector2Int(-1,0),1=>new Vector2Int(2,1),2=>new Vector2Int(1,-1),_=>new Vector2Int(0,2)};
            for(int i=0;i<turns;i++)p=new Vector2Int(p.y,1-p.x);
            return cell+p;
        }
        public BuildingInstance Station(string id)=>id!=null&&stations.TryGetValue(id,out var b)?b:null;
        public bool StationActive(string id)=>Station(id) is BuildingInstance b&&b.Operational&&inventory.Available(b.OwnerBaseId);
        public void Refresh(IEnumerable<BuildingInstance> buildings)
        {
            stations.Clear();tracks.Clear();foreach(var b in buildings){if(b.Disposed||!b.Active)continue;var f=b.Module<RailFacility>();if(f==null)continue;
                if(f.Kind==RailFacilityKind.Station){RegisterBaseLabel(b.OwnerBaseId);stations.Add(b.PersistentId,b);if(!labels.Any(l=>l.stationId==b.PersistentId))labels.Add(new StationLabel{stationId=b.PersistentId,baseId=b.OwnerBaseId,number=labels.Where(l=>l.baseId==b.OwnerBaseId).Select(l=>l.number).DefaultIfEmpty(0).Max()+1});}else tracks.Add(b.Cell,b);}
            foreach(var r in routes)if(!ValidateExisting(r,out var error)){r.error=error;r.train.status=TrainStatus.RouteError;}
        }
        static readonly Vector2Int[] Neighbors={Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
        // Compatibility entry point for commit, restore and older callers. The editor keeps its own result.
        public bool ValidateDraft(IReadOnlyList<RailStop> stops,string replacing,out List<RailLeg> legs,out string error,RailRouteMode mode=RailRouteMode.LegacyCycle)
        {
            var result=InspectDraft(stops,replacing,mode);legs=result.Legs.ToList();error=result.Issue?.Message;
            InvalidCell=result.Issue?.Cell;return result.Valid;
        }
        public RouteValidationResult InspectDraft(IReadOnlyList<RailStop> stops,string replacing,RailRouteMode mode=RailRouteMode.LegacyCycle)
        {
            if(mode==RailRouteMode.Shuttle)return InspectShuttle(stops,replacing);
            if(mode!=RailRouteMode.LegacyCycle)throw new ArgumentOutOfRangeException(nameof(mode));
            var segments=new List<RouteSegmentResult>();var issues=new List<RouteIssue>();
            RouteValidationResult Result()=>new RouteValidationResult(segments,issues);
            if(stops==null||stops.Count<2){issues.Add(new RouteIssue(RouteIssueCode.TooFewStations,"서로 다른 역이 2개 이상 필요합니다.","활성 기차역을 두 곳 이상 추가하세요."));return Result();}
            var seen=new HashSet<string>();var used=new Dictionary<Vector2Int,int>();var blocked=new bool[stops.Count];
            for(int i=0;i<stops.Count;i++){
                var stop=stops[i];var station=Station(stop?.stationId);
                void StopError(RouteIssueCode code,string message,string recovery,int port=-1)
                    {blocked[i]=true;issues.Add(new RouteIssue(code,message,recovery,i,stationId:stop?.stationId,port:port,cell:station?.Cell));}
                if(stop==null||!StationActive(stop.stationId)){StopError(RouteIssueCode.InactiveStation,$"{i+1}번 역이 없거나 비활성입니다.","역과 소속 기지를 복구하거나 해당 역을 교체하세요.");continue;}
                if(!seen.Add(stop.stationId))StopError(RouteIssueCode.DuplicateStation,$"{i+1}번 역이 중복되었습니다.","같은 역의 중복 방문을 제거하세요.");
                if(!double.IsFinite(stop.load)||!double.IsFinite(stop.unload)||stop.load<0||stop.unload<0||stop.load>CargoCapacity||stop.unload>CargoCapacity)
                    StopError(RouteIssueCode.InvalidQuantity,"역 적재·하역 수량은 0~1,000이어야 합니다.","적재·하역 수량을 0~1,000의 유한한 값으로 변경하세요.");
                if(stop.arrival<0||stop.arrival>3||stop.departure<0||stop.departure>3||stop.arrival==stop.departure)
                    StopError(RouteIssueCode.InvalidPort,$"{i+1}번 역에 서로 다른 진입·진출 포트를 지정하세요.","진입과 진출을 서로 다른 포트 1~4로 지정하세요.",stop.departure);
            }
            for(int i=0;i<stops.Count;i++){
                var a=stops[i];var b=stops[(i+1)%stops.Count];
                if(blocked[i]||blocked[(i+1)%stops.Count]){segments.Add(new RouteSegmentResult(i,RouteSegmentStatus.Blocked,new RailLeg()));continue;}
                var start=Port(Station(a.stationId),a.departure);var end=Port(Station(b.stationId),b.arrival);
                var leg=new RailLeg();Vector2Int? previous=null;var cell=start;var visited=new HashSet<Vector2Int>();
                RouteIssue LegError(RouteIssueCode code,string message,string recovery,string owner=null)
                    =>new RouteIssue(code,$"구간 {i+1} [{cell.x}, {cell.y}]: {message}",recovery,i,i,a.stationId,b.stationId,a.departure,cell,owner,i==stops.Count-1);
                RouteIssue Trace(){while(true){
                    if(!tracks.ContainsKey(cell))return LegError(RouteIssueCode.MissingTrack,"선로가 없습니다.","출발·도착 포트 사이의 누락된 선로를 설치하세요.");
                    if(!visited.Add(cell))return LegError(RouteIssueCode.RepeatedTrack,"순환·중복 선로입니다.","다른 구간과 겹치지 않는 단일 경로로 연결하세요.");
                    if(used.TryGetValue(cell,out int other)&&other!=i){
                        var conflict=LegError(RouteIssueCode.RepeatedTrack,"다른 구간과 중복된 선로입니다.","구간별로 독립된 선로를 연결하세요.");
                        if(segments[other].Status==RouteSegmentStatus.Valid){var prior=stops[other];var peer=new RouteIssue(RouteIssueCode.RepeatedTrack,$"구간 {other+1}: 구간 {i+1}과 선로가 중복됩니다.",conflict.Recovery,other,other,prior.stationId,stops[(other+1)%stops.Count].stationId,prior.departure,cell,returnLeg:other==stops.Count-1);segments[other].Status=RouteSegmentStatus.Error;segments[other].Issue=peer;issues.Add(peer);}
                        return conflict;
                    }
                    used[cell]=i;
                    if(assignments.TryGetValue(cell,out var owner)&&owner!=replacing)return LegError(RouteIssueCode.OccupiedTrack,"다른 노선이 사용하는 선로입니다.","점유 노선을 수정하거나 독립된 선로를 연결하세요.",owner);
                    leg.cells.Add(cell);var next=Neighbors.Select(d=>cell+d).Where(c=>tracks.ContainsKey(c)&&(!previous.HasValue||c!=previous.Value)).ToArray();
                    if(cell==end){if(next.Length!=0)return LegError(RouteIssueCode.ArrivalOverrun,"도착 포트 이후 분기/연결이 있습니다.","도착 포트 밖으로 이어지는 불필요한 선로를 제거하세요.");break;}
                    if(next.Length==0)return LegError(RouteIssueCode.MissingTrack,"도착 포트까지 선로가 이어지지 않습니다.","표시된 끝점에서 목적 역의 진입 포트까지 선로를 연결하세요.");
                    if(next.Length>1)return LegError(RouteIssueCode.Branch,"선로 분기·교차입니다.","표시된 셀의 분기를 제거하여 진행 방향을 하나로 만드세요.");
                    previous=cell;cell=next[0];
                }
                return null;}
                var issue=Trace();
                if(issue==null&&FuelCost(leg)>stops.Count*FuelPerStation)
                    issue=new RouteIssue(RouteIssueCode.InsufficientTankRange,$"구간 {i+1}: 필요한 석탄 {FuelCost(leg):0.##}개가 연료칸 최대 {stops.Count*FuelPerStation}개를 넘습니다.",
                        "경유역을 추가해 구간을 나누거나 노선을 짧게 만드세요.",i,i,a.stationId,b.stationId,a.departure,start,returnLeg:i==stops.Count-1);
                if(issue!=null)issues.Add(issue);segments.Add(new RouteSegmentResult(i,issue==null?RouteSegmentStatus.Valid:RouteSegmentStatus.Error,leg,issue));
            }
            return Result();
        }
        public bool Commit(string requestId,IReadOnlyList<RailStop> draft,out RailRoute route,out string error,RailRouteMode mode=RailRouteMode.LegacyCycle)
        {
            route=routes.Find(r=>r.id==requestId);error=null;if(route!=null)return true;
            if(!Guid.TryParseExact(requestId,"N",out _)){error="노선 요청 ID 오류";return false;}
            if(!ValidateDraft(draft,null,out var legs,out error,mode))return false;
            route=new RailRoute{mode=mode,id=requestId,name="노선 "+(routes.Count+1),trainId=Guid.NewGuid().ToString("N"),stops=draft.Select(s=>s.Copy()).ToList(),legs=legs};
            route.train.waitingForStation=StationOccupied(route.stops[0].stationId,route);
            routes.Add(route);foreach(var cell in legs.SelectMany(l=>l.cells).Distinct())assignments.Add(cell,route.id);return true;
        }
        public bool Edit(RailRoute route,IReadOnlyList<RailStop> stops,out string error)
        {
            error=null;if(!Owns(route)||route.revision==int.MaxValue||route.train.segmentPaid||route.train.status is not (TrainStatus.Stopped or TrainStatus.RouteError)){error="정지한 기차의 노선만 수정할 수 있습니다.";return false;}
            string current=route.stops[route.train.stop].stationId;
            if(stops==null||!stops.Any(s=>s!=null&&s.stationId==current)){error="기차가 정차한 역은 제거할 수 없습니다.";return false;}
            if(!ValidateDraft(stops,route.id,out var legs,out error,route.mode))return false;
            if(route.train.fuel>stops.Count*FuelPerStation){error="수정 후 연료 용량보다 현재 석탄이 많습니다.";return false;}
            foreach(var cell in route.legs.SelectMany(l=>l.cells).Distinct())assignments.Remove(cell);
            route.stops=stops.Select(s=>s.Copy()).ToList();route.legs=legs;route.train.stop=route.stops.FindIndex(s=>s.stationId==current);NormalizeDirection(route);
            foreach(var cell in legs.SelectMany(l=>l.cells).Distinct())assignments.Add(cell,route.id);route.train.status=TrainStatus.Stopped;route.error=null;route.revision++;route.pending=new PendingRouteRevision();return true;
        }
        bool Owns(RailRoute r)=>r!=null&&routes.Contains(r);
        public bool Configure(RailRoute route,string resource,int stop,double load,double unload,out string error)
        {
            error=null;if(!Owns(route)||route.train.status!=TrainStatus.Stopped||stop<0||stop>=route.stops.Count){error="정지 상태에서 역 설정을 변경하세요.";return false;}
            if(string.IsNullOrWhiteSpace(resource)||!double.IsFinite(load)||!double.IsFinite(unload)||load<0||unload<0||load>CargoCapacity||unload>CargoCapacity){error="화물/수량 설정 오류";return false;}
            if(route.train.HasCargoResource&&route.train.resource!=resource){error="화물칸 자원은 지정 후 변경할 수 없습니다.";return false;}
            route.train.resource=resource;route.stops[stop].load=load;route.stops[stop].unload=unload;return true;
        }
        public bool Refuel(RailRoute route,double amount,out string error)
        {
            error=null;if(!Owns(route)||route.train.waitingForStation||route.train.status is not (TrainStatus.Stopped or TrainStatus.FuelWait)||!double.IsFinite(amount)||amount<=0){error="정차 후 보급할 수 있습니다.";return false;}
            var b=Station(route.stops[route.train.stop].stationId);if(b==null||!StationActive(b.PersistentId)){error="역의 소속 기지 재고가 비활성입니다.";return false;}
            double moved=inventory.Load(b.OwnerBaseId,CoalId,Math.Min(amount,route.stops.Count*FuelPerStation-route.train.fuel));route.train.fuel+=moved;
            if(moved<=0){error="석탄이 없거나 연료칸이 가득 찼습니다.";return false;}return true;
        }
        public double FuelCost(RailLeg leg)=>CoalPerUnit*(1+.1*(leg.cells.Count-1));
        public bool Start(RailRoute route,out string error)
        {
            error=null;if(!Owns(route)||route.train.status is not (TrainStatus.Stopped or TrainStatus.FuelWait or TrainStatus.RouteError)){error="시작할 수 없는 상태입니다.";return false;}
            if(!ValidateExisting(route,out error)){route.error=error;route.train.status=TrainStatus.RouteError;return false;}
            var t=route.train;if(!t.HasCargoResource){error="화물 자원을 먼저 지정하세요.";return false;}
            if(t.segmentPaid){t.status=TrainStatus.Moving;route.error=null;return true;}
            if(t.fuel<FuelCost(route.CurrentLeg)){t.status=TrainStatus.FuelWait;error="다음 구간의 석탄이 부족합니다.";return false;}
            if(!t.waitingForStation)Service(route);route.error=null;t.status=TrainStatus.Dwelling;return true;
        }
        public void Stop(RailRoute route)
        {if(!Owns(route))return;var t=route.train;if(t.status==TrainStatus.Moving)t.status=TrainStatus.StopRequested;else if(t.status is TrainStatus.Dwelling or TrainStatus.FuelWait)t.status=TrainStatus.Stopped;}
        void Service(RailRoute route)
        {
            var t=route.train;if(t.serviced)return;var s=route.stops[t.stop];var b=Station(s.stationId);
            t.cargo-=inventory.Unload(b.OwnerBaseId,t.resource,Math.Min(s.unload,t.cargo));
            t.cargo+=inventory.Load(b.OwnerBaseId,t.resource,Math.Min(s.load,CargoCapacity-t.cargo));t.serviced=true;t.dwell=DwellSeconds;
        }
        public bool ValidateExisting(RailRoute route,out string error)
        {
            if(!ValidateDraft(route.stops,route.id,out var legs,out error,route.mode))return false;
            if(legs.Count!=route.legs.Count||legs.Where((l,i)=>(!l.cells.SequenceEqual(route.legs[i].cells)||(route.IsShuttle&&(l.startPort!=route.legs[i].startPort||l.endPort!=route.legs[i].endPort)))).Any()){error="저장된 선로 경로가 변경되었습니다.";return false;}return true;
        }
        [Serializable] public sealed class Snapshot {public int stationSlotVersion;public List<RailRoute> routes=new();public List<StationLabel> labels=new();public List<BaseLabel> baseLabels=new();}
        public Snapshot Capture()=>JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(new Snapshot{stationSlotVersion=1,routes=routes,labels=labels,baseLabels=baseLabels}));
        public static void ValidateSnapshot(Snapshot data,bool allowShuttle=true)
        {
            if(data?.routes==null||data.routes.Count>10000||data.stationSlotVersion<0||data.stationSlotVersion>1)throw new ArgumentException("철도 저장 목록 오류");
            if(data.labels==null)throw new ArgumentException("역 이름 저장 오류");var stationIds=new HashSet<string>();var ordinals=new HashSet<string>();
            foreach(var label in data.labels)if(label==null||!Guid.TryParseExact(label.stationId,"N",out _)||!Guid.TryParseExact(label.baseId,"N",out _)||label.number<1||!stationIds.Add(label.stationId)||!ordinals.Add(label.baseId+":"+label.number))throw new ArgumentException("역 이름/순번 저장 오류");
            // Missing/empty registry is the legacy format and is upgraded deterministically on restore.
            if(data.baseLabels!=null&&data.baseLabels.Count>0){var baseIds=new HashSet<string>();var baseNumbers=new HashSet<int>();foreach(var label in data.baseLabels)if(label==null||!Guid.TryParseExact(label.baseId,"N",out _)||!baseIds.Add(label.baseId)||label.number<1||label.number==int.MaxValue||!baseNumbers.Add(label.number)||string.IsNullOrWhiteSpace(label.name)||label.name.Length>128)throw new ArgumentException("기지 표시명/순번 저장 오류");if(data.labels.Any(l=>!baseIds.Contains(l.baseId)))throw new ArgumentException("역의 기지 표시명 참조 오류");}
            var ids=new HashSet<string>();var trains=new HashSet<string>();var cells=new HashSet<Vector2Int>();
            foreach(var r in data.routes){
                if(r==null||!Guid.TryParseExact(r.id,"N",out _)||!ids.Add(r.id)||!Guid.TryParseExact(r.trainId,"N",out _)||!trains.Add(r.trainId)||r.stops==null||r.stops.Count<2||r.legs==null||r.legs.Count!=(r.IsShuttle?r.stops.Count-1:r.stops.Count)||r.train==null)throw new ArgumentException("노선/기차 저장 참조 오류");
                if(!Enum.IsDefined(typeof(RailRouteMode),r.mode)||(!allowShuttle&&r.IsShuttle))throw new ArgumentException("미지원 철도 노선 모드");
                if(r.revision<0)throw new ArgumentException("노선 버전 저장 오류");
                ValidatePendingSnapshot(r,stationIds);
                var seen=new HashSet<string>();foreach(var s in r.stops)if(s==null||!Guid.TryParseExact(s.stationId,"N",out _)||!seen.Add(s.stationId)||!stationIds.Contains(s.stationId)||s.arrival<0||s.arrival>3||s.departure<0||s.departure>3||(!r.IsShuttle&&s.arrival==s.departure)||!double.IsFinite(s.load)||!double.IsFinite(s.unload)||s.load<0||s.load>CargoCapacity||s.unload<0||s.unload>CargoCapacity)throw new ArgumentException("역 저장 설정 오류");
                foreach(var l in r.legs){if(l?.cells==null||l.cells.Count==0||(r.IsShuttle&&(l.startPort<0||l.startPort>3||l.endPort<0||l.endPort>3)))throw new ArgumentException("선로 저장 오류");for(int i=0;i<l.cells.Count;i++){if(!cells.Add(l.cells[i]))throw new ArgumentException("선로 독점 저장 오류");if(i>0&&Math.Abs(l.cells[i].x-l.cells[i-1].x)+Math.Abs(l.cells[i].y-l.cells[i-1].y)!=1)throw new ArgumentException("단절 선로 저장 오류");}}
                TrainArmament.Validate(r.train.armament);
                var t=r.train;
                if((!r.IsShuttle&&t.reverse)||(r.IsShuttle&&(t.stop==0&&t.reverse||t.stop==r.stops.Count-1&&!t.reverse)))throw new ArgumentException("기차 왕복 방향 저장 오류");
                if(!Enum.IsDefined(typeof(TrainStatus),t.status)||t.stop<0||t.stop>=r.stops.Count||!double.IsFinite(t.cargo)||t.cargo<0||t.cargo>CargoCapacity||!double.IsFinite(t.fuel)||t.fuel<0||t.fuel>r.stops.Count*FuelPerStation||!double.IsFinite(t.dwell)||t.dwell<0||t.dwell>DwellSeconds||!double.IsFinite(t.progress)||t.progress<0||t.progress>=r.legs[r.IsShuttle?(t.reverse?t.stop-1:t.stop):t.stop].cells.Count||(!t.HasCargoResource&&t.cargo>0))throw new ArgumentException("기차 저장 수량/위치 오류");
                if(t.status is TrainStatus.Moving or TrainStatus.StopRequested){if(!t.segmentPaid||!t.serviced||t.dwell!=0)throw new ArgumentException("기차 이동 저장 상태 오류");}
                else if(t.status!=TrainStatus.RouteError&&(t.segmentPaid||t.progress!=0))throw new ArgumentException("기차 정차 저장 상태 오류");
            }
            if(data.stationSlotVersion==1)ValidateStationSlots(data.routes);
        }
        public void Restore(Snapshot data)
        {
            ValidateSnapshot(data);var copy=JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(data));
            if(copy.stationSlotVersion==0)MigrateStationSlots(copy.routes);
            ValidateStationSlots(copy.routes);
            routes.Clear();assignments.Clear();labels.Clear();labels.AddRange(copy.labels);baseLabels.Clear();
            if(copy.baseLabels!=null&&copy.baseLabels.Count>0)baseLabels.AddRange(copy.baseLabels);
            else foreach(var id in labels.Select(l=>l.baseId).Distinct().OrderBy(id=>id,StringComparer.Ordinal))RegisterBaseLabel(id);
            routes.AddRange(copy.routes);foreach(var r in routes){foreach(var c in r.legs.SelectMany(l=>l.cells).Distinct())assignments.Add(c,r.id);if(!ValidateExisting(r,out var error)){r.error=error;r.train.status=TrainStatus.RouteError;}}
        }
        public bool CanRecover(BuildingInstance b,out string error)
        {
            error=null;foreach(var r in routes){bool uses=b.Module<RailFacility>()?.Kind==RailFacilityKind.Station?r.stops.Any(s=>s.stationId==b.PersistentId):r.legs.Any(l=>l.cells.Contains(b.Cell));
                if(uses&&(r.train.segmentPaid||r.train.status is not (TrainStatus.Stopped or TrainStatus.RouteError or TrainStatus.FuelWait))){error="사용 중인 노선의 기차를 먼저 역에서 정지하세요.";return false;}
                if(uses&&r.stops[r.train.stop].stationId==b.PersistentId){error="기차가 정차한 역은 회수할 수 없습니다.";return false;}}
            return true;
        }
    }
}
