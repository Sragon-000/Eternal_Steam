// Standalone algorithm checks. Synthetic building handles intentionally do not test Unity lifecycle or rendering.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using EternalSteam;
using EternalSteam.Railway;
using UnityEngine;
public static class RailwayCoreChecks
{
    sealed class Inventory:IRailInventory
    {
        public readonly Dictionary<string,ResourceBank> Banks=new();
        public bool Available(string id)=>id!=null&&Banks.ContainsKey(id);
        public double Load(string id,string r,double n)=>Banks[id].Withdraw(r,n);
        public double Unload(string id,string r,double n)=>Banks[id].Deposit(r,n);
    }
    sealed class Fixture
    {
        public Inventory Inventory=new();public RailwayNetwork Network;public List<BuildingInstance> Buildings=new();public List<RailStop> Draft=new();
        public ResourceBank A=new(),B=new();public BuildingInstance SA,SB;public string AId=Guid.NewGuid().ToString("N"),BId=Guid.NewGuid().ToString("N");
        public Fixture(){foreach(var bank in new[]{A,B})foreach(var r in new[]{"iron","coal"})bank.AddCapacity(r,1000);A.Deposit("iron",300);A.Deposit("coal",100);Inventory.Banks[AId]=A;Inventory.Banks[BId]=B;Network=new RailwayNetwork(Inventory,"coal",1);SA=Add(RailFacilityKind.Station,0,0);SB=Add(RailFacilityKind.Station,6,0,BId);for(int x=2;x<=5;x++)Add(RailFacilityKind.Track,x,1);Add(RailFacilityKind.Track,5,0);for(int y=1;y<=4;y++)Add(RailFacilityKind.Track,8,y);for(int x=7;x>=-1;x--)Add(RailFacilityKind.Track,x,4);for(int y=3;y>=0;y--)Add(RailFacilityKind.Track,-1,y);Network.Refresh(Buildings);Draft.Add(new RailStop{stationId=SA.PersistentId,load=100});Draft.Add(new RailStop{stationId=SB.PersistentId,unload=100});}
        public BuildingInstance Add(RailFacilityKind kind,int x,int y,string owner=null){var b=(BuildingInstance)FormatterServices.GetUninitializedObject(typeof(BuildingInstance));void Set(string name,object value)=>typeof(BuildingInstance).GetField("<"+name+">k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(b,value);Set("PersistentId",Guid.NewGuid().ToString("N"));Set("Cell",new Vector2Int(x,y));Set("OwnerBaseId",owner??AId);Set("Direction",new Vector3(.70710677f,0,.70710677f));Set("Active",true);var f=new RailFacility(kind);f.Initialize(b,null);typeof(BuildingInstance).GetField("modules",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(b,new List<IBuildingModule>{f});Buildings.Add(b);return b;}
        public RailRoute Route(){Check(Network.Commit(Guid.NewGuid().ToString("N"),Draft,out var r,out var error),error);Check(Network.Configure(r,"iron",0,100,0,out error),error);return r;}
        public RailRoute Start(){var r=Route();Check(Network.Refuel(r,50,out var e),e);Check(Network.Start(r,out e),e);return r;}
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static int Main(){int passed=0;void Test(string name,Action a){a();passed++;Console.WriteLine("PASS "+name);}
        Test("Commit is idempotent and manual start",()=>{var f=new Fixture();var r=f.Route();Check(f.Network.Commit(r.id,f.Draft,out var again,out _),"duplicate");Check(ReferenceEquals(r,again)&&f.Network.Routes.Count==1&&r.train.status==TrainStatus.Stopped&&f.A.Amount("iron")==300,"auto start or duplicate");});
        Test("Independent cargo conservation and next-station stop",()=>{var f=new Fixture();var r=f.Start();f.Network.Tick(10);f.Network.Stop(r);f.Network.Tick(5);Check(r.train.status==TrainStatus.Stopped&&f.B.Amount("iron")==100&&f.A.Amount("iron")==200&&r.train.cargo==0,"delivery");Check(f.Network.Start(r,out _),"resume");Check(f.B.Amount("iron")==100,"repeat service");});
        Test("Full destination preserves cargo",()=>{var f=new Fixture();f.B.Deposit("iron",1000);var r=f.Start();f.Network.Tick(15);Check(r.train.cargo==100&&f.B.Amount("iron")==1000,"overflow");});
        Test("Fuel wait requires manual restart",()=>{var f=new Fixture();var r=f.Route();Check(!f.Network.Start(r,out _),"empty fuel starts");Check(f.Network.Refuel(r,50,out _),"refuel");f.Network.Tick(100);Check(r.train.status==TrainStatus.FuelWait&&r.train.cargo==0,"automatic restart");});
        Test("Shared track and branching are rejected",()=>{var f=new Fixture();var r=f.Route();Check(!f.Network.Commit(Guid.NewGuid().ToString("N"),f.Draft,out _,out _),"shared track");f.Add(RailFacilityKind.Track,3,2);f.Network.Refresh(f.Buildings);Check(r.train.status==TrainStatus.RouteError&&!f.Network.Start(r,out _),"branch");});
        Test("Running rail locked and resource immutable",()=>{var f=new Fixture();var r=f.Start();f.Network.Tick(11);Check(!f.Network.CanRecover(f.Buildings.First(b=>b.Cell==new Vector2Int(3,1)),out _),"remove running rail");Check(!f.Network.Configure(r,"coal",0,1,0,out _),"resource changed");});
        Test("Large ticks equal small ticks",()=>{var f=new Fixture();var r=f.Start();var g=new Fixture();var q=g.Start();f.Network.Tick(75);for(int i=0;i<750;i++)g.Network.Tick(.1);Check(r.train.stop==q.train.stop&&Math.Abs(r.train.cargo-q.train.cargo)<.0001&&Math.Abs(r.train.fuel-q.train.fuel)<.0001&&Math.Abs(r.train.progress-q.train.progress)<.0001,"time partition differs");});
        Test("Draft quantities rejected without creating a train",()=>{var f=new Fixture();foreach(double invalid in new[]{double.NaN,double.PositiveInfinity,-1,1001}){f.Draft[0].load=invalid;Check(!f.Network.Commit(Guid.NewGuid().ToString("N"),f.Draft,out _,out _),"invalid draft accepted");}Check(f.Network.Routes.Count==0,"invalid draft created train");});
        Test("Invalid saved quantity rejected",()=>{var f=new Fixture();var r=f.Route();var snapshot=new RailwayNetwork.Snapshot{routes=new(){r},labels=new(){new(){stationId=f.SA.PersistentId,baseId=f.AId,number=1},new(){stationId=f.SB.PersistentId,baseId=f.BId,number=1}}};RailwayNetwork.ValidateSnapshot(snapshot);r.train.cargo=-1;bool threw=false;try{RailwayNetwork.ValidateSnapshot(snapshot);}catch(ArgumentException){threw=true;}Check(threw,"invalid save accepted");});
        Test("Empty cargo sentinel permits first assignment only",()=>{var f=new Fixture();Check(f.Network.Commit(Guid.NewGuid().ToString("N"),f.Draft,out var r,out _),"commit");r.train.resource="";Check(!f.Network.Start(r,out _),"unassigned start");Check(f.Network.Configure(r,"iron",0,100,0,out _),"first assignment rejected");Check(!f.Network.Configure(r,"coal",0,100,0,out _),"resource changed");});
        Test("Station error recovery preserves occupied station protection",()=>{var f=new Fixture();var r=f.Route();var first=f.Buildings.First(b=>b.Cell==new Vector2Int(3,1));var second=f.Buildings.First(b=>b.Cell==new Vector2Int(4,1));Check(f.Network.CanRecover(first,out _),"initial recovery");f.Buildings.Remove(first);f.Network.Refresh(f.Buildings);Check(r.train.status==TrainStatus.RouteError,"missing error");Check(f.Network.CanRecover(second,out _),"next recovery blocked");Check(!f.Network.CanRecover(f.SA,out _),"occupied station removed");r.train.segmentPaid=true;Check(!f.Network.CanRecover(second,out _),"midsegment protection lost");});
        Test("Fuel-wait recovery and unnamed cargo validation",()=>{var f=new Fixture();var r=f.Route();Check(!f.Network.Start(r,out _)&&r.train.status==TrainStatus.FuelWait,"fuel wait");Check(f.Network.CanRecover(f.Buildings.First(b=>b.Cell==new Vector2Int(3,1)),out _),"fuel-wait track locked");Check(!f.Network.CanRecover(f.SA,out _),"fuel-wait occupied station");var snapshot=new RailwayNetwork.Snapshot{routes=new(){r},labels=new(){new(){stationId=f.SA.PersistentId,baseId=f.AId,number=1},new(){stationId=f.SB.PersistentId,baseId=f.BId,number=1}}};r.train.resource="";r.train.cargo=1;bool rejected=false;try{RailwayNetwork.ValidateSnapshot(snapshot);}catch(ArgumentException){rejected=true;}Check(rejected,"unnamed cargo accepted");});
        Console.WriteLine("Passed "+passed+" railway algorithm checks; Unity import/scene/Play not covered.");return 0;
    }
}
