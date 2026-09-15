using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace EternalSteam.Demo
{
    public sealed class HordeEnemyRenderer : IDisposable
    {
        const int Capacity = HordeEnemyWorld.Capacity;
        readonly HordeEnemyWorld world;
        readonly Bounds bounds;
        readonly Mesh enemyMesh;
        readonly Material enemyMaterial, slowedMaterial;
        readonly Matrix4x4[] matrices = new Matrix4x4[500];
        public HordeEnemyRenderer(HordeEnemyWorld world, Mesh mesh, Material material, Bounds? bounds = null)
        {
            this.bounds = bounds ?? new Bounds(Vector3.zero, new Vector3(90, 5, 68));
            this.world = world; enemyMesh = mesh; enemyMaterial = material;
            slowedMaterial = new Material(material);
            slowedMaterial.SetColor("_BaseColor", new Color(0.35f, 0.7f, 1));
        }
        public void Dispose() { if (slowedMaterial != null) UnityEngine.Object.Destroy(slowedMaterial); }
        public void Draw()
        {
            DrawEnemyGroup(false);
            DrawEnemyGroup(true);
        }

        void DrawEnemyGroup(bool slowed)
        {
            var rp = new RenderParams(slowed ? slowedMaterial : enemyMaterial)
            {
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = bounds
            };
            int count = 0;
            for (int i = 0; i < Capacity; i++)
            {
                if (!world.GetEnemy(i).alive || (world.GetEnemy(i).slowTime > 0) != slowed) continue;
                matrices[count++] = Matrix4x4.TRS(world.GetEnemy(i).position, Quaternion.identity, new Vector3(0.38f, 0.52f, 0.38f));
                if (count != matrices.Length) continue;
                Graphics.RenderMeshInstanced(rp, enemyMesh, 0, matrices, count);
                count = 0;
            }
            if (count > 0) Graphics.RenderMeshInstanced(rp, enemyMesh, 0, matrices, count);
        }
    }
}
