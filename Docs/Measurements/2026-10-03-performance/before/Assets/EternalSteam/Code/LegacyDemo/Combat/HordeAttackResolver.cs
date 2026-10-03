using System;
using UnityEngine;

namespace EternalSteam.Demo
{
    public sealed class HordeAttackResolver
    {
        int GridWidth => world.IndexWidth;
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
            int minX = world.CellX(Mathf.Min(origin.x, end.x) - ShotHalfWidth);
            int maxX = world.CellX(Mathf.Max(origin.x, end.x) + ShotHalfWidth);
            int minZ = world.CellZ(Mathf.Min(origin.z, end.z) - ShotHalfWidth);
            int maxZ = world.CellZ(Mathf.Max(origin.z, end.z) + ShotHalfWidth);
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

        public void ApplyPiercingShot(Vector3 origin, Vector3 direction, float range,float minimumDistance=1.2f)
        {
            // A single ray per shot keeps the prototype cheap and cannot hit a slot twice.
            for (int i = 0; i < world.MaxCount; i++)
            {
                if (!enemies[i].alive) continue;
                Vector3 delta = enemies[i].position - origin;
                delta.y = 0;
                float along = Vector3.Dot(delta, direction);
                float lateral = Mathf.Abs(delta.x * direction.z - delta.z * direction.x);
                if (along >= minimumDistance && along <= range && delta.sqrMagnitude <= range * range && lateral <= ShotHalfWidth)
                    ApplyDamage(i, HordeTowerStats.Damage(HordeTowerKind.Arrow));
            }
        }

        public void ApplyFrostCone(Vector3 origin, Vector3 direction, float range, float halfAngle)
        {
            int minX = world.CellX(origin.x - range);
            int maxX = world.CellX(origin.x + range);
            int minZ = world.CellZ(origin.z - range);
            int maxZ = world.CellZ(origin.z + range);
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
            int minX = world.CellX(center.x - radius);
            int maxX = world.CellX(center.x + radius);
            int minZ = world.CellZ(center.z - radius);
            int maxZ = world.CellZ(center.z + radius);
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
