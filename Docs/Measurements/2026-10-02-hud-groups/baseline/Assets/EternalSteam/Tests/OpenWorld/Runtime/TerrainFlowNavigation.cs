using System;
using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam.OpenWorld
{
    // Terrain only: four-way connectivity prevents cutting across blocked corners.
    // Buildings are still intercepted and attacked by EnemyBuildingCombat.
    // A reverse distance field is shared for a target cell, never searched per enemy.
    public sealed class TerrainFlowNavigation
    {
        sealed class Field {public int Goal;public int[] Distance;public long Used;}
        readonly List<Field> fields=new();
        readonly int maximumFields;
        int[] regions,queue;
        long used;
        int buildBudget=int.MaxValue;
        public TileWorldGround.TraversalSnapshot Grid {get;private set;}
        public int CachedFields=>fields.Count;
        public int FieldsBuilt {get;private set;}
        public long DetourSteps {get;private set;}
        public TerrainFlowNavigation(TileWorldGround.TraversalSnapshot grid,int maximumFields=16)
        {if(maximumFields<1)throw new ArgumentOutOfRangeException(nameof(maximumFields));this.maximumFields=maximumFields;Update(grid);}
        public void Update(TileWorldGround.TraversalSnapshot grid)
        {
            if(Grid.SameGrid(grid))return;
            Grid=grid;regions=null;queue=null;ClearFields();
        }
        public void ClearFields(){fields.Clear();FieldsBuilt=0;DetourSteps=0;used=0;}
        // Limit expensive new target fields per simulation step; actors wait safely
        // until a field can be built. Already cached targets have no such limit.
        public void BeginStep(int maximumBuilds=2)=>buildBudget=Math.Max(0,maximumBuilds);
        int Neighbour(int cell,int direction)
        {
            int x=cell%Grid.Width;
            return direction switch {0=>x>0?cell-1:-1,1=>x+1<Grid.Width?cell+1:-1,2=>cell>=Grid.Width?cell-Grid.Width:-1,_=>cell+Grid.Width<Grid.Count?cell+Grid.Width:-1};
        }
        void Connectivity()
        {
            if(regions!=null)return;
            regions=new int[Grid.Count];queue=new int[Grid.Count];int region=0;
            for(int start=0;start<regions.Length;start++){
                if(regions[start]!=0||!Grid.Walkable(start))continue;
                int read=0,write=0;regions[start]=++region;queue[write++]=start;
                while(read<write){int cell=queue[read++];for(int d=0;d<4;d++){int next=Neighbour(cell,d);if(next<0||regions[next]!=0||!Grid.Walkable(next))continue;regions[next]=region;queue[write++]=next;}}
            }
        }
        public bool HasRoute(Vector3 from,Vector3 goal)
        {
            if(!Grid.TryIndex(from,out int a)||!Grid.TryIndex(goal,out int b))return false;
            Connectivity();return regions[a]!=0&&regions[a]==regions[b];
        }
        Field For(int goal)
        {
            foreach(var field in fields)if(field.Goal==goal){field.Used=++used;return field;}
            if(buildBudget==0)return null;buildBudget--;
            Field result;
            if(fields.Count<maximumFields){result=new Field{Distance=new int[Grid.Count]};fields.Add(result);}
            else {result=fields[0];foreach(var field in fields)if(field.Used<result.Used)result=field;}
            result.Goal=goal;result.Used=++used;Array.Fill(result.Distance,-1);
            int read=0,write=0;result.Distance[goal]=0;queue[write++]=goal;
            while(read<write){int cell=queue[read++];for(int d=0;d<4;d++){int next=Neighbour(cell,d);if(next<0||result.Distance[next]>=0||!Grid.Walkable(next))continue;result.Distance[next]=result.Distance[cell]+1;queue[write++]=next;}}
            FieldsBuilt++;return result;
        }
        public bool TryStep(Vector3 from,Vector3 goal,float maximumDistance,out Vector3 next)
        {
            next=from;if(!float.IsFinite(maximumDistance)||maximumDistance<=0||!HasRoute(from,goal))return false;
            Grid.TryIndex(from,out int cell);Grid.TryIndex(goal,out int end);
            if(cell==end){goal.y=from.y;next=Vector3.MoveTowards(from,goal,maximumDistance);return Grid.HasClearRoute(from,next);}
            var field=For(end);if(field==null)return false;
            int best=-1,distance=field.Distance[cell];
            for(int d=0;d<4;d++){int candidate=Neighbour(cell,d);if(candidate<0)continue;int value=field.Distance[candidate];if(value>=0&&value<distance){best=candidate;distance=value;}}
            if(best<0)return false;
            var waypoint=Grid.Center(best);waypoint.y=from.y;var step=Vector3.MoveTowards(from,waypoint,maximumDistance);
            // Crossing a cell boundary before reaching its center can otherwise
            // cut a blocked corner on the next turn. Center within the current cell first.
            if(!Grid.HasClearRoute(from,step)){waypoint=Grid.Center(cell);waypoint.y=from.y;step=Vector3.MoveTowards(from,waypoint,maximumDistance);}
            if(!Grid.HasClearRoute(from,step))return false;
            next=step;DetourSteps++;return true;
        }
    }
}
