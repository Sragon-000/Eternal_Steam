using System;
using System.Collections.Generic;
using System.Linq;

namespace EternalSteam.Railway
{
    public sealed partial class RailwayNetwork
    {
        // The final rail socket lies at this progress in RailwaySceneView's interpolated path.
        public static double StationEntryProgress(RailRoute route)
        {double n=route.CurrentLeg.cells.Count;return n*n/(n+1);}

        static string OccupiedStation(RailRoute r)
            =>r.train.segmentPaid?(r.train.stationEntryReserved?r.stops[r.NextStopIndex].stationId:null):
                (r.train.waitingForStation?null:r.stops[r.train.stop].stationId);
        bool StationOccupied(string station,RailRoute except)
            =>routes.Any(r=>r!=except&&OccupiedStation(r)==station);
        public string StationSlotOwner(string station)
            =>routes.FirstOrDefault(r=>OccupiedStation(r)==station)?.id;

        static void ValidateStationSlots(IReadOnlyList<RailRoute> source)
        {
            var occupied=new HashSet<string>();
            foreach(var r in source){var t=r.train;
                if(t.stationEntryReserved&&(!t.segmentPaid||t.waitingForStation||t.progress<StationEntryProgress(r)))
                    throw new ArgumentException("역 진입 예약 저장 오류");
                if(t.segmentPaid&&((t.waitingForStation&&Math.Abs(t.progress-StationEntryProgress(r))>1e-9)||
                    (!t.stationEntryReserved&&t.progress>StationEntryProgress(r)+1e-9)))
                    throw new ArgumentException("역 진입 대기 위치 저장 오류");
                var station=OccupiedStation(r);
                if(station!=null&&!occupied.Add(station))throw new ArgumentException("역 정차 슬롯 중복 저장 오류");
            }
        }

        // Old saves had no slot ownership. Preserve inventory, service and fuel; stage duplicate
        // parked trains at their own departure socket, and conflicting arrivals at the entry socket.
        static void MigrateStationSlots(IReadOnlyList<RailRoute> source)
        {
            var occupied=new HashSet<string>();
            foreach(var r in source){r.train.waitingForStation=false;r.train.stationEntryReserved=false;
                if(!r.train.segmentPaid)r.train.waitingForStation=!occupied.Add(r.stops[r.train.stop].stationId);}
            foreach(var r in source){var t=r.train;if(!t.segmentPaid||t.progress<StationEntryProgress(r))continue;
                if(occupied.Add(r.stops[r.NextStopIndex].stationId))t.stationEntryReserved=true;
                else {t.progress=StationEntryProgress(r);t.waitingForStation=true;}}
        }

        static bool Running(TrainState t)=>t.status is TrainStatus.Moving or TrainStatus.StopRequested or TrainStatus.Dwelling;

        // Advance all routes on the same event clock. Processing an entire route's dt first would
        // let its future arrivals block another train's earlier arrival, and depend on frame size.
        public void Tick(double dt)
        {
            if(!double.IsFinite(dt)||dt<=0)return;
            foreach(var r in routes)if(Running(r.train)&&!r.stops.All(s=>StationActive(s.stationId))){
                r.train.status=TrainStatus.RouteError;r.error="노선의 역이 비활성입니다.";}
            double remaining=dt;
            while(true){
                bool changed;
                do {
                    changed=false;
                    // Free departing slots before arbitration at the same simulation instant.
                    foreach(var r in routes){var t=r.train;
                        if(t.status!=TrainStatus.Dwelling||t.waitingForStation||t.dwell>1e-9)continue;
                        t.dwell=0;double fuel=FuelCost(r.CurrentLeg);
                        if(t.fuel<fuel){t.status=TrainStatus.FuelWait;changed=true;continue;}
                        t.fuel-=fuel;t.segmentPaid=true;t.progress=0;t.status=TrainStatus.Moving;changed=true;
                    }
                    // Stable route-list order breaks exact ties and is retained by save/restore.
                    foreach(var r in routes){var t=r.train;
                        if(!t.segmentPaid&&t.waitingForStation&&t.status!=TrainStatus.RouteError&&StationActive(r.stops[t.stop].stationId)&&!StationOccupied(r.stops[t.stop].stationId,r)){
                            t.waitingForStation=false;if(t.status==TrainStatus.Dwelling)Service(r);changed=true;
                        }
                        if(t.status is not (TrainStatus.Moving or TrainStatus.StopRequested))continue;
                        double gate=StationEntryProgress(r);
                        if(!t.stationEntryReserved&&t.progress>=gate-1e-9){
                            t.progress=gate;
                            if(StationOccupied(r.stops[r.NextStopIndex].stationId,r)){t.waitingForStation=true;continue;}
                            t.waitingForStation=false;t.stationEntryReserved=true;changed=true;
                        }
                        if(t.progress<r.CurrentLeg.cells.Count-1e-9)continue;
                        bool stop=t.status==TrainStatus.StopRequested;t.stop=r.NextStopIndex;NormalizeDirection(r);
                        t.progress=0;t.segmentPaid=false;t.stationEntryReserved=false;t.waitingForStation=false;t.serviced=false;
                        if(t.stop==0)ApplyPendingAtBoundary(r);Service(r);t.status=stop?TrainStatus.Stopped:TrainStatus.Dwelling;changed=true;
                    }
                }while(changed);
                if(remaining<=1e-9)return;
                double step=remaining;bool advances=false;
                foreach(var r in routes){var t=r.train;if(!Running(t)||t.waitingForStation)continue;
                    double until=t.status==TrainStatus.Dwelling?t.dwell:
                        (t.stationEntryReserved?r.CurrentLeg.cells.Count:StationEntryProgress(r))-t.progress;
                    if(until>1e-9){step=Math.Min(step,until);advances=true;}
                }
                if(!advances)return;
                foreach(var r in routes){var t=r.train;if(!Running(t)||t.waitingForStation)continue;
                    if(t.status==TrainStatus.Dwelling)t.dwell=Math.Max(0,t.dwell-step);else t.progress+=step;
                }
                remaining-=step;
            }
        }
    }
}
