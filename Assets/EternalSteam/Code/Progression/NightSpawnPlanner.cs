using System;
using System.Collections.Generic;
namespace EternalSteam
{
    // Uniform reservoir sampling without replacement. Existing valid points survive refreshes.
    public sealed class NightSpawnPlanner
    {
        [Saved] List<int> points=new();
        readonly int[] candidates;
        [Saved(1)] uint random;
        public IReadOnlyList<int> Points=>points;
        [Saved(0)] public long Pending {get;private set;}
        [Saved(-1)] public long LastBudgetDay {get;private set;}=-1;
        public NightSpawnPlanner(int cells,uint seed){candidates=new int[cells];random=seed==0?1:seed;}
        uint Next(){random^=random<<13;random^=random>>17;random^=random<<5;return random;}
        int Choose(int limit){uint bound=(uint)limit,threshold=unchecked(0u-bound)%bound,value;do{value=Next();}while(value<threshold);return (int)(value%bound);}
        public void BeginNight(long day,long budget)
        {
            if(day<=LastBudgetDay)return;
            LastBudgetDay=day;Pending=budget>long.MaxValue-Pending?long.MaxValue:Pending+Math.Max(0,budget);points.Clear();
        }
        public void Reconcile(bool[] eligible,int desired,IReadOnlyList<int> fixedPoints=null)
        {
            if(eligible.Length!=candidates.Length)throw new ArgumentException("Cell count changed");
            desired=Math.Max(0,desired);
            points.RemoveAll(p=>!eligible[p]);
            if(fixedPoints!=null) {
                // Restore authored points before retaining temporary replacements.
                for(int i=fixedPoints.Count-1;i>=0;i--){int p=fixedPoints[i];if(p<0||p>=eligible.Length||!eligible[p])continue;points.Remove(p);points.Insert(0,p);}
            }
            if(points.Count>desired)points.RemoveRange(desired,points.Count-desired);
            int count=0;for(int i=0;i<eligible.Length;i++)if(eligible[i]&&!points.Contains(i))candidates[count++]=i;
            while(points.Count<desired&&count>0){int selected=Choose(count);points.Add(candidates[selected]);candidates[selected]=candidates[--count];}
        }
        public bool Consume(){if(Pending<=0)return false;Pending--;return true;}
        public void Stop(){Pending=0;points.Clear();}
    }
}
