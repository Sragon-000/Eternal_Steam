using System;
using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam.Demo
{
    public interface IEnemyMovementConstraint { Vector3 Constrain(int id,Vector3 from,Vector3 proposed); }
    public enum EnemyArrivalPolicy { Remove, Remain }
    public sealed class HordeEnemyWorld
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
        public HordeEnemyWorld(Func<Vector3, float> groundHeight = null, bool damageOnEscape = true, int capacity = Capacity, Rect? indexBounds = null)
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
            Array.Clear(enemies, 0, enemies.Length);
            Array.Fill(heads, -1);
            for (int i = 0; i < MaxCount; i++) free[i] = MaxCount - 1 - i;
            FreeCount = MaxCount;
            Alive = Killed = Escaped = Spawned = 0;
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
