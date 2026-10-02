using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace EternalSteam.Railway
{
    [Serializable] public sealed class PendingRouteRevision
    {
        public bool active,failed;
        public int baseRevision;
        public List<RailStop> stops=new();
        public List<RailLeg> legs=new();
        public string error;
    }
    public sealed partial class RailwayNetwork
    {
        public bool HasPending(RailRoute route)=>Owns(route)&&route.pending?.active==true;
        public bool UpdateRoute(RailRoute route,IReadOnlyList<RailStop> stops,int expectedRevision,out string error)
        {
            error=null;
            if(!Owns(route)||route.revision!=expectedRevision||route.revision==int.MaxValue){error="편집 중 노선이 변경됐습니다. 다시 열어 주세요.";return false;}
            if(!ValidateDraft(stops,route.id,out var legs,out error,route.mode))return false;
            // Settings belong to station identity; new visits start at zero. Cargo resource never changes here.
            var revised=PreserveSettings(route,stops);
            if(!route.train.segmentPaid&&route.train.status is TrainStatus.Stopped or TrainStatus.RouteError)
                return Edit(route,revised,out error);
            if(revised[0].stationId!=route.stops[0].stationId){error="예약 변경은 기존 첫 역을 유지해야 합니다.";return false;}
            if(route.train.fuel>revised.Count*50){error="새 연료 용량보다 석탄이 많습니다. 소비 후 다시 예약하세요.";return false;}
            route.pending=new PendingRouteRevision{active=true,baseRevision=route.revision,stops=revised,legs=legs};
            return true;
        }
        static List<RailStop> PreserveSettings(RailRoute route,IReadOnlyList<RailStop> stops)
        {
            return stops.Select(s=>{var copy=s.Copy();var old=route.stops.Find(v=>v.stationId==s.stationId);copy.load=old?.load??0;copy.unload=old?.unload??0;return copy;}).ToList();
        }
        public bool CancelPending(RailRoute route)
        {
            if(!HasPending(route))return false;
            route.pending=new PendingRouteRevision();return true;
        }
        void ApplyPendingAtBoundary(RailRoute route)
        {
            var pending=route.pending;if(pending?.active!=true||pending.failed)return;
            string error=null;List<RailLeg> legs=null;
            if(pending.baseRevision!=route.revision||pending.stops[0].stationId!=route.stops[0].stationId)error="예약의 기준 노선이 변경됐습니다.";
            else if(route.train.fuel>pending.stops.Count*50)error="변경 후 연료 용량보다 현재 석탄이 많습니다.";
            else if(ValidateDraft(pending.stops,route.id,out legs,out error,route.mode)&&!SamePath(legs,pending.legs))error="예약한 선로 경로가 변경됐습니다.";
            if(error!=null){pending.failed=true;pending.error=error;return;}
            // All checks precede the ownership swap. Until now, reserved tracks remain available to other routes.
            var revised=PreserveSettings(route,pending.stops);
            foreach(var cell in route.legs.SelectMany(l=>l.cells).Distinct())assignments.Remove(cell);
            foreach(var cell in legs.SelectMany(l=>l.cells).Distinct())assignments.Add(cell,route.id);
            route.stops=revised;route.legs=legs;route.train.stop=0;NormalizeDirection(route);route.revision++;
            route.pending=new PendingRouteRevision();
        }
        static bool SamePath(IReadOnlyList<RailLeg> a,IReadOnlyList<RailLeg> b)=>a.Count==b.Count&&!a.Where((l,i)=>(!l.cells.SequenceEqual(b[i].cells)||l.startPort!=b[i].startPort||l.endPort!=b[i].endPort)).Any();
        static void ValidatePendingSnapshot(RailRoute route,HashSet<string> labels)
        {
            var p=route.pending;if(p==null||!p.active)return;
            if(p.baseRevision!=route.revision||route.revision==int.MaxValue||p.stops==null||p.stops.Count<2||p.stops.Count>10000||p.legs==null||p.legs.Count!=(route.IsShuttle?p.stops.Count-1:p.stops.Count)||p.stops[0]==null||route.stops[0]==null||p.stops[0].stationId!=route.stops[0].stationId)
                throw new ArgumentException("예약 노선 기준/역 참조 오류");
            var ids=new HashSet<string>();var cells=new HashSet<Vector2Int>();
            foreach(var s in p.stops)if(s==null||!labels.Contains(s.stationId)||!ids.Add(s.stationId)||s.arrival<0||s.arrival>3||s.departure<0||s.departure>3||(!route.IsShuttle&&s.arrival==s.departure)||!double.IsFinite(s.load)||!double.IsFinite(s.unload)||s.load<0||s.load>CargoCapacity||s.unload<0||s.unload>CargoCapacity)throw new ArgumentException("예약 역 설정 오류");
            foreach(var l in p.legs){if(l?.cells==null||l.cells.Count==0||(route.IsShuttle&&(l.startPort<0||l.startPort>3||l.endPort<0||l.endPort>3)))throw new ArgumentException("예약 선로 오류");for(int i=0;i<l.cells.Count;i++){if(!cells.Add(l.cells[i])||(i>0&&Math.Abs(l.cells[i].x-l.cells[i-1].x)+Math.Abs(l.cells[i].y-l.cells[i-1].y)!=1))throw new ArgumentException("예약 선로 중복/단절 오류");}}
        }
    }
}
