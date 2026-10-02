using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace EternalSteam.Railway
{
    public enum RailPlanStatus { Found, NoPath, SearchLimit, Cancelled }
    public sealed class RailPlan
    {
        public RailPlanStatus Status;
        public RailConnection Connection;
        public int NewCells,ReusedCells,Turns,Expanded;
    }
    /// <summary>Bounded deterministic A*: new cells first, turns second, distance third. No world mutation.</summary>
    public static class RailPathPlanner
    {
        static readonly Vector2Int[] Directions={Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
        sealed class Node
        {
            public Vector2Int cell;public int direction,port,fresh,turns,length,estimate,serial;public Node previous;
        }
        public static RailPlan Find(string from,string to,IReadOnlyList<Vector2Int> starts,IReadOnlyList<Vector2Int> ends,
            Func<int,bool> startAllowed,Func<int,bool> endAllowed,Func<Vector2Int,int> cellCost,int reusableCount,
            int expansionLimit=40000,Func<bool> cancelled=null)
        {
            if(starts==null||ends==null||starts.Count!=4||ends.Count!=4||cellCost==null||expansionLimit<1||reusableCount<0)throw new ArgumentException("Invalid railway search inputs");
            var result=new RailPlan();var targets=Enumerable.Range(0,4).Where(endAllowed).ToArray();
            if(targets.Length==0){result.Status=RailPlanStatus.NoPath;return result;}
            int Estimate(Vector2Int c)=>targets.Min(p=>Math.Abs(c.x-ends[p].x)+Math.Abs(c.y-ends[p].y));
            var comparer=Comparer<Node>.Create((a,b)=>{
                int compare=(a.fresh+Math.Max(0,a.estimate-reusableCount)).CompareTo(b.fresh+Math.Max(0,b.estimate-reusableCount));
                if(compare==0)compare=a.turns.CompareTo(b.turns);if(compare==0)compare=(a.length+a.estimate).CompareTo(b.length+b.estimate);if(compare==0)compare=a.estimate.CompareTo(b.estimate);return compare!=0?compare:a.serial.CompareTo(b.serial);
            });
            var queue=new SortedSet<Node>(comparer);var best=new Dictionary<(Vector2Int,int,int),(int,int,int)>();int serial=0;
            void Offer(Node n){var key=(n.cell,n.direction,n.port);var score=(n.fresh,n.turns,n.length);if(best.TryGetValue(key,out var old)&&old.CompareTo(score)<=0)return;best[key]=score;n.estimate=Estimate(n.cell);n.serial=serial++;queue.Add(n);}
            for(int p=0;p<4;p++)if(startAllowed(p)){int cost=cellCost(starts[p]);if(cost>=0)Offer(new Node{cell=starts[p],direction=-1,port=p,fresh=cost,length=1});}
            while(queue.Count>0){
                if(cancelled?.Invoke()==true){result.Status=RailPlanStatus.Cancelled;return result;}
                var n=queue.Min;queue.Remove(n);if(best[(n.cell,n.direction,n.port)]!=(n.fresh,n.turns,n.length))continue;
                if(result.Expanded>=expansionLimit){result.Status=RailPlanStatus.SearchLimit;return result;}result.Expanded++;
                int end=targets.Where(p=>ends[p]==n.cell).DefaultIfEmpty(-1).First();
                if(end>=0){var path=new List<Vector2Int>();for(var at=n;at!=null;at=at.previous)path.Add(at.cell);path.Reverse();
                    result.Status=RailPlanStatus.Found;result.Connection=new RailConnection{id=Guid.NewGuid().ToString("N"),fromStation=from,toStation=to,fromPort=n.port,toPort=end,cells=path};result.NewCells=n.fresh;result.ReusedCells=path.Count-n.fresh;result.Turns=n.turns;return result;}
                for(int d=0;d<4;d++){if(n.previous!=null&&n.previous.cell==n.cell+Directions[d])continue;var cell=n.cell+Directions[d];int cost=cellCost(cell);if(cost<0)continue;
                    Offer(new Node{cell=cell,direction=d,port=n.port,fresh=n.fresh+cost,turns=n.turns+(n.direction>=0&&n.direction!=d?1:0),length=n.length+1,previous=n});}
            }
            result.Status=RailPlanStatus.NoPath;return result;
        }
    }
}
