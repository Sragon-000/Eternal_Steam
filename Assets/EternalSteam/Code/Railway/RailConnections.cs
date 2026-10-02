using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EternalSteam.Railway
{
    [Serializable] public sealed class RailConnection
    {
        public string id,fromStation,toStation;
        public int fromPort,toPort;
        public List<Vector2Int> cells=new();
        public RailLeg Leg(bool reverse=false)=>new RailLeg{startPort=reverse?toPort:fromPort,endPort=reverse?fromPort:toPort,cells=reverse?cells.AsEnumerable().Reverse().ToList():new List<Vector2Int>(cells)};
    }
    [Serializable] public sealed class RailTrackRecord {public string buildingId;public Vector2Int cell;}
    public sealed partial class RailwayNetwork
    {
        readonly List<RailConnection> connections=new();
        readonly Dictionary<Vector2Int,RailConnection> connectedCells=new();
        public int ConnectionRevision {get;private set;}
        public IReadOnlyList<RailConnection> Connections=>connections.Select(CloneConnection).ToArray();
        static RailConnection CloneConnection(RailConnection c)=>new RailConnection{id=c.id,fromStation=c.fromStation,toStation=c.toStation,fromPort=c.fromPort,toPort=c.toPort,cells=new List<Vector2Int>(c.cells)};
        public bool HasTrack(Vector2Int cell)=>tracks.ContainsKey(cell);
        public bool TrackAssigned(Vector2Int cell)=>assignments.ContainsKey(cell);
        public bool TrackConnected(Vector2Int cell)=>connectedCells.ContainsKey(cell);
        public bool SocketConnected(string station,int port)=>connections.Any(c=>c.fromStation==station&&c.fromPort==port||c.toStation==station&&c.toPort==port);
        public bool ValidateConnection(RailConnection c,bool requireTracks,out string error)
        {
            error=null;
            try{ValidateConnectionShape(c);}catch(ArgumentException e){error=e.Message;return false;}
            var a=Station(c.fromStation);var b=Station(c.toStation);
            if(!StationActive(c.fromStation)||!StationActive(c.toStation)){error="연결할 역과 소속 기지가 활성 상태여야 합니다.";return false;}
            if(Port(a,c.fromPort)!=c.cells[0]||Port(b,c.toPort)!=c.cells[c.cells.Count-1]){error="연결구 위치가 변경됐습니다. 경로를 다시 확인하세요.";return false;}
            if(SocketConnected(c.fromStation,c.fromPort)||SocketConnected(c.toStation,c.toPort)){error="이미 연결된 연결구입니다. 다른 연결구를 선택하세요.";return false;}
            foreach(var cell in c.cells){
                if(connectedCells.ContainsKey(cell)||assignments.ContainsKey(cell)){error=$"[{cell.x}, {cell.y}] 다른 연결 또는 노선의 선로입니다.";return false;}
                if(requireTracks&&!tracks.ContainsKey(cell)){error=$"[{cell.x}, {cell.y}] 선로가 없습니다.";return false;}
            }
            return true;
        }
        public bool Connect(RailConnection c,out string error)
        {
            var old=connections.FirstOrDefault(v=>v.id==c?.id);
            if(old!=null){error=SameConnection(old,c)?null:"연결 요청 ID가 다른 경로에 사용됐습니다.";return error==null;}
            if(!ValidateConnection(c,true,out error))return false;
            AddConnection(CloneConnection(c));return true;
        }
        static bool SameConnection(RailConnection a,RailConnection b)=>b!=null&&a.fromStation==b.fromStation&&a.toStation==b.toStation&&a.fromPort==b.fromPort&&a.toPort==b.toPort&&b.cells!=null&&a.cells.SequenceEqual(b.cells);
        void AddConnection(RailConnection c){connections.Add(c);foreach(var cell in c.cells)connectedCells.Add(cell,c);ConnectionRevision++;}
        void RememberConnections(RailRoute route)
        {
            if(!route.IsShuttle)return;
            for(int i=0;i<route.legs.Count;i++){
                var l=route.legs[i];if(connectedCells.ContainsKey(l.cells[0]))continue;
                AddConnection(new RailConnection{id=Guid.NewGuid().ToString("N"),fromStation=route.stops[i].stationId,toStation=route.stops[i+1].stationId,fromPort=l.startPort,toPort=l.endPort,cells=new List<Vector2Int>(l.cells)});
            }
        }
        IEnumerable<RailLeg> ExplicitLegs(string from,string to)=>connections.Where(c=>c.fromStation==from&&c.toStation==to||c.toStation==from&&c.fromStation==to).Select(c=>c.Leg(c.toStation==from));
        public IReadOnlyList<Vector2Int> TrackDirections(Vector2Int cell)
        {
            if(!connectedCells.TryGetValue(cell,out var c))return Array.Empty<Vector2Int>();
            int i=c.cells.IndexOf(cell);var result=new List<Vector2Int>(2);
            if(i>0)result.Add(c.cells[i-1]-cell);else AddSocket(c.fromStation);
            if(i<c.cells.Count-1)result.Add(c.cells[i+1]-cell);else AddSocket(c.toStation);
            return result;
            void AddSocket(string id){var s=Station(id);if(s==null)return;var d=s.Cell+Vector2Int.one-cell;result.Add(Math.Abs(d.x)>Math.Abs(d.y)?new Vector2Int(Math.Sign(d.x),0):new Vector2Int(0,Math.Sign(d.y)));}
        }
        static void ValidateConnectionShape(RailConnection c)
        {
            if(c==null||!Guid.TryParseExact(c.id,"N",out _)||!Guid.TryParseExact(c.fromStation,"N",out _)||!Guid.TryParseExact(c.toStation,"N",out _)||c.fromStation==c.toStation||c.fromPort<0||c.fromPort>3||c.toPort<0||c.toPort>3||c.cells==null||c.cells.Count==0||c.cells.Count>10000)throw new ArgumentException("철도 연결 참조 오류");
            var seen=new HashSet<Vector2Int>();for(int i=0;i<c.cells.Count;i++)if(!seen.Add(c.cells[i])||i>0&&Math.Abs((long)c.cells[i].x-c.cells[i-1].x)+Math.Abs((long)c.cells[i].y-c.cells[i-1].y)!=1)throw new ArgumentException("철도 연결의 반복 또는 단절");
        }
        static void ValidateConnections(Snapshot data)
        {
            if(data.connectionVersion<0||data.connectionVersion>1)throw new ArgumentException("미지원 철도 연결 버전");
            if(data.connectionVersion==0)return;
            if(data.connections==null||data.trackRecords==null||data.connections.Count>10000||data.trackRecords.Count>10000)throw new ArgumentException("철도 연결 저장 목록 오류");
            var ids=new HashSet<string>();var cells=new HashSet<Vector2Int>();var sockets=new HashSet<(string,int)>();var stations=data.labels.Select(l=>l.stationId).ToHashSet();
            foreach(var c in data.connections){ValidateConnectionShape(c);if(!ids.Add(c.id)||!stations.Contains(c.fromStation)||!stations.Contains(c.toStation)||!sockets.Add((c.fromStation,c.fromPort))||!sockets.Add((c.toStation,c.toPort))||c.cells.Any(cell=>!cells.Add(cell)))throw new ArgumentException("철도 연결 중복/교차/역 참조 오류");}
            ids.Clear();cells.Clear();foreach(var t in data.trackRecords)if(t==null||!Guid.TryParseExact(t.buildingId,"N",out _)||!ids.Add(t.buildingId)||!cells.Add(t.cell))throw new ArgumentException("선로 건물 ID/셀 저장 오류");
            foreach(var r in data.routes.Where(r=>r.IsShuttle))for(int i=0;i<r.legs.Count;i++){
                var expected=r.legs[i];var match=data.connections.Where(c=>c.fromStation==r.stops[i].stationId&&c.toStation==r.stops[i+1].stationId||c.toStation==r.stops[i].stationId&&c.fromStation==r.stops[i+1].stationId).Select(c=>c.Leg(c.toStation==r.stops[i].stationId));
                if(!match.Any(l=>l.startPort==expected.startPort&&l.endPort==expected.endPort&&l.cells.SequenceEqual(expected.cells)))throw new ArgumentException("노선과 물리 연결 저장 불일치");
            }
        }
        public static void ValidateTrackRecords(Snapshot data,IReadOnlyDictionary<string,Vector2Int> actual)
        {
            if(data.connectionVersion==0)return;
            if(data.trackRecords.Count!=actual.Count||data.trackRecords.Any(t=>!actual.TryGetValue(t.buildingId,out var cell)||cell!=t.cell))throw new ArgumentException("선로 건물과 연결 그래프 ID/셀 불일치");
        }
        void RestoreConnections(Snapshot copy)
        {
            connections.Clear();connectedCells.Clear();ConnectionRevision++;
            if(copy.connectionVersion==1)foreach(var c in copy.connections)AddConnection(c);
            else foreach(var r in copy.routes)RememberConnections(r);
        }
    }
}
