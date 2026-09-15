using System;
using UnityEngine;

namespace EternalSteam.Demo
{
    public sealed class HordeEnemyWorld
    {
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
            public int health;
        }
        internal readonly Enemy[] enemies = new Enemy[Capacity];
        internal readonly int[] heads = new int[GridWidth * GridHeight];
        internal readonly int[] next = new int[Capacity];
        readonly int[] free = new int[Capacity];
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
        public ref readonly Enemy GetEnemy(int index) => ref enemies[index];
        public HordeEnemyWorld(Func<Vector3, float> groundHeight = null, bool damageOnEscape = true)
        { this.groundHeight = groundHeight; this.damageOnEscape = damageOnEscape; Reset(); }
        public bool TrySpawn(Vector3 position, Vector3 destination, float speed = 2, int health = 20)
        {
            if (FreeCount == 0 || Defeated || health <= 0 || !float.IsFinite(speed) || speed < 0) return false;
            if (!float.IsFinite(position.sqrMagnitude) || !float.IsFinite(destination.sqrMagnitude)) return false;
            if (groundHeight != null) { position.y = groundHeight(position); destination.y = groundHeight(destination); }
            int id = free[--FreeCount];
            enemies[id] = new Enemy { alive = true, health = health, position = position, destination = destination, speed = speed };
            Alive++; Spawned++; return true;
        }
        public void Reset()
        {
            Health = MaxHealth;
            random = new System.Random(731);
            Array.Clear(enemies, 0, enemies.Length);
            Array.Fill(heads, -1);
            for (int i = 0; i < Capacity; i++) free[i] = Capacity - 1 - i;
            FreeCount = Capacity;
            Alive = Killed = Escaped = Spawned = 0;
        }
        public void Spawn(HordeMapKind mapKind)
        {
            if (FreeCount == 0 || Defeated) return;
            HordeMapLayout.SpawnRoute(mapKind, random, out var start, out var end);
            int id = free[--FreeCount];
            enemies[id] = new Enemy
            {
                alive = true,
                health = 20,
                position = start,
                destination = end,
                speed = (mapKind == HordeMapKind.Lane ? 1.7f : 3.8f) + (float)random.NextDouble() * 0.8f
            };
            Alive++;
            Spawned++;
        }

        public void MoveAndIndex(float dt)
        {
            Array.Fill(heads, -1);
            for (int i = 0; i < Capacity; i++)
            {
                if (Defeated) break;
                if (!enemies[i].alive) continue;
                float slowedSeconds = Mathf.Min(dt, enemies[i].slowTime);
                enemies[i].slowTime = Mathf.Max(0, enemies[i].slowTime - dt);
                float movementSeconds = dt - slowedSeconds * 0.6f;
                enemies[i].position = Vector3.MoveTowards(enemies[i].position, enemies[i].destination, enemies[i].speed * movementSeconds);
                if (groundHeight != null) enemies[i].position.y = groundHeight(enemies[i].position);
                if ((enemies[i].position - enemies[i].destination).sqrMagnitude < 0.001f)
                {
                    Remove(i, false);
                    continue;
                }
                int cell = Cell(enemies[i].position);
                next[i] = heads[cell];
                heads[cell] = i;
            }
        }

        static int Cell(Vector3 position)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((position.x + 44) / CellSize), 0, GridWidth - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((position.z + 34) / CellSize), 0, GridHeight - 1);
            return z * GridWidth + x;
        }

        public void Remove(int id, bool killed)
        {
            if (!enemies[id].alive) return;
            enemies[id].alive = false;
            free[FreeCount++] = id;
            Alive--;
            if (killed) Killed++;
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
