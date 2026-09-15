using System;
using UnityEngine;

namespace EternalSteam.Demo
{
    public sealed class HordeAttackResolver
    {
        const int Capacity = HordeEnemyWorld.Capacity;
        const int GridWidth = HordeEnemyWorld.GridWidth, GridHeight = HordeEnemyWorld.GridHeight;
        const float CellSize = HordeEnemyWorld.CellSize;
        public const float ShotHalfWidth = 0.3f;
        readonly HordeEnemyWorld world;
        readonly HordeEnemyWorld.Enemy[] enemies;
        readonly int[] heads, next;
        public HordeAttackResolver(HordeEnemyWorld world)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            enemies = world.enemies; heads = world.heads; next = world.next;
        }
        void ApplyDamage(int id, int damage) => world.ApplyDamage(id, damage);

        public int FindTarget(Vector3 origin, Vector3 forward, float range)
        {
            Vector3 end = origin + forward * range;
            int minX = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(origin.x, end.x) - ShotHalfWidth + 44) / CellSize), 0, GridWidth - 1);
            int maxX = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(origin.x, end.x) + ShotHalfWidth + 44) / CellSize), 0, GridWidth - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(origin.z, end.z) - ShotHalfWidth + 34) / CellSize), 0, GridHeight - 1);
            int maxZ = Mathf.Clamp(Mathf.FloorToInt((Mathf.Max(origin.z, end.z) + ShotHalfWidth + 34) / CellSize), 0, GridHeight - 1);
            float best = range;
            int target = -1;
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    for (int i = heads[z * GridWidth + x]; i >= 0; i = next[i])
                    {
                        if (!enemies[i].alive) continue;
                        Vector3 delta = enemies[i].position - origin;
                        delta.y = 0;
                        float along = Vector3.Dot(delta, forward);
                        float lateral = Mathf.Abs(delta.x * forward.z - delta.z * forward.x);
                        if (along < 1.2f || along >= best || lateral > ShotHalfWidth || delta.sqrMagnitude > range * range) continue;
                        best = along;
                        target = i;
                    }
            return target;
        }

        public void ApplyPiercingShot(Vector3 origin, Vector3 direction, float range)
        {
            // A single ray per shot keeps the prototype cheap and cannot hit a slot twice.
            for (int i = 0; i < Capacity; i++)
            {
                if (!enemies[i].alive) continue;
                Vector3 delta = enemies[i].position - origin;
                delta.y = 0;
                float along = Vector3.Dot(delta, direction);
                float lateral = Mathf.Abs(delta.x * direction.z - delta.z * direction.x);
                if (along >= 1.2f && along <= range && delta.sqrMagnitude <= range * range && lateral <= ShotHalfWidth)
                    ApplyDamage(i, HordeTowerStats.Damage(HordeTowerKind.Arrow));
            }
        }

        public void ApplyFrostCone(Vector3 origin, Vector3 direction, float range, float halfAngle)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt((origin.x - range + 44) / CellSize), 0, GridWidth - 1);
            int maxX = Mathf.Clamp(Mathf.FloorToInt((origin.x + range + 44) / CellSize), 0, GridWidth - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt((origin.z - range + 34) / CellSize), 0, GridHeight - 1);
            int maxZ = Mathf.Clamp(Mathf.FloorToInt((origin.z + range + 34) / CellSize), 0, GridHeight - 1);
            float cosine = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    for (int i = heads[z * GridWidth + x]; i >= 0; i = next[i])
                    {
                        if (!enemies[i].alive) continue;
                        Vector3 delta = enemies[i].position - origin;
                        delta.y = 0;
                        float squareDistance = delta.sqrMagnitude;
                        if (squareDistance > range * range || Vector3.Dot(delta, direction) < Mathf.Sqrt(squareDistance) * cosine) continue;
                        enemies[i].slowTime = 2.5f;
                    }
        }

        public void ApplyArea(Vector3 center, float radius)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt((center.x - radius + 44) / CellSize), 0, GridWidth - 1);
            int maxX = Mathf.Clamp(Mathf.FloorToInt((center.x + radius + 44) / CellSize), 0, GridWidth - 1);
            int minZ = Mathf.Clamp(Mathf.FloorToInt((center.z - radius + 34) / CellSize), 0, GridHeight - 1);
            int maxZ = Mathf.Clamp(Mathf.FloorToInt((center.z + radius + 34) / CellSize), 0, GridHeight - 1);
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    for (int i = heads[z * GridWidth + x]; i >= 0; i = next[i])
                    {
                        if (!enemies[i].alive) continue;
                        var delta = enemies[i].position - center;
                        delta.y = 0;
                        if (delta.sqrMagnitude > radius * radius) continue;
                        ApplyDamage(i, HordeTowerStats.Damage(HordeTowerKind.Cannon));
                    }
        }

    }
}
