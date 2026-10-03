using System.IO;using System.Linq;using Newtonsoft.Json.Linq;using EternalSteam.Demo;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam.Demo
{
    public sealed class ReferenceEnemyWorld
    {
        public EnemyArrivalPolicy ArrivalPolicy {get;set;}
        public IEnemyMovementConstraint MovementConstraint {get;set;}
        public const int Capacity = 4000;
        internal const int GridWidth = 44, GridHeight = 34;
        internal const float CellSize = 2f;
        public const int MaxHealth = 1000;
        public struct Enemy
        {
            public Vector3 position, destination;
            public float speed;
            public bool alive;
            public float slowTime;
            public float movementPenalty;
            public bool air;
            public bool waitingForDestination;
            public int health;
        }
        internal readonly Enemy[] enemies;
        public int MaxCount => enemies.Length;
        internal readonly int[] heads;
        internal int IndexWidth { get; }
        internal int IndexHeight { get; }
        readonly Vector2 indexOrigin;
        internal readonly int[] next;
        readonly int[] free;
        readonly int[] generations;
        public int Generation(int index)=>generations[index];
        public void SetMovementPenalty(int index,float value){enemies[index].movementPenalty=Mathf.Clamp01(value);}
        public void SetDestination(int index,Vector3? destination)
        {
            if(!enemies[index].alive)return;
            enemies[index].waitingForDestination=!destination.HasValue;
            if(!destination.HasValue)return;
            var p=destination.Value;
            if(groundHeight!=null)p.y=groundHeight(p);
            if(enemies[index].air)p.y+=5;
            enemies[index].destination=p;
        }
        public void QueryIndices(Vector3 origin,float radius,List<int> results)
        { results.Clear();for(int z=CellZ(origin.z-radius);z<=CellZ(origin.z+radius);z++)for(int x=CellX(origin.x-radius);x<=CellX(origin.x+radius);x++)for(int i=heads[z*IndexWidth+x];i>=0;i=next[i]){if(!enemies[i].alive)continue;var d=enemies[i].position-origin;d.y=0;if(d.sqrMagnitude<=radius*radius)results.Add(i);} }
        System.Random random;
        readonly Func<Vector3, float> groundHeight;
        readonly bool damageOnEscape;
        public int FreeCount { get; private set; }
        public int Alive { get; private set; }
        public int Killed { get; private set; }
        public int Escaped { get; private set; }
        public int Spawned { get; private set; }
        public int Health { get; private set; } = MaxHealth;
        public bool Defeated => Health == 0;
        public event Action Defeat;
        public event Action<int> SpawnedEnemy;
        public event Action<int,int> KilledEnemy;
        public ref readonly Enemy GetEnemy(int index) => ref enemies[index];
        public ReferenceEnemyWorld(Func<Vector3, float> groundHeight = null, bool damageOnEscape = true, int capacity = Capacity, Rect? indexBounds = null)
        {
            if (capacity < 1 || capacity > 100000) throw new ArgumentOutOfRangeException(nameof(capacity));
            var bounds = indexBounds ?? new Rect(-44,-34,88,68);
            if (!float.IsFinite(bounds.x) || !float.IsFinite(bounds.y) || !float.IsFinite(bounds.width) || !float.IsFinite(bounds.height) || bounds.width<=0 || bounds.height<=0 || bounds.width>4096 || bounds.height>4096) throw new ArgumentOutOfRangeException(nameof(indexBounds));
            indexOrigin = bounds.position;
            IndexWidth = Mathf.CeilToInt(bounds.width/CellSize); IndexHeight = Mathf.CeilToInt(bounds.height/CellSize);
            heads = new int[IndexWidth*IndexHeight];
            enemies = new Enemy[capacity]; next = new int[capacity]; free = new int[capacity]; generations=new int[capacity];
            this.groundHeight = groundHeight; this.damageOnEscape = damageOnEscape; Reset();
        }
        public bool TrySpawn(Vector3 position, Vector3 destination, float speed = 2, int health = 20, bool air = false)
        {
            if (FreeCount == 0 || Defeated || health <= 0 || !float.IsFinite(speed) || speed < 0) return false;
            if (!float.IsFinite(position.sqrMagnitude) || !float.IsFinite(destination.sqrMagnitude)) return false;
            if (groundHeight != null) { position.y = groundHeight(position); destination.y = groundHeight(destination); }
            int id = free[--FreeCount];generations[id]++;
            enemies[id] = new Enemy { alive = true, health = health, position = position, destination = destination, speed = speed, air=air };
            if(air){enemies[id].position.y+=5;enemies[id].destination.y+=5;}
            Alive++; Spawned++; SpawnedEnemy?.Invoke(id);return true;
        }
        public void Reset()
        {
            Health = MaxHealth;
            random = new System.Random(731);
            DespawnAll();
            Killed = Escaped = Spawned = 0;
        }
        // Ending a completed map is neither a kill nor an escape. Preserve run
        // counters and health, and do not emit rewards or defeat events.
        public void DespawnAll()
        {
            Array.Clear(enemies, 0, enemies.Length);
            Array.Fill(heads, -1);
            for (int i = 0; i < MaxCount; i++) free[i] = MaxCount - 1 - i;
            FreeCount = MaxCount;
            Alive = 0;
        }
        public void Spawn(HordeMapKind mapKind)
        {
            if (FreeCount == 0 || Defeated) return;
            HordeMapLayout.SpawnRoute(mapKind, random, out var start, out var end);
            int id = free[--FreeCount];generations[id]++;
            enemies[id] = new Enemy
            {
                alive = true,
                health = 20,
                position = start,
                destination = end,
                speed = (mapKind == HordeMapKind.Lane ? 1.7f : 3.8f) + (float)random.NextDouble() * 0.8f
            };
            Alive++;
            Spawned++;SpawnedEnemy?.Invoke(id);
        }

        public void MoveAndIndex(float dt)
        {
            Array.Fill(heads, -1);
            for (int i = 0; i < MaxCount; i++)
            {
                if (Defeated) break;
                if (!enemies[i].alive) continue;
                float slowedSeconds = Mathf.Min(dt, enemies[i].slowTime);
                enemies[i].slowTime = Mathf.Max(0, enemies[i].slowTime - dt);
                float movementSeconds = dt - slowedSeconds * 0.6f;
                bool moved=false;
                if(!enemies[i].waitingForDestination) {
                    var from=enemies[i].position;var proposed=Vector3.MoveTowards(from,enemies[i].destination,enemies[i].speed*movementSeconds*(1-enemies[i].movementPenalty));
                    enemies[i].position=MovementConstraint!=null?MovementConstraint.Constrain(i,from,proposed):proposed;
                    moved=from.x!=enemies[i].position.x||from.z!=enemies[i].position.z;
                }
                if (groundHeight != null && (ArrivalPolicy==EnemyArrivalPolicy.Remove||moved)) enemies[i].position.y = groundHeight(enemies[i].position)+(enemies[i].air?5:0);
                if (ArrivalPolicy==EnemyArrivalPolicy.Remove && !enemies[i].waitingForDestination && (enemies[i].position - enemies[i].destination).sqrMagnitude < 0.001f)
                {
                    Remove(i, false);
                    continue;
                }
                int cell = Cell(enemies[i].position);
                next[i] = heads[cell];
                heads[cell] = i;
            }
        }

        internal int CellX(float x) => Mathf.Clamp(Mathf.FloorToInt((x-indexOrigin.x)/CellSize),0,IndexWidth-1);
        internal int CellZ(float z) => Mathf.Clamp(Mathf.FloorToInt((z-indexOrigin.y)/CellSize),0,IndexHeight-1);
        int Cell(Vector3 position) => CellZ(position.z)*IndexWidth+CellX(position.x);

        public void Remove(int id, bool killed)
        {
            if (!enemies[id].alive) return;
            enemies[id].alive = false;
            free[FreeCount++] = id;
            Alive--;
            if (killed) { Killed++;KilledEnemy?.Invoke(id,generations[id]); }
            else
            {
                Escaped++;
                if (damageOnEscape) Health = Mathf.Max(0, Health - 1);
                if (Defeated)
                {
                    Defeat?.Invoke();
                }
            }
        }

        public void ApplyDamage(int id, int damage)
        {
            if (!enemies[id].alive || damage <= 0) return;
            enemies[id].health -= damage;
            if (enemies[id].health <= 0) Remove(id, true);
        }
    }
}

public static class VerifyEnemyOptimizationParity {
 public static string Main(){var a=new ReferenceEnemyWorld(null,false,257);var b=new HordeEnemyWorld(null,false,257);var random=new System.Random(8137);int operations=0;var ah=new System.Collections.Generic.List<int>();var bh=new System.Collections.Generic.List<int>();
 for(int step=0;step<3000;step++){
  int id=random.Next(257);switch(step%7){
   case 0:case 1:case 2:var p=new Vector3(random.Next(-40,40),0,random.Next(-30,30));var target=p+new Vector3(random.Next(-5,6),0,random.Next(-5,6));float speed=random.Next(0,6);bool air=step%2==0;if(a.TrySpawn(p,target,speed,30,air)!=b.TrySpawn(p,target,speed,30,air))throw new Exception("Spawn mismatch");break;
   case 3:a.ApplyDamage(id,40);b.ApplyDamage(id,40);break;
   case 4:a.SetDestination(id,null);b.SetDestination(id,null);break;
   case 5:a.Remove(id,false);b.Remove(id,false);break;
   case 6:a.MoveAndIndex(.16f);b.MoveAndIndex(.16f);break;
  }
  if(a.Alive!=b.Alive||a.FreeCount!=b.FreeCount||a.Killed!=b.Killed||a.Escaped!=b.Escaped)throw new Exception("Counter mismatch");
  for(int i=0;i<257;i++){var x=a.GetEnemy(i);var y=b.GetEnemy(i);if(x.alive!=y.alive||x.health!=y.health||x.position!=y.position||x.destination!=y.destination||x.slowTime!=y.slowTime||a.Generation(i)!=b.Generation(i))throw new Exception("Slot mismatch "+i);}
  if(step%7==6){var origin=new Vector3(random.Next(-40,40),0,random.Next(-30,30));a.QueryIndices(origin,8,ah);b.QueryIndices(origin,8,bh);if(!ah.SequenceEqual(bh))throw new Exception("Query or tie order mismatch");}
  if(step==1499){a.DespawnAll();b.DespawnAll();}operations++;
 }
 var result=new JObject{{"result","PASS"},{"operations",operations},{"capacity",257},{"slotStateAndGenerationsIdentical",true},{"spatialQueryOrderIdentical",true},{"seed",8137},{"purpose","Product behavior regression; no research experiment"}};File.WriteAllText("Docs/Measurements/2026-10-03-performance/enemy-parity.json",result.ToString());return result.ToString();}
}
