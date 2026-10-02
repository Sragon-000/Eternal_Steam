using System;
using System.Collections.Generic;
using System.Linq;
using EternalSteam.Railway;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    public sealed class TrainDefenseController:IDisposable
    {
        readonly OpenWorldContent content;readonly RailwayNetwork network;readonly RailwaySceneView view;
        readonly ITargetQuery targets;readonly IReadOnlyList<BuildingDefinition> definitions;
        readonly Dictionary<string,MobileDefenseRuntime> mounted=new();
        public IEnumerable<MobileDefenseRuntime> Mounted=>mounted.Values;
        public bool HasPendingExecution=>mounted.Values.Any(m=>m.Pending);
        public TrainDefenseController(OpenWorldContent content,RailwayNetwork network,RailwaySceneView view,ITargetQuery targets,IReadOnlyList<BuildingDefinition> definitions)
        {this.content=content;this.network=network;this.view=view;this.targets=targets;this.definitions=definitions;}
        public IEnumerable<BuildingDefinition> Choices(RailRoute r)=>definitions.Where(d=>MobileDefenseRuntime.Compatible(d,r.train.armament.footprint)&&content.MeetsBaseLevel(d,out _));
        bool AtStation(RailRoute r,out BuildingInstance station,out string error)
        {
            station=null;error=null;
            if(r==null||!network.Routes.Contains(r)||r.train.segmentPaid||r.train.status is not (TrainStatus.Stopped or TrainStatus.FuelWait or TrainStatus.RouteError)){error="역에서 기차를 정지한 뒤 변경하세요.";return false;}
            station=network.Station(r.stops[r.train.stop].stationId);
            if(station==null||!network.StationActive(station.PersistentId)){error="정차 역의 소속 기지가 비활성입니다.";return false;}
            if(mounted.TryGetValue(r.trainId,out var m)&&m.Pending){error="진행 중인 공격이 끝난 뒤 변경하세요.";return false;}
            r.train.armament??=new TrainArmament();return true;
        }
        public bool CanShape(RailRoute r,out string error)
        {
            if(!AtStation(r,out _,out error))return false;
            if(!string.IsNullOrEmpty(r.train.armament.definition)){error="무기를 먼저 해제하세요.";return false;}
            return true;
        }
        public bool CanInstall(RailRoute r,BuildingDefinition definition,out string error)
        {
            if(!AtStation(r,out var station,out error))return false;
            if(!string.IsNullOrEmpty(r.train.armament.definition)||!Choices(r).Contains(definition)){
                error="빈 방어칸과 정확히 같은 크기의 해금된 방어 건물이 필요합니다.";return false;
            }
            var bank=content.Inventories.Available(station.OwnerBaseId);
            if(bank==null||(!bank.InfiniteResources&&bank.Amount("iron")<TrainArmament.InstallIron)){
                error="정차 역 소속 기지의 철이 부족합니다.";return false;
            }
            return true;
        }
        public bool CanRemove(RailRoute r,out string error)
        {
            if(!AtStation(r,out _,out error))return false;
            if(string.IsNullOrEmpty(r.train.armament.definition)){error="장착된 무기가 없습니다.";return false;}
            return true;
        }
        public bool CanUpgrade(RailRoute r,out string error)
        {
            if(!AtStation(r,out var station,out error))return false;Sync();
            if(!mounted.TryGetValue(r.trainId,out var m)){error="장착된 무기가 없습니다.";return false;}
            var upgrade=m.Building.Module<IUpgradeControl>();
            if(upgrade.Level>=content.LevelCap){error="메인 기지 레벨까지 강화할 수 있습니다.";return false;}
            if(!upgrade.CanUpgrade(out error))return false;
            var bank=content.Inventories.Available(station.OwnerBaseId);
            if(bank==null||(!bank.InfiniteResources&&bank.Amount("iron")<TrainArmament.UpgradeIronPerLevel*upgrade.Level)){
                error="정차 역 소속 기지의 철이 부족합니다.";return false;
            }
            return true;
        }
        public bool CanCharge(RailRoute r,out string error)
        {
            if(!AtStation(r,out var station,out error))return false;
            var source=content.Bases.Bases.TryGetValue(station.OwnerBaseId,out var b)?b.Nexus.Module<PowerModule>():null;
            if(r.train.armament.battery>=TrainArmament.Capacity||source==null||source.Role!=PowerRole.Storage||!source.Owner.Active||source.Owner.Disposed||source.Stored<=0){
                error="축전지가 가득 찼거나 정차 역 소속 기지의 저장 전력이 없습니다.";return false;
            }
            return true;
        }
        public bool Shape(RailRoute r,out string error)
        {
            if(!AtStation(r,out _,out error))return false;var a=r.train.armament;if(!string.IsNullOrEmpty(a.definition)){error="무기를 먼저 해제하세요.";return false;}
            a.footprint=a.footprint==new Vector2Int(2,2)?new Vector2Int(1,3):a.footprint==new Vector2Int(1,3)?new Vector2Int(3,1):new Vector2Int(2,2);return true;
        }
        public bool Install(RailRoute r,BuildingDefinition definition,out string error)
        {
            if(!AtStation(r,out var station,out error))return false;var a=r.train.armament;
            if(!string.IsNullOrEmpty(a.definition)||!Choices(r).Contains(definition)){error="빈 방어칸과 정확히 같은 크기의 해금된 방어 건물이 필요합니다.";return false;}
            var bank=content.Inventories.Available(station.OwnerBaseId);
            return ConstructionPurchase.TryCommit(new[]{new ConstructionPurchase.Payment{Bank=bank,Resource="iron",Amount=TrainArmament.InstallIron}},()=>{
                var next=new TrainArmament{definition=definition.Id,footprint=a.footprint,battery=a.battery};
                var runtime=new MobileDefenseRuntime(next,definition,targets,()=>view.CombatPosition(r,content),content);
                runtime.Capture();r.train.armament=next;mounted.Add(r.trainId,runtime);return true;
            },out error);
        }
        public bool Remove(RailRoute r,out string error)
        {
            if(!AtStation(r,out _,out error))return false;var a=r.train.armament;
            if(string.IsNullOrEmpty(a.definition)){error="장착된 무기가 없습니다.";return false;}
            if(mounted.Remove(r.trainId,out var m))m.Dispose();a.definition="";a.modules.Clear();return true; // no refund; battery belongs to the train
        }
        public bool Upgrade(RailRoute r,out string error)
        {
            if(!AtStation(r,out var station,out error))return false;Sync();
            if(!mounted.TryGetValue(r.trainId,out var m)){error="장착된 무기가 없습니다.";return false;}
            var upgrade=m.Building.Module<IUpgradeControl>();if(upgrade.Level>=content.LevelCap){error="메인 기지 레벨까지 강화할 수 있습니다.";return false;}
            if(!upgrade.CanUpgrade(out error))return false;
            bool ok=ConstructionPurchase.TryCommit(new[]{new ConstructionPurchase.Payment{Bank=content.Inventories.Available(station.OwnerBaseId),Resource="iron",Amount=TrainArmament.UpgradeIronPerLevel*upgrade.Level}},()=>upgrade.TryUpgrade(out _),out error);if(ok)m.Capture();return ok;
        }
        public bool Charge(RailRoute r,out string error)
        {
            if(!AtStation(r,out var station,out error))return false;var a=r.train.armament;
            var source=content.Bases.Bases.TryGetValue(station.OwnerBaseId,out var b)?b.Nexus.Module<PowerModule>():null;
            double moved=source?.WithdrawStored(TrainArmament.Capacity-a.battery)??0;a.battery+=moved;
            if(moved<=0){error="축전지가 가득 찼거나 정차 역 소속 기지의 저장 전력이 없습니다.";return false;}return true;
        }
        public void Sync()
        {
            foreach(var r in network.Routes){r.train.armament??=new TrainArmament();var a=r.train.armament;
                if(mounted.TryGetValue(r.trainId,out var previous)&&!ReferenceEquals(previous.State,a)){previous.Dispose();mounted.Remove(r.trainId);}
                if(!string.IsNullOrEmpty(a.definition)&&!mounted.ContainsKey(r.trainId))mounted.Add(r.trainId,new MobileDefenseRuntime(a,definitions.Single(d=>d.Id==a.definition),targets,()=>view.CombatPosition(r,content),content));
            }
        }
        public void Tick(float seconds,bool combatEnabled)
        {
            Sync();foreach(var r in network.Routes)if(mounted.TryGetValue(r.trainId,out var m)){
                var pose=view.Pose(r,content);if(Vector3.Dot(m.Building.Direction,pose.direction)<.999f)m.Building.ConfirmDirection(pose.direction);
                view.Defense(r.trainId)?.Bind(m.Building);m.Tick(seconds,combatEnabled);
            }else view.Defense(r.trainId)?.Bind(null);
        }
        public void Capture(){Sync();foreach(var m in mounted.Values)m.Capture();}
        public void Validate(RailwayNetwork.Snapshot data,int mainLevel)
        {
            foreach(var r in data.routes){var a=r.train.armament;if(string.IsNullOrEmpty(a?.definition))continue;
                var d=definitions.SingleOrDefault(d=>d.Id==a.definition);if(d==null||(d.Placement?.RequiredNexusLevel??1)>mainLevel)throw new ArgumentException("기차 방어 해금/정의 오류");
                using(var probe=new MobileDefenseRuntime(a,d,targets,()=>Vector3.zero)){if(a.modules.Count!=probe.Building.Modules.Count||probe.Building.Module<IUpgradeControl>().Level>mainLevel)throw new ArgumentException("기차 방어 저장 상태 오류");}
            }
        }
        public void Dispose(){foreach(var m in mounted.Values)m.Dispose();mounted.Clear();}
    }
}
