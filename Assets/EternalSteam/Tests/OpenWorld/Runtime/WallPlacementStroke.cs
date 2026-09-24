using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // A stroke owns only the reservations it added. Gesture cancellation preserves earlier edits.
    public sealed class WallPlacementStroke
    {
        readonly WorldEditSession edits;
        readonly HashSet<Vector2Int> visited=new();
        readonly List<WorldEditSession.PendingContent> added=new();
        BuildingDefinition definition;
        Vector2Int previous;
        bool hasPrevious;
        public int Added=>added.Count;
        public int Rejected {get;private set;}
        public string LastFailure {get;private set;}
        public WallPlacementStroke(WorldEditSession edits){this.edits=edits;}
        public static bool Supports(BuildingDefinition definition)
        {
            if(definition==null||definition.Footprint!=Vector2Int.one||definition.Modules==null)return false;
            foreach(var module in definition.Modules)if(module is BuildingCombatDefinition body&&body.Role==BuildingCombatRole.Wall)return true;
            return false;
        }
        public void Begin(BuildingDefinition value){Cancel();definition=value;}
        public void AddPoint(Vector3 point)
        {
            if(!Supports(definition)||!float.IsFinite(point.sqrMagnitude))return;
            var cell=WorldGridGeometry.Cell(point,2);
            if(!hasPrevious)Visit(cell);
            else {
                // Four-connected supercover: diagonal strokes leave no corner-only wall gaps.
                int x=previous.x,z=previous.y,nx=Math.Abs(cell.x-x),nz=Math.Abs(cell.y-z);
                int sx=Math.Sign(cell.x-x),sz=Math.Sign(cell.y-z),ix=0,iz=0;
                Visit(previous);
                while(ix<nx||iz<nz){
                    if((1L+2L*ix)*nz<=(1L+2L*iz)*nx){x+=sx;ix++;}else{z+=sz;iz++;}
                    Visit(new Vector2Int(x,z));
                }
            }
            previous=cell;hasPrevious=true;
        }
        void Visit(Vector2Int cell)
        {
            if(!visited.Add(cell))return;
            if(edits.AddContent(definition,WorldGridGeometry.Center(cell,2),Vector3.forward,out var reason))added.Add(edits.ContentPending[edits.ContentPending.Count-1]);
            else{Rejected++;LastFailure=reason;}
        }
        public void BreakSegment()=>hasPrevious=false;
        public void Complete(){added.Clear();visited.Clear();hasPrevious=false;definition=null;Rejected=0;LastFailure=null;}
        public void Cancel(){foreach(var item in added)edits.Remove(item);Complete();}
    }
}
