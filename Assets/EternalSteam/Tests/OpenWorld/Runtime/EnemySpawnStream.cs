using EternalSteam.Demo;
using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam.OpenWorld
{
    // A large request is a counter, never an allocation proportional to the request.
    public sealed class EnemySpawnStream
    {
        public const int MaximumRequest = 100000000;
        public const int ActiveLimit = 100000;
        public const int BatchSize = 2048;
        readonly HordeEnemyWorld world;
        readonly NexusDestinationQuery destinations;
        readonly Terrain terrain;
        readonly TileWorldGround tileWorld;
        readonly EnemySpawnLayout layout;
        readonly float spacing;
        readonly int enemyHealth;readonly bool steerDestinations;readonly IBuildingTargetQuery buildingTargets;
        readonly int columns, rows, stride;
        int cursor;
        sealed class Batch{public int Count;public bool Air;}
        readonly Queue<Batch> requests=new();
        public int Pending { get; private set; }
        public EnemySpawnStream(HordeEnemyWorld world, NexusDestinationQuery destinations, Terrain terrain, float spacing, int enemyHealth=10,bool steerDestinations=true,IBuildingTargetQuery buildingTargets=null)
        {
            this.world=world; this.destinations=destinations; this.terrain=terrain;
            this.enemyHealth=Mathf.Max(1,enemyHealth);this.steerDestinations=steerDestinations;this.buildingTargets=buildingTargets;
            terrain.TryGetComponent(out tileWorld);
            this.spacing=float.IsFinite(spacing)?Mathf.Max(.5f,spacing):.75f;
            layout=new EnemySpawnLayout(world.MaxCount);
            columns=Mathf.Max(1,Mathf.FloorToInt((terrain.terrainData.size.x-2)/this.spacing));
            rows=Mathf.Max(1,Mathf.FloorToInt((terrain.terrainData.size.z-2)/this.spacing));
            int total=columns*rows; stride=7919;
            while(Gcd(stride,total)!=1)stride++;
        }
        static int Gcd(int a,int b){while(b!=0){int t=a%b;a=b;b=t;}return a;}
        public bool Request(int count,bool air=false)
        {
            RefreshDestinations();
            if(count<1 || count>MaximumRequest || (long)world.Spawned+Pending+count>MaximumRequest || !destinations.HasTargets)return false;
            Pending+=count;requests.Enqueue(new Batch{Count=count,Air=air});return true;
        }
        public int Pump()
        {
            RefreshDestinations();
            if(Pending==0 || world.FreeCount==0 || !destinations.HasTargets)return 0;
            layout.Begin(world,spacing);
            int spawned=0,total=columns*rows;
            int attempts=Mathf.Min(total,BatchSize*4);
            var origin=terrain.transform.position;
            for(int i=0;i<attempts && spawned<BatchSize && Pending>0 && world.FreeCount>0;i++) {
                int index=cursor;cursor=(cursor+stride)%total;
                var point=origin+new Vector3(1+(index%columns)*spacing,0,1+(index/columns)*spacing);
                if(!destinations.TryNearest(point,out var target))break;
                var delta=target-point;delta.y=0;
                // Spawn outside the base rather than on top of a foundation or turret.
                if(delta.sqrMagnitude<20*20 || !layout.IsFree(point))continue;
                var batch=requests.Peek();
                if(!batch.Air&&buildingTargets!=null&&buildingTargets.FirstBlocker(point,point,out _,out _))continue;
                if(!batch.Air && tileWorld!=null && !tileWorld.HasClearRoute(point,target))continue;
                if(!world.TrySpawn(point,target,2.4f,enemyHealth,batch.Air))break;
                layout.Add(point);Pending--;spawned++;if(--batch.Count==0)requests.Dequeue();
            }
            return spawned;
        }
        public bool HasDestination {get{RefreshDestinations();return destinations.HasTargets;}}
        void RefreshDestinations()
        {
            if(!destinations.Refresh()||!steerDestinations)return;
            // Re-target once per nexus configuration change, not once per enemy per frame.
            for(int i=0;i<world.MaxCount;i++)if(world.GetEnemy(i).alive)
                world.SetDestination(i,destinations.TryNearest(world.GetEnemy(i).position,out var target)?target:(Vector3?)null);
        }
        public void Reset(){Pending=0;cursor=0;requests.Clear();}
    }
}
