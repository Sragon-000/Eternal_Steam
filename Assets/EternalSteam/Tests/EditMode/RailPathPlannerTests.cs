using System;
using System.Collections.Generic;
using System.Linq;
using EternalSteam.Railway;
using NUnit.Framework;
using UnityEngine;
namespace EternalSteam.Tests
{
    public sealed class RailPathPlannerTests
    {
        static Vector2Int[] S(Vector2Int p)=>Enumerable.Repeat(p,4).ToArray();
        static RailPlan Find(Vector2Int a,Vector2Int b,Func<Vector2Int,int> cost,int reused=0,int limit=40000,Func<bool> cancel=null)=>RailPathPlanner.Find(Guid.NewGuid().ToString("N"),Guid.NewGuid().ToString("N"),S(a),S(b),p=>p==0,p=>p==0,cost,reused,limit,cancel);
        [Test] public void FiveHundredSquareSearchIsBoundedAndDeterministic()
        {
            var bounds=new RectInt(0,0,500,500);int Cost(Vector2Int p)=>bounds.Contains(p)?1:-1;
            var a=Find(Vector2Int.zero,new Vector2Int(499,499),Cost);var b=Find(Vector2Int.zero,new Vector2Int(499,499),Cost);
            Assert.That(a.Status,Is.EqualTo(RailPlanStatus.Found));Assert.That(a.NewCells,Is.EqualTo(999));Assert.That(a.Turns,Is.EqualTo(1));Assert.That(a.Expanded,Is.LessThanOrEqualTo(40000));Assert.That(a.Connection.cells,Is.EqualTo(b.Connection.cells));
        }
        [Test] public void ReuseWinsOverShorterAllNewPath()
        {
            var reuse=new HashSet<Vector2Int>{new(0,0),new(0,1),new(1,1),new(2,1),new(3,1),new(4,1),new(4,0)};
            var bounds=new RectInt(0,0,5,2);var plan=Find(Vector2Int.zero,new Vector2Int(4,0),p=>!bounds.Contains(p)?-1:reuse.Contains(p)?0:1,reuse.Count);
            Assert.That(plan.Status,Is.EqualTo(RailPlanStatus.Found));Assert.That(plan.NewCells,Is.Zero);Assert.That(plan.ReusedCells,Is.EqualTo(7));Assert.That(plan.Turns,Is.EqualTo(2));
        }
        [Test] public void BlockedAndSearchLimitAndCancellationAreDistinct()
        {
            Assert.That(Find(Vector2Int.zero,Vector2Int.right,p=>p==Vector2Int.zero?1:-1).Status,Is.EqualTo(RailPlanStatus.NoPath));
            Assert.That(Find(Vector2Int.zero,new Vector2Int(8,8),p=>1,limit:1).Status,Is.EqualTo(RailPlanStatus.SearchLimit));
            Assert.That(Find(Vector2Int.zero,Vector2Int.right,p=>1,cancel:()=>true).Status,Is.EqualTo(RailPlanStatus.Cancelled));
        }
        [Test] public void DetourRemainsContiguousAndNeverRepeatsCell()
        {
            var bounds=new RectInt(0,0,10,10);var plan=Find(Vector2Int.zero,new Vector2Int(9,0),p=>bounds.Contains(p)&&!(p.x==5&&p.y<8)?1:-1);
            Assert.That(plan.Status,Is.EqualTo(RailPlanStatus.Found));var path=plan.Connection.cells;Assert.That(path.Distinct().Count(),Is.EqualTo(path.Count));
            for(int i=1;i<path.Count;i++)Assert.That(Math.Abs(path[i].x-path[i-1].x)+Math.Abs(path[i].y-path[i-1].y),Is.EqualTo(1));
        }
    }
}
