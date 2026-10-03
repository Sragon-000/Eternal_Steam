using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // One static mesh per confirmed nexus; the mesh has no colliders or placement authority.
    public sealed class NexusGridView:MonoBehaviour
    {
        Mesh mesh;
        public static void Create(Transform parent,Vector3 center,float radius,Material material,Terrain terrain)
        {
            var go=new GameObject("Nexus 2m construction grid");go.transform.SetParent(parent,false);
            var view=go.AddComponent<NexusGridView>();var vertices=new List<Vector3>();var indices=new List<int>();
            Vector3 Point(float x,float z){var p=center+WorldGridGeometry.ToWorld(new Vector3(x,0,z));p.y=(terrain!=null?terrain.SampleHeight(p)+terrain.transform.position.y:center.y)+.075f;return go.transform.InverseTransformPoint(p);}
            void Segment(Vector2 a,Vector2 b){var tangent=(b-a).normalized;var side=new Vector2(-tangent.y,tangent.x)*.018f;int n=vertices.Count;vertices.Add(Point(a.x+side.x,a.y+side.y));vertices.Add(Point(a.x-side.x,a.y-side.y));vertices.Add(Point(b.x+side.x,b.y+side.y));vertices.Add(Point(b.x-side.x,b.y-side.y));indices.AddRange(new[]{n,n+2,n+1,n+1,n+2,n+3});}
            // Clip each line to the circle, subdivide to follow terrain heights.
            for(int axis=0;axis<2;axis++)for(float offset=Mathf.Ceil(-radius/2)*2;offset<=radius;offset+=2){float extent=Mathf.Sqrt(Mathf.Max(0,radius*radius-offset*offset));for(float t=-extent;t<extent;t+=2){float end=Mathf.Min(t+2,extent);if(axis==0)Segment(new Vector2(offset,t),new Vector2(offset,end));else Segment(new Vector2(t,offset),new Vector2(end,offset));}}
            view.mesh=new Mesh{name="Nexus range grid",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};view.mesh.SetVertices(vertices);view.mesh.SetTriangles(indices,0);view.mesh.RecalculateNormals();view.mesh.RecalculateBounds();go.AddComponent<MeshFilter>().sharedMesh=view.mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);}
    }
}
