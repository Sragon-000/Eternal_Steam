using System;using System.IO;using System.Linq;using System.Collections;using System.Reflection;using Newtonsoft.Json.Linq;using EternalSteam.OpenWorld;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // Nine reusable meshes, centred on the camera. No per-cell objects or full-world allocation.
    public sealed class ReferenceWorldGrid:MonoBehaviour
    {
        const int Cells=64;
        const float CellSize=2,Span=Cells*CellSize;
        readonly Mesh[] meshes=new Mesh[9];
        [SerializeField] MeshRenderer[] renderers=new MeshRenderer[9];
        [SerializeField] MeshFilter[] filters=new MeshFilter[9];
        bool initialized;
        public bool Visible {get;private set;}
        public void SetVisible(bool visible) {if(!initialized||Visible==visible)return;Visible=visible;foreach(var renderer in renderers)if(renderer!=null)renderer.enabled=visible;}
        readonly Vector2Int[] keys=new Vector2Int[9];
        readonly List<Vector3> vertices=new();readonly List<int> indices=new();
        FreeCameraRig cameraRig;Terrain terrain;TileWorldGround tiles;
        Vector2Int current=new(int.MinValue,int.MinValue);
        public int RebuildCount {get;private set;}
        public void Initialize(FreeCameraRig camera,Terrain ground) {
            if(initialized)return;
            if(renderers.Length!=9||filters.Length!=9)throw new System.InvalidOperationException("Author nine grid chunks in the scene.");
            cameraRig=camera;terrain=ground;ground.TryGetComponent(out tiles);
            for(int i=0;i<9;i++) {
                if(filters[i]==null||renderers[i]==null)throw new System.InvalidOperationException("Grid chunk reference missing in scene.");
                var mesh=new Mesh{name="Construction grid chunk"};mesh.MarkDynamic();meshes[i]=mesh;
                filters[i].sharedMesh=mesh;renderers[i].enabled=false;keys[i]=new Vector2Int(int.MinValue,int.MinValue);
            }
            initialized=true;
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
                if(tiles!=null&&!tiles.IsPlayable(mid))return;
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

public static class VerifyPerformanceGrid {
 public static string Main(){if(!Application.isPlaying)throw new Exception("Play required");new GameObject("Grid geometry parity probe").AddComponent<PerformanceGridProbe>();return "Queued full grid parity and camera cancellation checks";}
}
public sealed class PerformanceGridProbe:MonoBehaviour {
 const string Root="Docs/Measurements/2026-10-03-performance/";
 IEnumerator Start(){var it=Run();while(true){bool more;try{more=it.MoveNext();}catch(Exception e){File.WriteAllText(Root+"grid-result.json",new JObject{{"result","FAIL"},{"error",e.ToString()}}.ToString());break;}if(!more)break;yield return it.Current;}Destroy(gameObject);}
 IEnumerator Run(){var h=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();var s=h.Sandbox;h.Groups.Select(true);yield return new WaitForSecondsRealtime(.4f);h.Input.BeginEditing();yield return new WaitForEndOfFrame();int frames=0;var work=new System.Collections.Generic.List<double>();
 // Input owns visibility in normal Play. Wait for all authored mesh slots to complete.
 while(s.WorldGrid.PendingChunkCount>0&&frames++<300)yield return null;
 if(s.WorldGrid.PendingChunkCount!=0)throw new Exception("Grid did not complete");
 var filters=(MeshFilter[])typeof(WorldGridView).GetField("filters",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s.WorldGrid);var renderers=(MeshRenderer[])typeof(WorldGridView).GetField("renderers",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s.WorldGrid);var actual=filters.Select(f=>f.sharedMesh).ToArray();bool parity=true;double referenceMs=0;var baseline=s.WorldGrid.gameObject.AddComponent<ReferenceWorldGrid>();
 try{typeof(ReferenceWorldGrid).GetField("filters",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(baseline,filters);typeof(ReferenceWorldGrid).GetField("renderers",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(baseline,renderers);baseline.Initialize(s.CameraRig,s.Ground);baseline.SetVisible(true);var call=(Action)Delegate.CreateDelegate(typeof(Action),baseline,typeof(ReferenceWorldGrid).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic));var timer=System.Diagnostics.Stopwatch.StartNew();call();timer.Stop();referenceMs=timer.Elapsed.TotalMilliseconds;for(int i=0;i<filters.Length;i++){var expected=filters[i].sharedMesh;parity&=actual[i].vertices.SequenceEqual(expected.vertices)&&actual[i].triangles.SequenceEqual(expected.triangles);}}
 finally{DestroyImmediate(baseline);for(int i=0;i<filters.Length;i++){filters[i].sharedMesh=actual[i];renderers[i].enabled=true;}}
 if(!parity)throw new Exception("Published grid differs from reference vertices/triangles");
 var original=s.CameraRig.Focus;var late=(Action)Delegate.CreateDelegate(typeof(Action),s.WorldGrid,typeof(WorldGridView).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic));s.CameraRig.Focus=original+WorldGridGeometry.ToWorld(new Vector3(400,0,0));late();s.CameraRig.Focus=original+WorldGridGeometry.ToWorld(new Vector3(0,0,400));late();s.CameraRig.Focus=original;frames=0;do{var timer=System.Diagnostics.Stopwatch.StartNew();late();timer.Stop();work.Add(timer.Elapsed.TotalMilliseconds);frames++;yield return null;}while(s.WorldGrid.PendingChunkCount>0&&frames<300);
 if(s.WorldGrid.PendingChunkCount!=0)throw new Exception("Interrupted grid never completed");h.Input.Cancel();h.Groups.Select(false);File.WriteAllText(Root+"grid-result.json",new JObject{{"result","PASS"},{"exactVertexAndTriangleParity",parity},{"originalSynchronousMs",referenceMs},{"cameraMoveInterruptRecovered",true},{"boundedCalls",work.Count},{"maxCallMs",work.Max()},{"meshCount",actual.Length}}.ToString());}
}
