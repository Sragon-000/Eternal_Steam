using System;
using System.Collections.Generic;
using System.Linq;
using EternalSteam.Railway;
using EternalSteam.OpenWorld;
using NUnit.Framework;
using UnityEngine;
namespace EternalSteam.Tests
{
    public sealed class RailwayEconomyTests
    {
        static ResourceBank Bank(double amount,double capacity=1000){var b=new ResourceBank();b.AddCapacity("iron",capacity);b.Deposit("iron",amount);return b;}
        [Test] public void InfiniteResourcesTogglePreservesStockAndRestoresCharging()
        {
            var b=Bank(5);var costs=new[]{new ResourceCost{ResourceId="iron",Amount=20}};
            Assert.That(b.CanPurchase(costs,out _),Is.False);b.InfiniteResources=true;
            Assert.That(b.TryPurchase(costs,()=>true,out _),Is.True);Assert.That(b.Withdraw("iron",100),Is.EqualTo(100));
            Assert.That(b.Capture().Single().amount,Is.EqualTo(5));b.InfiniteResources=false;
            Assert.That(b.CanPurchase(costs,out _),Is.False);Assert.That(b.Withdraw("iron",100),Is.EqualTo(5));
        }
        [Test] public void InfiniteResourcesStillValidatesCostsCommitAndCapacity()
        {
            var b=Bank(5,10);b.InfiniteResources=true;
            Assert.That(b.TryPurchase(new[]{new ResourceCost{ResourceId="iron",Amount=20}},()=>false,out _),Is.False);
            Assert.That(b.CanPurchase(new[]{new ResourceCost{ResourceId="iron",Amount=double.NaN}},out _),Is.False);
            Assert.That(b.Exchange("coal",99,"iron",4),Is.True);Assert.That(b.Amount("coal"),Is.Zero);
            Assert.That(b.Exchange("coal",99,"iron",4),Is.False);Assert.That(b.Amount("iron"),Is.EqualTo(9));
        }
        [Test] public void InfiniteResourcesAppliesToNewAndRestoredBaseLedgers()
        {
            var registry=new BaseInventoryRegistry(Bank(0),_=>true);string a=Guid.NewGuid().ToString("N"),b=Guid.NewGuid().ToString("N");
            registry.Ensure(a);registry.InfiniteResources=true;Assert.That(registry.Ensure(a).InfiniteResources,Is.True);Assert.That(registry.Ensure(b).InfiniteResources,Is.True);
            registry.Restore(registry.Capture());Assert.That(registry.Ensure(a).InfiniteResources,Is.True);
            registry.InfiniteResources=false;Assert.That(registry.Ensure(a).InfiniteResources,Is.False);Assert.That(registry.Ensure(b).InfiniteResources,Is.False);
        }
        [Test] public void InfiniteConstructionStillRequiresSuccessfulPlacement()
        {
            var b=Bank(0);b.InfiniteResources=true;var costs=new[]{new ConstructionPurchase.Payment{Bank=b,Resource="iron",Amount=100}};
            Assert.That(ConstructionPurchase.TryCommit(costs,()=>false,out _),Is.False);Assert.That(ConstructionPurchase.TryCommit(costs,()=>true,out _),Is.True);
            Assert.That(b.Amount("iron"),Is.Zero);b.InfiniteResources=false;Assert.That(ConstructionPurchase.TryCommit(costs,()=>true,out _),Is.False);
        }
        [Test] public void SeparateBaseLedgersNeverShareOrCopyBalances()
        {
            var template=Bank(25);var available=new HashSet<string>{"a","b"};var r=new BaseInventoryRegistry(template,available.Contains);
            Assert.That(r.Ensure("a").Amount("iron"),Is.Zero);r.Ensure("a").Deposit("iron",300);
            Assert.That(r.Ensure("b").Amount("iron"),Is.Zero);available.Remove("a");Assert.That(r.Available("a"),Is.Null);Assert.That(r.Ensure("a").Amount("iron"),Is.EqualTo(300));
        }
        [Test] public void MultiBasePurchaseFailureChargesNeitherBase()
        {
            var a=Bank(100);var b=Bank(2);bool called=false;
            var payments=new[]{new ConstructionPurchase.Payment{Bank=a,Resource="iron",Amount=10},new ConstructionPurchase.Payment{Bank=b,Resource="iron",Amount=3}};
            Assert.That(ConstructionPurchase.TryCommit(payments,()=>{called=true;return true;},out _),Is.False);Assert.That(called,Is.False);Assert.That(a.Amount("iron"),Is.EqualTo(100));Assert.That(b.Amount("iron"),Is.EqualTo(2));
        }
        [Test] public void FailedInstallAndAggregatedCostsDoNotPartiallyCharge()
        {
            var a=Bank(10);var payments=new[]{new ConstructionPurchase.Payment{Bank=a,Resource="iron",Amount=6},new ConstructionPurchase.Payment{Bank=a,Resource="iron",Amount=6}};
            Assert.That(ConstructionPurchase.TryCommit(payments,()=>true,out _),Is.False);payments[1].Amount=4;
            Assert.That(ConstructionPurchase.TryCommit(payments,()=>false,out _),Is.False);Assert.That(a.Amount("iron"),Is.EqualTo(10));
            Assert.That(ConstructionPurchase.TryCommit(payments,()=>true,out _),Is.True);Assert.That(a.Amount("iron"),Is.Zero);
        }
        [Test] public void FailedMultiBaseInstallRestoresReservedStocksAndCapacity()
        {
            var a=Bank(10);var b=Bank(5);
            var payments=new[]{new ConstructionPurchase.Payment{Bank=a,Resource="iron",Amount=4},new ConstructionPurchase.Payment{Bank=b,Resource="iron",Amount=3}};
            Assert.That(ConstructionPurchase.TryCommit(payments,()=>{
                Assert.That(a.Amount("iron"),Is.EqualTo(6));Assert.That(b.Amount("iron"),Is.EqualTo(2));
                a.Withdraw("iron",2);b.AddCapacity("iron",-10);return false;
            },out _),Is.False);
            Assert.That(a.Amount("iron"),Is.EqualTo(10));Assert.That(b.Amount("iron"),Is.EqualTo(5));
            Assert.That(b.Capacity("iron"),Is.EqualTo(1000));
        }
        [Test] public void OverCapacityStockIsPreservedAndRejectsNewDeliveries()
        {
            var b=Bank(100);b.AddCapacity("iron",-950);Assert.That(b.Amount("iron"),Is.EqualTo(100));Assert.That(b.Deposit("iron",30),Is.Zero);Assert.That(b.Withdraw("iron",70),Is.EqualTo(70));Assert.That(b.Deposit("iron",30),Is.EqualTo(20));
        }
        [TestCase(double.NaN)][TestCase(double.PositiveInfinity)][TestCase(-1)]
        public void InvalidTransfersLeaveStockUnchanged(double amount){var b=Bank(100);Assert.That(b.Deposit("iron",amount),Is.Zero);Assert.That(b.Withdraw("iron",amount),Is.Zero);Assert.That(b.Amount("iron"),Is.EqualTo(100));}
        [Test] public void RestoreValidatesBeforeReplacingLedgers()
        {
            string id=Guid.NewGuid().ToString("N");var r=new BaseInventoryRegistry(Bank(0),_=>true);r.Ensure(id).Deposit("iron",30);
            var bad=new List<BaseInventoryRegistry.Record>{new(){baseId=id,stocks=new(){new(){id="iron",amount=double.NaN,capacity=100}}}};
            Assert.Throws<ArgumentException>(()=>r.Restore(bad));Assert.That(r.Ensure(id).Amount("iron"),Is.EqualTo(30));
        }
    }
    public sealed class RailwayTests
    {
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)]
        public void StationDirectionPreviewUsesTheInstalledPortPositions(int turn)
        {
            var direction=WorldGridGeometry.Rotation*Quaternion.Euler(0,turn*90,0)*Vector3.forward;
            var snapped=OpenWorldInput.SnapStationDirection(direction*4);
            Assert.That(Vector3.Dot(snapped,direction),Is.GreaterThan(.999f));
            var cell=new Vector2Int(12,18);
            var installed=Facility(RailFacilityKind.Station,cell,aId);installed.ConfirmDirection(snapped);
            for(int port=0;port<4;port++)Assert.That(RailwayNetwork.PortCell(cell,snapped,port),Is.EqualTo(RailwayNetwork.Port(installed,port)));
        }
        readonly List<UnityEngine.Object> assets=new();readonly List<BuildingInstance> buildings=new();
        readonly string aId=Guid.NewGuid().ToString("N"),bId=Guid.NewGuid().ToString("N");
        ResourceBank a,b;Inventory inventory;RailwayNetwork network;BuildingInstance sa,sb;List<RailStop> draft;
        sealed class Inventory:IRailInventory
        {
            public Dictionary<string,ResourceBank> Banks=new();
            public bool Available(string id)=>id!=null&&Banks.ContainsKey(id);
            public double Load(string id,string resource,double n)=>Banks[id].Withdraw(resource,n);
            public double Unload(string id,string resource,double n)=>Banks[id].Deposit(resource,n);
        }
        BuildingInstance Facility(RailFacilityKind kind,Vector2Int cell,string owner)
        {
            var module=ScriptableObject.CreateInstance<RailFacilityDefinition>();module.Kind=kind;assets.Add(module);
            var def=ScriptableObject.CreateInstance<BuildingDefinition>();assets.Add(def);def.Id=kind.ToString();def.DisplayName=def.Id;def.Footprint=kind==RailFacilityKind.Track?Vector2Int.one:new Vector2Int(2,2);def.Modules.Add(module);
            var instance=new BuildingInstance(buildings.Count+1,def,cell,new Vector3(cell.x*2,0,cell.y*2),new BuildingServices(null));instance.RestoreIdentity(instance.PersistentId,owner);instance.ConfirmDirection(Quaternion.Euler(0,45,0)*Vector3.forward);instance.Activate();buildings.Add(instance);return instance;
        }
        [SetUp] public void Setup()
        {
            a=new ResourceBank();b=new ResourceBank();foreach(var bank in new[]{a,b})foreach(string resource in new[]{"iron","coal"})bank.AddCapacity(resource,1000);a.Deposit("iron",300);a.Deposit("coal",100);
            inventory=new Inventory();inventory.Banks.Add(aId,a);inventory.Banks.Add(bId,b);network=new RailwayNetwork(inventory,"coal",1);
            sa=Facility(RailFacilityKind.Station,new Vector2Int(0,0),aId);sb=Facility(RailFacilityKind.Station,new Vector2Int(6,0),bId);
            foreach(var cell in new[]{new Vector2Int(2,1),new Vector2Int(3,1),new Vector2Int(4,1),new Vector2Int(5,1),new Vector2Int(5,0)})Facility(RailFacilityKind.Track,cell,aId);
            for(int y=1;y<=4;y++)Facility(RailFacilityKind.Track,new Vector2Int(8,y),aId);
            for(int x=7;x>=-1;x--)Facility(RailFacilityKind.Track,new Vector2Int(x,4),aId);
            for(int y=3;y>=0;y--)Facility(RailFacilityKind.Track,new Vector2Int(-1,y),aId);
            network.Refresh(buildings);draft=new(){new(){stationId=sa.PersistentId,load=100},new(){stationId=sb.PersistentId,unload=100}};
        }
        [Test] public void StructuredValidationDoesNotMutateRouteInventoryOrPriorResult()
        {
            var r=Route();string before=JsonUtility.ToJson(network.Capture());double stock=a.Amount("iron");
            var failure=network.InspectDraft(draft,null);var success=network.InspectDraft(draft,r.id);
            Assert.That(failure.Issue.Code,Is.EqualTo(RouteIssueCode.OccupiedTrack));
            Assert.That(failure.Issue.ConflictingRouteId,Is.EqualTo(r.id));Assert.That(success.Valid,Is.True);
            Assert.That(success.Legs.Count,Is.EqualTo(2));Assert.That(failure.Issue.Code,Is.EqualTo(RouteIssueCode.OccupiedTrack));
            Assert.That(JsonUtility.ToJson(network.Capture()),Is.EqualTo(before));Assert.That(a.Amount("iron"),Is.EqualTo(stock));
        }
        [Test] public void StructuredValidationIdentifiesDuplicateVisitAndStation()
        {
            draft.Add(draft[0].Copy());var issue=network.InspectDraft(draft,null).Issue;
            Assert.That(issue.Code,Is.EqualTo(RouteIssueCode.DuplicateStation));Assert.That(issue.StopIndex,Is.EqualTo(2));
            Assert.That(issue.StationId,Is.EqualTo(sa.PersistentId));Assert.That(issue.Cell,Is.EqualTo(sa.Cell));Assert.That(issue.Recovery,Is.Not.Empty);
        }
        [Test] public void StructuredValidationSeparatesInactiveStationFromMissingReference()
        {
            inventory.Banks.Remove(bId);var issue=network.InspectDraft(draft,null).Issue;
            Assert.That(issue.Code,Is.EqualTo(RouteIssueCode.InactiveStation));Assert.That(issue.StopIndex,Is.EqualTo(1));Assert.That(issue.Cell,Is.EqualTo(sb.Cell));
            draft[1].stationId=Guid.NewGuid().ToString("N");issue=network.InspectDraft(draft,null).Issue;
            Assert.That(issue.Code,Is.EqualTo(RouteIssueCode.InactiveStation));Assert.That(issue.Cell,Is.Null);
        }
        [TestCase(-1,1)][TestCase(0,4)][TestCase(1,1)]
        public void StructuredValidationRejectsInvalidOrIdenticalPorts(int arrival,int departure)
        {
            draft[1].arrival=arrival;draft[1].departure=departure;var issue=network.InspectDraft(draft,null).Issue;
            Assert.That(issue.Code,Is.EqualTo(RouteIssueCode.InvalidPort));Assert.That(issue.StationId,Is.EqualTo(sb.PersistentId));Assert.That(issue.StopIndex,Is.EqualTo(1));
        }
        [Test] public void StructuredValidationMarksReturnLegAndPreservesCompletedLeg()
        {
            buildings.First(x=>x.Cell==new Vector2Int(8,1)).Dispose();network.Refresh(buildings);
            var result=network.InspectDraft(draft,null);var issue=result.Issue;
            Assert.That(issue.Code,Is.EqualTo(RouteIssueCode.MissingTrack));Assert.That(issue.ReturnLeg,Is.True);Assert.That(issue.LegIndex,Is.EqualTo(1));
            Assert.That(issue.StationId,Is.EqualTo(sb.PersistentId));Assert.That(issue.DestinationId,Is.EqualTo(sa.PersistentId));Assert.That(issue.Port,Is.EqualTo(1));Assert.That(result.Legs.Count,Is.EqualTo(1));
            Assert.That(network.ValidateDraft(draft,null,out _,out var message),Is.False);Assert.That(network.InvalidCell,Is.EqualTo(issue.Cell));Assert.That(message,Is.EqualTo(issue.Message));
        }
        [Test] public void StructuredValidationSeparatesBranchFromArrivalOverrun()
        {
            var extra=Facility(RailFacilityKind.Track,new Vector2Int(3,2),aId);network.Refresh(buildings);
            var issue=network.InspectDraft(draft,null).Issue;Assert.That(issue.Code,Is.EqualTo(RouteIssueCode.Branch));Assert.That(issue.Cell,Is.EqualTo(new Vector2Int(3,1)));
            extra.Dispose();Facility(RailFacilityKind.Track,new Vector2Int(5,-1),aId);network.Refresh(buildings);
            issue=network.InspectDraft(draft,null).Issue;Assert.That(issue.Code,Is.EqualTo(RouteIssueCode.ArrivalOverrun));Assert.That(issue.Cell,Is.EqualTo(new Vector2Int(5,0)));
        }
        [Test] public void RouteLongerThanFullFuelTankIsRejectedBeforeCommit()
        {
            Assert.That(network.InspectDraft(draft,null).Valid,Is.True);
            var expensive=new RailwayNetwork(inventory,"coal",100);expensive.Refresh(buildings);
            var inspected=expensive.InspectDraft(draft,null);
            Assert.That(inspected.Valid,Is.False);
            Assert.That(inspected.Issues.All(issue=>issue.Code==RouteIssueCode.InsufficientTankRange),Is.True);
            Assert.That(inspected.Issues[0].LegIndex,Is.Zero);
            Assert.That(inspected.Issues[0].Cell,Is.EqualTo(RailwayNetwork.Port(sa,draft[0].departure)));
            Assert.That(inspected.Issues[0].Message,Does.Contain("연료칸 최대 100"));
            Assert.That(expensive.Commit(Guid.NewGuid().ToString("N"),draft,out _,out var reason),Is.False);
            Assert.That(reason,Does.Contain("연료칸 최대 100"));
            Assert.That(expensive.Routes,Is.Empty);
            Assert.That(a.Amount("coal"),Is.EqualTo(100));
        }
        [Test] public void StructuredValidationSeparatesEmptyDraftAndInvalidQuantity()
        {
            Assert.That(network.InspectDraft(Array.Empty<RailStop>(),null).Issue.Code,Is.EqualTo(RouteIssueCode.TooFewStations));
            draft[0].load=double.NaN;Assert.That(network.InspectDraft(draft,null).Issue.Code,Is.EqualTo(RouteIssueCode.InvalidQuantity));
        }
        [Test] public void MultipleBrokenSegmentsAreReportedAndCommitLeavesStateUntouched()
        {
            buildings.First(b=>b.Cell==new Vector2Int(2,1)).Dispose();buildings.First(b=>b.Cell==new Vector2Int(8,1)).Dispose();network.Refresh(buildings);
            var result=network.InspectDraft(draft,null);Assert.That(result.Issues.Count,Is.EqualTo(2));Assert.That(result.Issues.Select(i=>i.LegIndex),Is.EqualTo(new[]{0,1}));Assert.That(result.Segments.All(s=>s.Status==RouteSegmentStatus.Error),Is.True);
            var before=JsonUtility.ToJson(network.Capture());Assert.That(network.Commit(Guid.NewGuid().ToString("N"),draft,out _,out _),Is.False);Assert.That(JsonUtility.ToJson(network.Capture()),Is.EqualTo(before));Assert.That(a.Amount("iron"),Is.EqualTo(300));
        }
        [Test] public void LaterValidSegmentKeepsItsOriginalIndexAfterEarlierFailure()
        {
            buildings.First(b=>b.Cell==new Vector2Int(2,1)).Dispose();network.Refresh(buildings);var result=network.InspectDraft(draft,null);
            Assert.That(result.Segments.Count,Is.EqualTo(2));Assert.That(result.Segments[0].Status,Is.EqualTo(RouteSegmentStatus.Error));Assert.That(result.Segments[1].Index,Is.EqualTo(1));Assert.That(result.Segments[1].Status,Is.EqualTo(RouteSegmentStatus.Valid));Assert.That(result.Segments[1].Path.cells[0],Is.EqualTo(new Vector2Int(8,1)));Assert.That(result.Legs,Is.Empty,"Legacy valid prefix remains compatible");
        }
        [Test] public void MultipleStationErrorsBlockAffectedSegmentsWithoutInventingTrackFailures()
        {
            draft[0].arrival=draft[0].departure;draft[1].arrival=draft[1].departure;var result=network.InspectDraft(draft,null);
            Assert.That(result.Issues.Count,Is.EqualTo(2));Assert.That(result.Issues.All(i=>i.Code==RouteIssueCode.InvalidPort),Is.True);Assert.That(result.Issues.Select(i=>i.StopIndex),Is.EqualTo(new[]{0,1}));Assert.That(result.Segments.All(s=>s.Status==RouteSegmentStatus.Blocked),Is.True);
        }
        RailRoute Route(){Assert.That(network.Commit(Guid.NewGuid().ToString("N"),draft,out var r,out var error),Is.True,error);Assert.That(network.Configure(r,"iron",0,100,0,out _),Is.True);return r;}
        [TearDown] public void Cleanup(){foreach(var b in buildings)b.Dispose();buildings.Clear();foreach(var a in assets)UnityEngine.Object.DestroyImmediate(a);assets.Clear();}
        [TestCase(null)][TestCase("")] public void UnassignedCargoSurvivesJsonAndAllowsOnlyFirstAssignment(string unset)
        {
            Assert.That(network.Commit(Guid.NewGuid().ToString("N"),draft,out var r,out _),Is.True);
            r.train.resource=unset;var json=JsonUtility.ToJson(network.Capture());
            network.Restore(JsonUtility.FromJson<RailwayNetwork.Snapshot>(json));r=network.Routes.Single();
            Assert.That(network.Start(r,out _),Is.False);
            Assert.That(network.Configure(r,"iron",0,100,0,out var error),Is.True,error);
            Assert.That(network.Configure(r,"coal",0,100,0,out _),Is.False);
            Assert.That(r.train.resource,Is.EqualTo("iron"));
            Assert.That(r.train.cargo,Is.Zero);Assert.That(a.Amount("iron"),Is.EqualTo(300));
        }
        [Test] public void UnassignedCargoCannotHideStockInSnapshot()
        {
            var r=Route();var snapshot=network.Capture();snapshot.routes[0].train.resource="";snapshot.routes[0].train.cargo=1;
            Assert.Throws<ArgumentException>(()=>RailwayNetwork.ValidateSnapshot(snapshot));
        }
        [Test] public void StationErrorAllowsFurtherTrackRecoveryButProtectsOccupiedStation()
        {
            var r=Route();network.Refuel(r,50,out _);double fuel=r.train.fuel;
            var tracks=buildings.Where(x=>x.Module<RailFacility>()?.Kind==RailFacilityKind.Track).ToArray();
            Assert.That(network.CanRecover(tracks[0],out _),Is.True);tracks[0].Dispose();network.Refresh(buildings);
            Assert.That(r.train.status,Is.EqualTo(TrainStatus.RouteError));
            Assert.That(network.CanRecover(tracks[1],out var error),Is.True,error);
            Assert.That(network.CanRecover(sa,out _),Is.False);
            Assert.That(r.train.fuel,Is.EqualTo(fuel));Assert.That(r.train.cargo,Is.Zero);
        }
        [Test] public void MidSegmentErrorStillProtectsTrackAndStations()
        {
            var r=Route();network.Refuel(r,50,out _);network.Start(r,out _);network.Tick(11);
            var tracks=buildings.Where(x=>x.Module<RailFacility>()?.Kind==RailFacilityKind.Track).ToArray();
            tracks[0].Dispose();network.Refresh(buildings);
            Assert.That(r.train.status,Is.EqualTo(TrainStatus.RouteError));Assert.That(r.train.segmentPaid,Is.True);
            Assert.That(network.CanRecover(tracks[1],out _),Is.False);Assert.That(network.CanRecover(sb,out _),Is.False);
        }
        [TestCase(TrainStatus.Stopped)][TestCase(TrainStatus.Moving)][TestCase(TrainStatus.Dwelling)][TestCase(TrainStatus.StopRequested)][TestCase(TrainStatus.FuelWait)][TestCase(TrainStatus.RouteError)]
        public void EveryTransportStateRoundTripsWithoutTransfer(TrainStatus status)
        {
            var r=Route();network.Refuel(r,50,out _);network.Start(r,out _);
            if(status is TrainStatus.Moving or TrainStatus.StopRequested or TrainStatus.RouteError)network.Tick(11);
            r.train.status=status;var snapshot=network.Capture();string before=JsonUtility.ToJson(snapshot);double total=a.Amount("iron")+b.Amount("iron")+r.train.cargo;
            network.Restore(JsonUtility.FromJson<RailwayNetwork.Snapshot>(before));Assert.That(JsonUtility.ToJson(network.Capture()),Is.EqualTo(before));Assert.That(a.Amount("iron")+b.Amount("iron")+network.Routes.Single().train.cargo,Is.EqualTo(total));
        }
        [Test] public void CommitIsIdempotentAndDoesNotStartOrLoad()
        {var r=Route();Assert.That(network.Commit(r.id,draft,out var again,out _),Is.True);Assert.That(again,Is.SameAs(r));Assert.That(network.Routes.Count,Is.EqualTo(1));Assert.That(r.train.status,Is.EqualTo(TrainStatus.Stopped));Assert.That(a.Amount("iron"),Is.EqualTo(300));}
        [Test] public void DeliveryConservesStockAndNextStationStopDoesNotRepeatService()
        {
            var r=Route();network.Refuel(r,50,out _);Assert.That(network.Start(r,out _),Is.True);Assert.That(a.Amount("iron"),Is.EqualTo(200));network.Tick(10);network.Stop(r);network.Tick(5);
            Assert.That(r.train.status,Is.EqualTo(TrainStatus.Stopped));Assert.That(b.Amount("iron"),Is.EqualTo(100));Assert.That(r.train.cargo,Is.Zero);
            Assert.That(network.Start(r,out _),Is.True);Assert.That(b.Amount("iron"),Is.EqualTo(100));Assert.That(a.Amount("iron")+b.Amount("iron")+r.train.cargo,Is.EqualTo(300));
        }
        [Test] public void FullDestinationRetainsCargoAndMissingFuelDoesNotDepart()
        {
            b.Deposit("iron",1000);var r=Route();Assert.That(network.Start(r,out _),Is.False);network.Refuel(r,50,out _);network.Start(r,out _);network.Tick(15);
            Assert.That(r.train.cargo,Is.EqualTo(100));Assert.That(b.Amount("iron"),Is.EqualTo(1000));
        }
        [Test] public void TrackSharingAndBranchesAreRejected()
        {
            var r=Route();Assert.That(network.Commit(Guid.NewGuid().ToString("N"),draft,out _,out _),Is.False);
            Facility(RailFacilityKind.Track,new Vector2Int(3,2),aId);network.Refresh(buildings);Assert.That(r.train.status,Is.EqualTo(TrainStatus.RouteError));Assert.That(network.Start(r,out _),Is.False);
        }
        [Test] public void MovingTrackRecoveryIsBlockedAndCargoResourceIsImmutable()
        {
            var r=Route();Assert.That(network.Configure(r,"coal",0,1,0,out _),Is.False);network.Refuel(r,50,out _);network.Start(r,out _);network.Tick(11);
            Assert.That(network.CanRecover(buildings.First(x=>x.Cell==new Vector2Int(3,1)),out _),Is.False);
        }
        [Test] public void MovingRestorePreservesFuelAndDoesNotDuplicateCargo()
        {
            var r=Route();network.Refuel(r,50,out _);network.Start(r,out _);network.Tick(12);double fuel=r.train.fuel;var saved=network.Capture();network.Restore(saved);r=network.Routes[0];
            Assert.That(r.train.progress,Is.EqualTo(2));network.Tick(3);Assert.That(r.train.fuel,Is.EqualTo(fuel));Assert.That(b.Amount("iron"),Is.EqualTo(100));Assert.That(a.Amount("iron"),Is.EqualTo(200));
        }
        [Test] public void CurvedCircuitEndsAtTheSameStationWithLargeAndPointOneSecondTicks()
        {
            var r=Route();Assert.That(network.Configure(r,"iron",0,0,0,out _),Is.True);
            Assert.That(network.Configure(r,"iron",1,0,0,out _),Is.True);
            Assert.That(network.Refuel(r,100,out _),Is.True);
            Assert.That(network.Start(r,out _),Is.True);
            var baseline=network.Capture();double duration=r.ExpectedSeconds;
            Assert.That(duration*10,Is.EqualTo(Math.Round(duration*10)));
            network.Tick(duration);var large=network.Capture().routes.Single().train;
            network.Restore(baseline);r=network.Routes.Single();
            for(int i=0;i<(int)(duration*10);i++)network.Tick(.1);
            Assert.That(r.train.status,Is.EqualTo(TrainStatus.Dwelling));
            Assert.That(r.train.stop,Is.Zero);
            Assert.That(r.train.status,Is.EqualTo(large.status));
            Assert.That(r.train.progress,Is.EqualTo(large.progress).Within(1e-8));
            Assert.That(r.train.dwell,Is.EqualTo(large.dwell).Within(1e-8));
            Assert.That(r.train.fuel,Is.EqualTo(large.fuel).Within(1e-8));
            Assert.That(r.train.cargo,Is.EqualTo(large.cargo).Within(1e-8));
        }
        [Test] public void InvalidSavedAmountsAndDuplicateTrainsAreRejected()
        {var r=Route();var saved=network.Capture();saved.routes[0].train.cargo=-1;Assert.Throws<ArgumentException>(()=>network.Restore(saved));Assert.That(network.Routes[0],Is.SameAs(r));}
        List<RailStop> ReversePorts()=>draft.Select(s=>new RailStop{stationId=s.stationId,arrival=s.departure,departure=s.arrival}).ToList();
        [Test] public void PendingAppliesOnlyAtFirstStationAndKeepsTrainIdentity()
        {
            var r=Route();string train=r.trainId;network.Refuel(r,50,out _);network.Start(r,out _);network.Tick(11);
            Assert.That(network.UpdateRoute(r,ReversePorts(),r.revision,out var error),Is.True,error);
            Assert.That(r.stops[0].departure,Is.EqualTo(1));Assert.That(r.train.progress,Is.EqualTo(1));
            network.Tick(30);Assert.That(network.HasPending(r),Is.True);network.Tick(1);
            Assert.That(network.HasPending(r),Is.False);Assert.That(r.revision,Is.EqualTo(1));Assert.That(r.stops[0].departure,Is.Zero);
            Assert.That(r.trainId,Is.EqualTo(train));Assert.That(r.train.stop,Is.Zero);Assert.That(r.train.cargo,Is.EqualTo(100));Assert.That(r.stops[0].load,Is.EqualTo(100));
        }
        [Test] public void PendingSaveCancelAndStaleDraftLeaveActiveRouteUntouched()
        {
            var r=Route();network.Refuel(r,50,out _);network.Start(r,out _);network.Tick(12);
            Assert.That(network.UpdateRoute(r,ReversePorts(),0,out _),Is.True);var before=network.Capture();network.Restore(before);r=network.Routes.Single();
            Assert.That(network.HasPending(r),Is.True);Assert.That(r.train.progress,Is.EqualTo(2));
            Assert.That(network.CancelPending(r),Is.True);network.Tick(30);Assert.That(r.revision,Is.Zero);Assert.That(r.stops[0].departure,Is.EqualTo(1));
            Assert.That(network.UpdateRoute(r,ReversePorts(),99,out _),Is.False);Assert.That(network.HasPending(r),Is.False);
        }
        [Test] public void StopRequestWinsAfterBoundaryApplication()
        {
            var r=Route();network.Refuel(r,50,out _);network.Start(r,out _);network.Tick(26);
            Assert.That(network.UpdateRoute(r,ReversePorts(),0,out _),Is.True);network.Stop(r);network.Tick(16);
            Assert.That(r.revision,Is.EqualTo(1));Assert.That(r.train.status,Is.EqualTo(TrainStatus.Stopped));Assert.That(r.train.stop,Is.Zero);
        }
        [Test] public void ChangedPendingPathFailsWithoutChangingValidActiveRoute()
        {
            var r=Route();network.Refuel(r,50,out _);network.Start(r,out _);network.Tick(11);network.UpdateRoute(r,ReversePorts(),0,out _);
            // Saved reservation carries a valid-shaped but different path; no silent reroute at application.
            r.pending.legs[0].cells.Reverse();network.Tick(31);
            Assert.That(r.pending.failed,Is.True);Assert.That(r.revision,Is.Zero);Assert.That(r.stops[0].departure,Is.EqualTo(1));Assert.That(r.train.status,Is.EqualTo(TrainStatus.Dwelling));
        }
        [Test] public void PendingRejectsChangedAnchorAndMalformedSavedRevision()
        {
            var r=Route();network.Refuel(r,50,out _);network.Start(r,out _);var changed=ReversePorts();changed.Reverse();
            Assert.That(network.UpdateRoute(r,changed,0,out _),Is.False);Assert.That(network.UpdateRoute(r,ReversePorts(),0,out _),Is.True);
            var saved=network.Capture();saved.routes[0].pending.baseRevision=42;Assert.Throws<ArgumentException>(()=>network.Restore(saved));Assert.That(network.Routes.Single(),Is.SameAs(r));
        }
        [Test] public void MidSegmentErrorRecoveryKeepsPositionCargoAndPaidFuel()
        {
            var r=Route();network.Refuel(r,50,out _);network.Start(r,out _);network.Tick(12);double fuel=r.train.fuel;
            var branch=Facility(RailFacilityKind.Track,new Vector2Int(3,2),aId);network.Refresh(buildings);Assert.That(r.train.status,Is.EqualTo(TrainStatus.RouteError));
            buildings.Remove(branch);branch.Dispose();network.Refresh(buildings);Assert.That(network.Start(r,out _),Is.True);Assert.That(r.train.progress,Is.EqualTo(2));Assert.That(r.train.fuel,Is.EqualTo(fuel));Assert.That(r.train.cargo,Is.EqualTo(100));network.Tick(3);Assert.That(b.Amount("iron"),Is.EqualTo(100));
        }
        [Test] public void DestinationStationLossStopsPaidSegmentAndResumeDoesNotChargeAgain()
        {
            var r=Route();Assert.That(network.Refuel(r,50,out _),Is.True);Assert.That(network.Start(r,out _),Is.True);
            network.Tick(12);double fuel=r.train.fuel,cargo=r.train.cargo,progress=r.train.progress;
            buildings.Remove(sb);network.Refresh(buildings);
            Assert.That(r.train.status,Is.EqualTo(TrainStatus.RouteError));Assert.That(r.error,Does.Contain("역"));
            network.Tick(20);Assert.That(r.train.progress,Is.EqualTo(progress));Assert.That(r.train.fuel,Is.EqualTo(fuel));Assert.That(r.train.cargo,Is.EqualTo(cargo));
            buildings.Add(sb);network.Refresh(buildings);Assert.That(network.Start(r,out var error),Is.True,error);
            Assert.That(r.train.progress,Is.EqualTo(progress));Assert.That(r.train.fuel,Is.EqualTo(fuel));
            network.Tick(r.legs[0].cells.Count-progress);
            Assert.That(r.train.status,Is.EqualTo(TrainStatus.Dwelling));Assert.That(r.train.stop,Is.EqualTo(1));Assert.That(r.train.fuel,Is.EqualTo(fuel));
            Assert.That(r.train.cargo,Is.Zero);Assert.That(b.Amount("iron"),Is.EqualTo(100));
        }
        [TestCase(3,0)][TestCase(3,1)][TestCase(4,2)][TestCase(4,3)]
        public void MultipleStationsAndRotatedPortsCompleteClosedCircuit(int count,int rotation)
        {
            foreach(var old in buildings)old.Dispose();buildings.Clear();
            Vector2Int Rotate(Vector2Int p,bool station){for(int i=0;i<rotation;i++)p=new Vector2Int(p.y,-p.x-(station?1:0));return p;}
            var stops=new List<RailStop>();for(int i=0;i<count;i++){var station=Facility(RailFacilityKind.Station,Rotate(new Vector2Int(i*6,0),true),i==0?aId:bId);station.ConfirmDirection(Quaternion.Euler(0,45+90*rotation,0)*Vector3.forward);stops.Add(new RailStop{stationId=station.PersistentId,load=i==0?100:0,unload=i==1?100:0});}
            void Track(int x,int y)=>Facility(RailFacilityKind.Track,Rotate(new Vector2Int(x,y),false),aId);
            for(int i=0;i<count-1;i++){for(int x=i*6+2;x<=i*6+5;x++)Track(x,1);Track(i*6+5,0);}
            int end=(count-1)*6+2;for(int y=1;y<=4;y++)Track(end,y);for(int x=end-1;x>=-1;x--)Track(x,4);for(int y=3;y>=0;y--)Track(-1,y);
            network.Refresh(buildings);Assert.That(network.Commit(Guid.NewGuid().ToString("N"),stops,out var r,out var error),Is.True,error);network.Configure(r,"iron",0,100,0,out _);network.Refuel(r,100,out _);network.Start(r,out _);network.Tick(r.ExpectedSeconds);
            Assert.That(r.train.stop,Is.Zero);Assert.That(r.train.status,Is.EqualTo(TrainStatus.Dwelling));Assert.That(b.Amount("iron"),Is.EqualTo(100));Assert.That(a.Amount("iron")+b.Amount("iron")+r.train.cargo,Is.EqualTo(300));
        }
        [TestCase(20,30,1000,30,50)][TestCase(30,40,25,25,45)][TestCase(30,40,1000,40,30)]
        public void ThreeBaseDistributionPreservesRemainder(double unloadB,double unloadC,double capacityC,double expectedC,double remainder)
        {
            foreach(var old in buildings)old.Dispose();buildings.Clear();string cId=Guid.NewGuid().ToString("N");var c=new ResourceBank();c.AddCapacity("iron",capacityC);inventory.Banks.Add(cId,c);
            var stops=new List<RailStop>();for(int i=0;i<3;i++){var station=Facility(RailFacilityKind.Station,new Vector2Int(i*6,0),i==0?aId:i==1?bId:cId);stops.Add(new RailStop{stationId=station.PersistentId,load=i==0?100:0,unload=i==1?unloadB:i==2?unloadC:0});}
            void Track(int x,int y)=>Facility(RailFacilityKind.Track,new Vector2Int(x,y),aId);
            for(int i=0;i<2;i++){for(int x=i*6+2;x<=i*6+5;x++)Track(x,1);Track(i*6+5,0);}for(int y=1;y<=4;y++)Track(14,y);for(int x=13;x>=-1;x--)Track(x,4);for(int y=3;y>=0;y--)Track(-1,y);
            network.Refresh(buildings);Assert.That(network.Commit(Guid.NewGuid().ToString("N"),stops,out var r,out var error),Is.True,error);network.Configure(r,"iron",0,100,0,out _);network.Refuel(r,50,out _);network.Start(r,out _);network.Tick(30);
            Assert.That(r.train.stop,Is.EqualTo(2));Assert.That(a.Amount("iron"),Is.EqualTo(200));Assert.That(b.Amount("iron"),Is.EqualTo(unloadB));Assert.That(c.Amount("iron"),Is.EqualTo(expectedC));Assert.That(r.train.cargo,Is.EqualTo(remainder));Assert.That(a.Amount("iron")+b.Amount("iron")+c.Amount("iron")+r.train.cargo,Is.EqualTo(300));
        }
        [Test] public void Acc015_UnloadOnlyDropsRemainingFiftyWhenTargetIsEighty()
        {
            var r=Route();Assert.That(network.Configure(r,"iron",0,0,0,out var reason),Is.True,reason);
            Assert.That(network.Configure(r,"iron",1,0,80,out reason),Is.True,reason);
            r.train.cargo=50;Assert.That(network.Refuel(r,50,out reason),Is.True,reason);
            Assert.That(network.Start(r,out reason),Is.True,reason);
            Assert.That(r.train.cargo,Is.EqualTo(50));network.Tick(15);
            Assert.That(r.train.stop,Is.EqualTo(1));Assert.That(b.Amount("iron"),Is.EqualTo(50));
            Assert.That(r.train.cargo,Is.Zero);Assert.That(a.Amount("iron"),Is.EqualTo(300));
        }
        [Test] public void Acc019_ThousandTargetLoadsAvailableThreeHundredThenRetriesNextVisit()
        {
            var r=Route();Assert.That(network.Configure(r,"iron",0,1000,0,out var reason),Is.True,reason);
            Assert.That(network.Configure(r,"iron",1,0,0,out reason),Is.True,reason);
            r.train.cargo=300;Assert.That(network.Refuel(r,50,out reason),Is.True,reason);
            Assert.That(network.Start(r,out reason),Is.True,reason);
            Assert.That(r.train.cargo,Is.EqualTo(600),"Cargo had 700 free, but A only had 300");
            Assert.That(a.Amount("iron"),Is.Zero);Assert.That(b.Amount("iron"),Is.Zero);
            Assert.That(a.Deposit("iron",200),Is.EqualTo(200));
            network.Tick(r.ExpectedSeconds);
            Assert.That(r.train.stop,Is.Zero);Assert.That(r.train.cargo,Is.EqualTo(800));
            Assert.That(a.Amount("iron"),Is.Zero);Assert.That(b.Amount("iron"),Is.Zero);
        }
        [Test] public void UnloadBeforeLoadAndLimitedSourceRespectVisitAmounts()
        {
            var r=Route();network.Configure(r,"iron",0,1000,80,out _);r.train.cargo=50;network.Refuel(r,50,out _);network.Start(r,out _);
            Assert.That(r.train.cargo,Is.EqualTo(350));Assert.That(a.Amount("iron"),Is.Zero);
            network.Tick(15);Assert.That(b.Amount("iron"),Is.EqualTo(100));Assert.That(r.train.cargo,Is.EqualTo(250));
        }
        [Test] public void EmptyCargoStillRunsAndMissingReturnCannotCommit()
        {
            a.Withdraw("iron",300);var r=Route();network.Refuel(r,50,out _);Assert.That(network.Start(r,out _),Is.True);network.Tick(11);Assert.That(r.train.status,Is.EqualTo(TrainStatus.Moving));Assert.That(r.train.cargo,Is.Zero);
            var removed=buildings.Single(b=>b.Cell==new Vector2Int(-1,2));removed.Dispose();network.Refresh(buildings);Assert.That(r.train.status,Is.EqualTo(TrainStatus.RouteError));Assert.That(network.ValidateDraft(draft,r.id,out _,out var error),Is.False);Assert.That(error,Does.Contain("구간 2"));Assert.That(network.InvalidCell.HasValue,Is.True);
        }
        [Test] public void BaseNamesAreDistinctAndSurviveReloadAndDefinitionRename()
        {
            var third=Facility(RailFacilityKind.Station,new Vector2Int(20,20),Guid.NewGuid().ToString("N"));
            var named=new RailwayNetwork(inventory,"coal",1,_=>"서브 기지");named.Refresh(buildings);
            var ids=new[]{sa.PersistentId,sb.PersistentId,third.PersistentId};var names=ids.Select(named.StationName).ToArray();
            Assert.That(names.Distinct().Count(),Is.EqualTo(3));
            var restored=new RailwayNetwork(inventory,"coal",1,_=>"상실 기지");restored.Restore(named.Capture());restored.Refresh(buildings.AsEnumerable().Reverse());
            Assert.That(ids.Select(restored.StationName),Is.EqualTo(names));
        }
        [Test] public void DemolitionDoesNotReuseBaseOrStationNumbers()
        {
            var before=network.StationName(sa.PersistentId);buildings.Remove(sa);sa.Dispose();network.Refresh(buildings);
            var replacement=Facility(RailFacilityKind.Station,new Vector2Int(20,20),aId);
            var other=Facility(RailFacilityKind.Station,new Vector2Int(30,30),Guid.NewGuid().ToString("N"));network.Refresh(buildings);
            Assert.That(network.StationName(sa.PersistentId),Is.EqualTo(before));
            Assert.That(network.StationName(replacement.PersistentId),Does.EndWith("기차역 2"));
            Assert.That(network.Capture().baseLabels.Single(x=>x.baseId==other.OwnerBaseId).number,Is.EqualTo(3));
        }
        [Test] public void LegacyBaseNamesMigrateDeterministicallyAndPersist()
        {
            var saved=network.Capture();saved.baseLabels=null;
            var one=new RailwayNetwork(inventory,"coal",1,_=>"서브 기지");one.Restore(saved);
            saved.labels.Reverse();var two=new RailwayNetwork(inventory,"coal",1,_=>"서브 기지");two.Restore(saved);
            Assert.That(one.StationName(sa.PersistentId),Is.EqualTo(two.StationName(sa.PersistentId)));
            var json=JsonUtility.ToJson(one.Capture());two.Restore(JsonUtility.FromJson<RailwayNetwork.Snapshot>(json));
            Assert.That(JsonUtility.ToJson(two.Capture()),Is.EqualTo(json));
        }
        [TestCase("duplicate-number")][TestCase("duplicate-id")][TestCase("missing-base")][TestCase("empty-name")]
        public void MalformedBaseNamesRejectRestoreWithoutMutation(string corruption)
        {
            var before=JsonUtility.ToJson(network.Capture());var saved=network.Capture();
            if(corruption=="duplicate-number")saved.baseLabels[1].number=saved.baseLabels[0].number;
            if(corruption=="duplicate-id")saved.baseLabels[1].baseId=saved.baseLabels[0].baseId;
            if(corruption=="missing-base")saved.baseLabels.RemoveAt(1);
            if(corruption=="empty-name")saved.baseLabels[0].name=" ";
            Assert.Throws<ArgumentException>(()=>network.Restore(saved));Assert.That(JsonUtility.ToJson(network.Capture()),Is.EqualTo(before));
        }
        [Test] public void PendingDoesNotReserveNewTrackAndCollisionAtBoundaryKeepsOriginalRoute()
        {
            foreach(var old in buildings)old.Dispose();buildings.Clear();
            var first=Facility(RailFacilityKind.Station,new Vector2Int(0,0),aId);var east=Facility(RailFacilityKind.Station,new Vector2Int(6,0),bId);var west=Facility(RailFacilityKind.Station,new Vector2Int(-6,0),bId);
            void Track(int x,int y)=>Facility(RailFacilityKind.Track,new Vector2Int(x,y),aId);
            for(int x=2;x<=5;x++)Track(x,1);Track(5,0);
            for(int y=-1;y>=-3;y--)Track(7,y);for(int x=6;x>=1;x--)Track(x,-3);Track(1,-2);Track(1,-1);
            for(int x=-1;x>=-4;x--)Track(x,0);Track(-4,1);
            for(int y=2;y<=4;y++)Track(-6,y);for(int x=-5;x<=0;x++)Track(x,4);Track(0,3);Track(0,2);
            network.Refresh(buildings);
            var original=new[]{new RailStop{stationId=first.PersistentId,arrival=2,departure=1,load=100},new RailStop{stationId=east.PersistentId,arrival=0,departure=2,unload=100}};
            var candidate=new[]{new RailStop{stationId=first.PersistentId,arrival=3,departure=0},new RailStop{stationId=west.PersistentId,arrival=1,departure=3,load=999}};
            Assert.That(network.Commit(Guid.NewGuid().ToString("N"),original,out var r,out var error),Is.True,error);network.Configure(r,"iron",0,100,0,out _);network.Refuel(r,50,out _);network.Start(r,out _);
            Assert.That(network.UpdateRoute(r,candidate,0,out error),Is.True,error);Assert.That(r.pending.stops[1].load,Is.Zero,"New station settings reset");
            Assert.That(network.Commit(Guid.NewGuid().ToString("N"),candidate,out var competitor,out error),Is.True,error);
            network.Tick(r.ExpectedSeconds);Assert.That(r.pending.failed,Is.True);Assert.That(r.pending.error,Does.Contain("다른 노선"));Assert.That(r.revision,Is.Zero);Assert.That(r.stops[1].stationId,Is.EqualTo(east.PersistentId));Assert.That(r.train.status,Is.EqualTo(TrainStatus.Dwelling));Assert.That(competitor.train.status,Is.EqualTo(TrainStatus.Stopped));
        }
    }
}
