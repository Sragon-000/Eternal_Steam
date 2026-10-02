using System;
using System.Collections.Generic;
using System.Linq;
using EternalSteam.Railway;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    /// <summary>Explicit preview followed by one synchronous, revalidated construction transaction.</summary>
    public sealed class RailwayConstruction
    {
        readonly OpenWorldContent content;readonly WorldEditSession edits;readonly BuildingDefinition track;
        string payer;RailPlan plan;readonly Dictionary<Vector2Int,string> reused=new();
        public RailPlan Plan=>plan;
        public string Payer=>payer;
        public RailwayConstruction(OpenWorldContent content,WorldEditSession edits,BuildingDefinition track){this.content=content;this.edits=edits;this.track=track;}
        public RailPlan Preview(string from,string to,int startPort=-1,int endPort=-1)
        {
            content.Railway.Refresh();var network=content.Railway.Network;var a=network.Station(from);var b=network.Station(to);payer=content.Bases.SelectedBaseId;reused.Clear();
            if(a==null||b==null||from==to||edits.Count>0){return plan=new RailPlan{Status=RailPlanStatus.NoPath};}
            var grid=content.GroundWorld.Grid;var cache=new Dictionary<Vector2Int,int>();
            int Cost(Vector2Int cell){
                if(cache.TryGetValue(cell,out int old))return old;
                int value=-1;
                if(grid.Bounds.Contains(cell)&&!network.TrackAssigned(cell)&&!network.TrackConnected(cell)){
                    if(network.HasTrack(cell))value=0;
                    else if(content.GroundPlacement.Validate(new PlacementRequest(-1,track,cell)).Success)value=1;
                }
                cache[cell]=value;return value;
            }
            int available=content.GroundWorld.Buildings.Count(v=>v.Module<RailFacility>()?.Kind==RailFacilityKind.Track&&!network.TrackConnected(v.Cell)&&!network.TrackAssigned(v.Cell));
            plan=RailPathPlanner.Find(from,to,Enumerable.Range(0,4).Select(p=>RailwayNetwork.Port(a,p)).ToArray(),Enumerable.Range(0,4).Select(p=>RailwayNetwork.Port(b,p)).ToArray(),
                p=>(startPort<0||startPort==p)&&!network.SocketConnected(from,p),p=>(endPort<0||endPort==p)&&!network.SocketConnected(to,p),Cost,available);
            if(plan.Status==RailPlanStatus.Found)foreach(var cell in plan.Connection.cells)if(network.HasTrack(cell)&&grid.OccupantAt(cell) is int id&&content.GroundWorld.TryGet(id,out var building))reused.Add(cell,building.PersistentId);
            return plan;
        }
        public WorldEditSession.ConstructionQuote Quote()=>plan?.Status==RailPlanStatus.Found&&plan.NewCells>0?edits.Quote(track,plan.NewCells):edits.Quote();
        public bool CanConfirm(out string error)
        {
            error=null;content.Railway.Refresh();
            if(plan?.Status!=RailPlanStatus.Found){error="먼저 연결 경로를 미리 보세요.";return false;}
            if(edits.Count!=0){error="다른 건설 작업을 확정하거나 취소하세요.";return false;}
            if(payer!=content.Bases.SelectedBaseId||content.Inventories.Available(payer)==null){error="지불 기지가 변경되거나 비활성화됐습니다. 다시 미리 보세요.";return false;}
            if(plan.NewCells>512){error="한 번에 새 선로 512칸까지 연결할 수 있습니다. 경유역을 추가하세요.";return false;}
            if(!content.Railway.Network.ValidateConnection(plan.Connection,false,out error))return false;
            var grid=content.GroundWorld.Grid;
            foreach(var cell in plan.Connection.cells){
                if(reused.TryGetValue(cell,out string id)){
                    if(grid.OccupantAt(cell) is not int local||!content.GroundWorld.TryGet(local,out var building)||building.PersistentId!=id){error="재사용 선로가 변경됐습니다. 다시 미리 보세요.";return false;}
                }else {var check=content.GroundPlacement.Validate(new PlacementRequest(-1,track,cell));if(!check.Success){error=check.Message;return false;}}
            }
            var quote=Quote();error=quote.Reason;return quote.Affordable;
        }
        public bool Confirm(out string error)
        {
            if(!CanConfirm(out error))return false;
            var network=content.Railway.Network;var connection=plan.Connection;
            if(plan.NewCells==0)return network.Connect(connection,out error);
            try{
                foreach(var cell in connection.cells)if(!reused.ContainsKey(cell)&&!edits.AddContent(track,content.GroundWorld.Grid.Center(cell,Vector2Int.one),WorldGridGeometry.Rotation*Vector3.forward,out error))return false;
                var result=edits.Confirm(()=>{
                    content.Railway.Refresh();return network.Connect(connection,out var why)?PlacementResult.Ok:new PlacementResult("connection",why);
                });
                error=result.Message;return result.Success;
            }finally{edits.Cancel();content.Railway.Refresh();}
        }
    }
}
