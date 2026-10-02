using System;
using System.Collections.Generic;
using System.Linq;
using EternalSteam.Railway;
using NUnit.Framework;
using UnityEngine;

namespace EternalSteam.Tests
{
    public sealed class RailwayShuttleTests
    {
        readonly List<UnityEngine.Object> assets=new();
        readonly List<BuildingInstance> buildings=new();
        readonly List<BuildingInstance> stations=new();
        sealed class Inventory:IRailInventory
        {
            public readonly Dictionary<string,ResourceBank> banks=new();
            public readonly Dictionary<string,int> visits=new();
            public bool Available(string id)=>banks.ContainsKey(id);
            public double Load(string id,string resource,double n)=>banks[id].Withdraw(resource,n);
            public double Unload(string id,string resource,double n){visits[id]=visits.GetValueOrDefault(id)+1;return banks[id].Deposit(resource,n);}
        }
        Inventory inventory;RailwayNetwork network;
        BuildingInstance Facility(RailFacilityKind kind,Vector2Int cell,string owner)
        {
            var module=ScriptableObject.CreateInstance<RailFacilityDefinition>();module.Kind=kind;assets.Add(module);
            var def=ScriptableObject.CreateInstance<BuildingDefinition>();def.Id=kind.ToString();def.Footprint=kind==RailFacilityKind.Track?Vector2Int.one:new Vector2Int(2,2);def.Modules.Add(module);assets.Add(def);
            var b=new BuildingInstance(buildings.Count+1,def,cell,new Vector3(cell.x*2,0,cell.y*2),new BuildingServices(null));
            b.RestoreIdentity(b.PersistentId,owner);b.ConfirmDirection(Quaternion.Euler(0,45,0)*Vector3.forward);b.Activate();buildings.Add(b);return b;
        }
        void AddStation(int index)
        {
            string id=Guid.NewGuid().ToString("N");var bank=new ResourceBank();foreach(string resource in new[]{"iron","coal"})bank.AddCapacity(resource,1000);
            bank.Deposit("iron",index==0?300:0);bank.Deposit("coal",100);inventory.banks.Add(id,bank);
            stations.Add(Facility(RailFacilityKind.Station,new Vector2Int(index*6,0),id));
            if(index>0){int x=(index-1)*6;foreach(var c in new[]{new Vector2Int(x+2,1),new Vector2Int(x+3,1),new Vector2Int(x+4,1),new Vector2Int(x+5,1),new Vector2Int(x+5,0)})Facility(RailFacilityKind.Track,c,stations[0].OwnerBaseId);}
            network.Refresh(buildings);
        }
        List<RailStop> Stops()=>stations.Select((s,i)=>new RailStop{stationId=s.PersistentId,arrival=0,departure=0,load=i==0?30:0,unload=i==stations.Count-1?30:0}).ToList();
        RailRoute Route()
        {
            Assert.That(network.Commit(Guid.NewGuid().ToString("N"),Stops(),out var r,out var reason,RailRouteMode.Shuttle),Is.True,reason);
            Assert.That(network.Configure(r,"iron",0,30,0,out reason),Is.True,reason);return r;
        }
        void Start(RailRoute r,double fuel=50){Assert.That(network.Refuel(r,fuel,out var e),Is.True,e);Assert.That(network.Start(r,out e),Is.True,e);}
        [SetUp] public void Setup(){inventory=new Inventory();network=new RailwayNetwork(inventory,"coal",1);AddStation(0);AddStation(1);}
        [TearDown] public void Cleanup(){foreach(var b in buildings)b.Dispose();buildings.Clear();stations.Clear();foreach(var a in assets)UnityEngine.Object.DestroyImmediate(a);assets.Clear();}

        [Test] public void SingleConnectionAllowsSameSocketBothWaysWithoutReturnTrack()
        {
            var r=Route();Assert.That(r.legs.Count,Is.EqualTo(1));Assert.That(r.legs[0].cells.Count,Is.EqualTo(5));
            Assert.That(r.legs[0].startPort,Is.EqualTo(1));Assert.That(r.legs[0].endPort,Is.EqualTo(0));
            Assert.That(r.ExpectedSeconds,Is.EqualTo(30));Assert.That(r.train.status,Is.EqualTo(TrainStatus.Stopped));
        }
        [Test] public void ThreeRoundTripsConserveCargoAndChargeEachDepartureExactlyOnce()
        {
            var r=Route();Start(r);network.Tick(90);
            Assert.That(r.train.stop,Is.Zero);Assert.That(r.train.reverse,Is.False);Assert.That(r.train.cargo,Is.EqualTo(30));
            Assert.That(inventory.banks[stations[1].OwnerBaseId].Amount("iron"),Is.EqualTo(90));
            Assert.That(inventory.banks.Values.Sum(b=>b.Amount("iron"))+r.train.cargo,Is.EqualTo(300));
            Assert.That(r.train.fuel,Is.EqualTo(50-6*network.FuelCost(r.legs[0])).Within(1e-8));
        }
        [Test] public void IntermediateStationIsVisitedOnceEachWayWithoutDuplicatedTerminusService()
        {
            AddStation(2);var r=Route();Start(r);Assert.That(r.ExpectedSeconds,Is.EqualTo(60));network.Tick(60);
            Assert.That(inventory.visits[stations[0].OwnerBaseId],Is.EqualTo(2));Assert.That(inventory.visits[stations[1].OwnerBaseId],Is.EqualTo(2));Assert.That(inventory.visits[stations[2].OwnerBaseId],Is.EqualTo(1));
            Assert.That(r.train.stop,Is.Zero);Assert.That(r.legs.Count,Is.EqualTo(2));
        }
        [Test] public void ReturnJourneySaveRestoresDirectionProgressFuelAndServiceFlags()
        {
            var r=Route();Start(r);network.Tick(26.25);Assert.That(r.train.reverse,Is.True);Assert.That(r.train.progress,Is.EqualTo(1.25));
            string before=JsonUtility.ToJson(network.Capture());network.Restore(JsonUtility.FromJson<RailwayNetwork.Snapshot>(before));
            Assert.That(JsonUtility.ToJson(network.Capture()),Is.EqualTo(before));r=network.Routes.Single();double fuel=r.train.fuel;
            network.Stop(r);network.Tick(3.75);Assert.That(r.train.stop,Is.Zero);Assert.That(r.train.status,Is.EqualTo(TrainStatus.Stopped));Assert.That(r.train.fuel,Is.EqualTo(fuel));
        }
        [Test] public void FuelWaitAtTerminusCanRefuelAndResumeWithoutUnloadingAgain()
        {
            var r=Route();Start(r,network.FuelCost(r.legs[0]));network.Tick(25);Assert.That(r.train.status,Is.EqualTo(TrainStatus.FuelWait));Assert.That(r.train.reverse,Is.True);
            var stock=inventory.banks[stations[1].OwnerBaseId];double iron=stock.Amount("iron");Start(r,10);network.Tick(1);
            Assert.That(stock.Amount("iron"),Is.EqualTo(iron));Assert.That(inventory.visits[stations[1].OwnerBaseId],Is.EqualTo(1));Assert.That(r.train.status,Is.EqualTo(TrainStatus.Moving));
        }
        [Test] public void PendingExtensionAppliesOnlyAtFirstStationReturnAndPreservesSettings()
        {
            var r=Route();Start(r);network.Tick(11);AddStation(2);Assert.That(network.UpdateRoute(r,Stops(),r.revision,out var e),Is.True,e);
            var saved=network.Capture();network.Restore(saved);r=network.Routes.Single();network.Tick(19);
            Assert.That(r.revision,Is.EqualTo(1));Assert.That(r.stops.Count,Is.EqualTo(3));Assert.That(r.legs.Count,Is.EqualTo(2));Assert.That(r.train.stop,Is.Zero);Assert.That(r.train.reverse,Is.False);Assert.That(r.pending.active,Is.False);
            Assert.That(r.stops[1].unload,Is.EqualTo(30));Assert.That(r.stops[2].unload,Is.Zero);
        }
        [Test] public void OtherRouteCannotClaimTheSamePhysicalTrackInEitherDirection()
        {
            var r=Route();var reversed=Stops();reversed.Reverse();
            Assert.That(network.Commit(Guid.NewGuid().ToString("N"),reversed,out _,out _,RailRouteMode.Shuttle),Is.False);
            Assert.That(network.InspectDraft(reversed,null,RailRouteMode.Shuttle).Issue.Code,Is.EqualTo(RouteIssueCode.OccupiedTrack));Assert.That(network.Routes.Count,Is.EqualTo(1));
        }
        [Test] public void InvalidDirectionAndV2ShuttlePayloadAreRejectedBeforeRestore()
        {
            var r=Route();var saved=network.Capture();Assert.Throws<ArgumentException>(()=>RailwayNetwork.ValidateSnapshot(saved,false));
            saved.routes[0].train.reverse=true;Assert.Throws<ArgumentException>(()=>network.Restore(saved));Assert.That(network.Routes.Single(),Is.SameAs(r));
        }
        [Test] public void MissingReturnTrackIsNotRequiredButRemovedOutboundTrackStopsAndRecoversSafely()
        {
            var r=Route();Start(r);network.Tick(26);var removed=buildings.Single(b=>b.Cell==new Vector2Int(4,1));
            Assert.That(network.CanRecover(removed,out _),Is.False);removed.Dispose();network.Refresh(buildings);Assert.That(r.train.status,Is.EqualTo(TrainStatus.RouteError));
            double fuel=r.train.fuel,progress=r.train.progress;Facility(RailFacilityKind.Track,new Vector2Int(4,1),stations[0].OwnerBaseId);network.Refresh(buildings);
            Assert.That(network.Start(r,out var e),Is.True,e);Assert.That(r.train.progress,Is.EqualTo(progress));Assert.That(r.train.fuel,Is.EqualTo(fuel));Assert.That(r.train.reverse,Is.True);
        }
    }
}
