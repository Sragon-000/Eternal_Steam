using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EternalSteam.Railway
{
    public sealed partial class RailwayNetwork
    {
        static void NormalizeDirection(RailRoute route)
        {
            if(!route.IsShuttle){route.train.reverse=false;return;}
            if(route.train.stop==0)route.train.reverse=false;
            else if(route.train.stop==route.stops.Count-1)route.train.reverse=true;
        }

        // Physical connections are stored once. Return journeys read their cells backwards.
        // This first pass supports existing unbranched tracks, not new construction or shared rails.
        RouteValidationResult InspectShuttle(IReadOnlyList<RailStop> stops,string replacing)
        {
            var segments=new List<RouteSegmentResult>();var issues=new List<RouteIssue>();
            if(stops==null||stops.Count<2){issues.Add(new RouteIssue(RouteIssueCode.TooFewStations,"서로 다른 역이 2개 이상 필요합니다.","활성 기차역을 두 곳 이상 추가하세요."));return new RouteValidationResult(segments,issues);}
            var blocked=new bool[stops.Count];var seen=new HashSet<string>();
            for(int i=0;i<stops.Count;i++){
                var s=stops[i];var station=Station(s?.stationId);
                void Error(RouteIssueCode code,string message,string recovery){blocked[i]=true;issues.Add(new RouteIssue(code,message,recovery,i,stationId:s?.stationId,cell:station?.Cell));}
                if(s==null||!StationActive(s.stationId)){Error(RouteIssueCode.InactiveStation,$"{i+1}번 역이 없거나 비활성입니다.","역과 소속 기지를 복구하세요.");continue;}
                if(!seen.Add(s.stationId))Error(RouteIssueCode.DuplicateStation,"같은 역이 중복 등록됐습니다.","귀환 방문은 자동 생성되므로 역은 한 번만 등록하세요.");
                if(!double.IsFinite(s.load)||!double.IsFinite(s.unload)||s.load<0||s.unload<0||s.load>CargoCapacity||s.unload>CargoCapacity)
                    Error(RouteIssueCode.InvalidQuantity,"적재·하역 수량은 0~1,000이어야 합니다.","유한한 수량을 지정하세요.");
            }
            var used=new HashSet<Vector2Int>();
            for(int i=0;i<stops.Count-1;i++){
                if(blocked[i]||blocked[i+1]){segments.Add(new RouteSegmentResult(i,RouteSegmentStatus.Blocked,new RailLeg()));continue;}
                var a=Station(stops[i].stationId);var b=Station(stops[i+1].stationId);
                var candidates=new List<RailLeg>();RouteIssue failure=null;
                RouteIssue Problem(RouteIssueCode code,string message,string recovery,Vector2Int cell,int port,string owner=null)
                    =>new RouteIssue(code,$"구간 {i+1}: {message}",recovery,i,i,a.PersistentId,b.PersistentId,port,cell,owner);
                for(int startPort=0;startPort<4;startPort++){
                    var cell=Port(a,startPort);if(!tracks.ContainsKey(cell))continue;
                    Vector2Int? previous=null;var visited=new HashSet<Vector2Int>();var leg=new RailLeg{startPort=startPort};
                    while(true){
                        if(!visited.Add(cell)){failure??=Problem(RouteIssueCode.RepeatedTrack,"반복 선로입니다.","구간 자체의 순환 연결을 제거하세요.",cell,startPort);break;}
                        if(assignments.TryGetValue(cell,out var owner)&&owner!=replacing){failure??=Problem(RouteIssueCode.OccupiedTrack,"다른 노선이 사용하는 선로입니다.","다른 노선과 독립된 선로를 사용하세요.",cell,startPort,owner);break;}
                        if(used.Contains(cell)){failure??=Problem(RouteIssueCode.RepeatedTrack,"물리 구간이 다른 구간과 겹칩니다.","역 사이 물리 구간을 분리하세요. 정상 왕복은 자동 재사용됩니다.",cell,startPort);break;}
                        leg.cells.Add(cell);
                        var next=Neighbors.Select(d=>cell+d).Where(c=>tracks.ContainsKey(c)&&c!=previous).ToArray();
                        int endPort=Enumerable.Range(0,4).Where(p=>Port(b,p)==cell).DefaultIfEmpty(-1).First();
                        if(endPort>=0){
                            if(next.Length!=0)failure??=Problem(RouteIssueCode.ArrivalOverrun,"도착 연결구에 추가 선로가 붙어 있습니다.","불필요한 연결을 제거하세요.",cell,startPort);
                            else {leg.endPort=endPort;candidates.Add(leg);}break;
                        }
                        if(next.Length!=1){failure??=Problem(next.Length==0?RouteIssueCode.MissingTrack:RouteIssueCode.Branch,next.Length==0?"목적 역까지 선로가 이어지지 않습니다.":"선로 분기·교차입니다.","표시된 셀과 목적 역 사이에 독립된 한 경로를 연결하세요.",cell,startPort);break;}
                        previous=cell;cell=next[0];
                    }
                }
                // Deterministic connector choice; an established route is revalidated against its saved path.
                var path=candidates.OrderBy(l=>l.cells.Count).ThenBy(l=>l.startPort).ThenBy(l=>l.endPort).FirstOrDefault();
                RouteIssue issue=null;
                if(path==null){path=new RailLeg();issue=failure??Problem(RouteIssueCode.MissingTrack,"역 사이 연결 선로가 없습니다.","어느 방향이든 두 역 사이 선로 한 개를 연결하세요.",a.Cell,-1);}
                else if(FuelCost(path)>stops.Count*FuelPerStation)issue=Problem(RouteIssueCode.InsufficientTankRange,"최대 연료로 이동할 수 없는 구간입니다.","경유역을 추가하거나 선로를 짧게 만드세요.",path.cells[0],path.startPort);
                if(issue==null)used.UnionWith(path.cells);else issues.Add(issue);
                segments.Add(new RouteSegmentResult(i,issue==null?RouteSegmentStatus.Valid:RouteSegmentStatus.Error,path,issue));
            }
            return new RouteValidationResult(segments,issues);
        }
    }
}
