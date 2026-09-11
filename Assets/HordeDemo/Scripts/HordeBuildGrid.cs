using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace EternalSteam.Demo
{
    public sealed class HordeBuildGrid : MonoBehaviour
    {
        Mesh mesh;
        Material material;

        public void Configure(HordeMapKind kind, Material template)
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
            material = new Material(template) { name = "Deployment grid" };
            material.SetColor("_BaseColor", new Color(0.22f, 0.52f, 0.58f));
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            float y = HordeMapLayout.SurfaceHeight(kind) + 0.025f;
            foreach (Rect zone in HordeMapLayout.BuildZones(kind))
            {
                var area = HordeMapLayout.GridBounds(zone);
                for (float x = area.xMin; x <= area.xMax + 0.01f; x += HordeMapLayout.BuildCellSize)
                    Strip(vertices, indices, new Vector3(x, y, area.yMin), new Vector3(x, y, area.yMax), area);
                for (float z = area.yMin; z <= area.yMax + 0.01f; z += HordeMapLayout.BuildCellSize)
                    Strip(vertices, indices, new Vector3(area.xMin, y, z), new Vector3(area.xMax, y, z), area);
            }
            mesh = new Mesh { name = "Deployment grid lines" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            var renderer = GetComponent<MeshRenderer>();
            if (renderer == null) renderer = gameObject.AddComponent<MeshRenderer>();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void Strip(List<Vector3> vertices, List<int> triangles, Vector3 start, Vector3 end, Rect area)
        {
            Vector3 side = Vector3.Cross(Vector3.up, (end - start).normalized) * 0.055f;
            int i = vertices.Count;
            vertices.Add(start - side);
            vertices.Add(start + side);
            vertices.Add(end + side);
            vertices.Add(end - side);
            for (int n = i; n < i + 4; n++)
            {
                var v = vertices[n];
                v.x = Mathf.Clamp(v.x, area.xMin, area.xMax);
                v.z = Mathf.Clamp(v.z, area.yMin, area.yMax);
                vertices[n] = v;
            }
            triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
            triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2);
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
