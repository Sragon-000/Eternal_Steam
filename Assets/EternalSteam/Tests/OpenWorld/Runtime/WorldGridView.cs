using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // Nine reusable meshes, centred on the camera. No per-cell objects or full-world allocation.
    public sealed class WorldGridView:MonoBehaviour
    {
        const int Cells=64;
        const float CellSize=2,Span=Cells*CellSize;
        readonly Mesh[] meshes=new Mesh[9];
        readonly MeshRenderer[] renderers=new MeshRenderer[9];
        public bool Visible {get;private set;}
        public void SetVisible(bool visible) {if(Visible==visible)return;Visible=visible;foreach(var renderer in renderers)if(renderer!=null)renderer.enabled=visible;}
        readonly Vector2Int[] keys=new Vector2Int[9];
        readonly List<Vector3> vertices=new();readonly List<int> indices=new();
        FreeCameraRig cameraRig;Terrain terrain;TileWorldGround tiles;
        Vector2Int current=new(int.MinValue,int.MinValue);
        public int RebuildCount {get;private set;}
        public static WorldGridView Create(Transform parent,FreeCameraRig camera,Terrain ground,Material material) {
            var go=new GameObject("World construction grid (9 chunks)");go.transform.SetParent(parent,false);
            var view=go.AddComponent<WorldGridView>();view.cameraRig=camera;view.terrain=ground;ground.TryGetComponent(out view.tiles);
            for(int i=0;i<9;i++) {
                var chunk=new GameObject("Grid chunk "+i);chunk.transform.SetParent(go.transform,false);
                var mesh=new Mesh{name="Construction grid chunk"};mesh.MarkDynamic();view.meshes[i]=mesh;
                chunk.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=chunk.AddComponent<MeshRenderer>();view.renderers[i]=renderer;renderer.enabled=false;renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
                view.keys[i]=new Vector2Int(int.MinValue,int.MinValue);
            }return view;
        }
        void LateUpdate() {
            if(!Visible)return;
            var center=WorldGridGeometry.Cell(cameraRig.Focus,Span);if(center==current)return;current=center;
            for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++) {
                var key=center+new Vector2Int(x,z);int slot=((key.x%3+3)%3)+3*((key.y%3+3)%3);
                if(keys[slot]==key)continue;keys[slot]=key;Rebuild(meshes[slot],key);
            }
        }
        void Rebuild(Mesh mesh,Vector2Int key) {
            vertices.Clear();indices.Clear();RebuildCount++;
            Vector3 Point(Vector2 p) {
                var world=WorldGridGeometry.ToWorld(new Vector3(p.x,0,p.y));
                world.y=terrain.SampleHeight(world)+terrain.transform.position.y+.08f;
                if(tiles!=null&&tiles.TrySurface(world,out float height))world.y=height+.08f;
                return transform.InverseTransformPoint(world);
            }
            void Segment(Vector2 a,Vector2 b) {
                var origin=terrain.transform.position;var size=terrain.terrainData.size;var mid=WorldGridGeometry.ToWorld(new Vector3((a.x+b.x)*.5f,0,(a.y+b.y)*.5f));
                if(mid.x<origin.x||mid.z<origin.z||mid.x>=origin.x+size.x||mid.z>=origin.z+size.z)return;
                var side=new Vector2(-(b-a).y,(b-a).x).normalized*.015f;int n=vertices.Count;
                vertices.Add(Point(a+side));vertices.Add(Point(a-side));vertices.Add(Point(b+side));vertices.Add(Point(b-side));
                indices.Add(n);indices.Add(n+2);indices.Add(n+1);indices.Add(n+1);indices.Add(n+2);indices.Add(n+3);
            }
            var start=new Vector2(key.x*Span,key.y*Span);
            for(int line=0;line<Cells;line++)for(int segment=0;segment<Cells;segment++) {
                Segment(start+new Vector2(line,segment)*CellSize,start+new Vector2(line,segment+1)*CellSize);
                Segment(start+new Vector2(segment,line)*CellSize,start+new Vector2(segment+1,line)*CellSize);
            }
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        }
        void OnDestroy(){foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);}
    }
}
