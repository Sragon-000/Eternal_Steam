using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace EternalSteam.OpenWorld
{
    // Matches the neutral grid's nine reusable 128m chunks. No per-cell objects/colliders.
    public sealed class BuildAreaHologramView:MonoBehaviour
    {
        const int Cells=64,Stride=Cells+2;
        const float CellSize=2,Span=Cells*CellSize;
        readonly Mesh[] meshes=new Mesh[9];
        [SerializeField] MeshRenderer[] renderers=new MeshRenderer[9];
        [SerializeField] MeshFilter[] filters=new MeshFilter[9];
        bool initialized;
        readonly Vector2Int[] keys=new Vector2Int[9];
        readonly bool[][] coverage=new bool[9][];
        readonly int[] counts=new int[9];
        readonly bool[] scratch=new bool[Stride*Stride];
        readonly List<Vector3> vertices=new();
        readonly List<Color> colors=new();
        readonly List<int> indices=new();
        static readonly Color Fill=new(.08f,.55f,1,.16f),Grid=new(.15f,.7f,1,.3f),Edge=new(.25f,.85f,1,.7f),Blocked=new(1,.2f,.15f,.65f);
        FreeCameraRig cameraRig;Terrain terrain;TileWorldGround tiles;OpenWorldContent content;
        Vector2Int current=new(int.MinValue,int.MinValue);
        int revision=-1;bool surfaceDirty;
        public bool Visible {get;private set;}
        public int RebuildCount {get;private set;}
        public int VisibleCoveredCellCount {get {int total=0;foreach(int count in counts)total+=count;return total;}}
        public void Initialize(FreeCameraRig camera,Terrain ground,OpenWorldContent world)
        {
            if(initialized)return;
            if(renderers.Length!=9||filters.Length!=9)throw new System.InvalidOperationException("Author nine area chunks in the scene.");
            cameraRig=camera;terrain=ground;content=world;ground.TryGetComponent(out tiles);
            for(int i=0;i<9;i++) {
                if(filters[i]==null||renderers[i]==null||renderers[i].sharedMaterial==null)throw new System.InvalidOperationException("Area chunk or material reference missing in scene.");
                keys[i]=new Vector2Int(int.MinValue,int.MinValue);coverage[i]=new bool[Stride*Stride];
                var mesh=new Mesh{name="Operational area chunk",indexFormat=IndexFormat.UInt32};mesh.MarkDynamic();meshes[i]=mesh;
                filters[i].sharedMesh=mesh;renderers[i].enabled=false;
            }
            initialized=true;
        }
        public void SetVisible(bool visible)
        {
            if(!initialized||Visible==visible)return;Visible=visible;
            // Reconcile keys/coverage before enabling stale meshes after a hidden camera move.
            if(visible)RefreshView();
            foreach(var renderer in renderers)if(renderer!=null)renderer.enabled=visible;
        }
        public void InvalidateSurface()=>surfaceDirty=true;
        void LateUpdate(){if(Visible)RefreshView();}
        void RefreshView()
        {
            content.Bases.RefreshCoverage();
            var center=WorldGridGeometry.Cell(cameraRig.Focus,Span);int next=content.Bases.CoverageRevision;
            if(center==current&&revision==next&&!surfaceDirty)return;
            bool areaChanged=revision!=next;current=center;revision=next;
            for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++) {
                var key=center+new Vector2Int(x,z);int slot=((key.x%3+3)%3)+3*((key.y%3+3)%3);
                bool moved=keys[slot]!=key;if(!moved&&!areaChanged&&!surfaceDirty)continue;
                bool changed=moved||surfaceDirty;var mask=coverage[slot];
                // One-cell halo makes shared chunk boundaries obey the same union perimeter.
                for(int j=-1;j<=Cells;j++)for(int i=-1;i<=Cells;i++) {
                    int index=(j+1)*Stride+i+1;var cell=key*Cells+new Vector2Int(i,j);
                    bool covered=content.Bases.Covers(WorldGridGeometry.Center(cell,CellSize),Vector2.one,WorldGridGeometry.Rotation,content.Bases.SelectedBaseId);
                    scratch[index]=covered;changed|=mask[index]!=covered;
                }
                if(!changed)continue;
                System.Array.Copy(scratch,mask,scratch.Length);keys[slot]=key;Rebuild(slot,key,mask);
            }
            surfaceDirty=false;
        }
        Vector3 Point(Vector2 p)
        {
            var world=WorldGridGeometry.ToWorld(new Vector3(p.x,0,p.y));
            world.y=terrain.SampleHeight(world)+terrain.transform.position.y;
            if(tiles!=null&&tiles.TrySurface(world,out float height))world.y=height;
            world.y+=.11f;return transform.InverseTransformPoint(world);
        }
        void Quad(Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {
            int n=vertices.Count;vertices.Add(Point(a));vertices.Add(Point(b));vertices.Add(Point(c));vertices.Add(Point(d));
            for(int i=0;i<4;i++)colors.Add(color);
            indices.Add(n);indices.Add(n+1);indices.Add(n+2);indices.Add(n+2);indices.Add(n+1);indices.Add(n+3);
        }
        void Line(Vector2 a,Vector2 b,Color color,float width)
        {var side=new Vector2(-(b-a).y,(b-a).x).normalized*width;Quad(a+side,a-side,b+side,b-side,color);}
        void Rebuild(int slot,Vector2Int key,bool[] mask)
        {
            vertices.Clear();indices.Clear();colors.Clear();counts[slot]=0;RebuildCount++;
            for(int z=0;z<Cells;z++)for(int x=0;x<Cells;x++) {
                int index=(z+1)*Stride+x+1;if(!mask[index])continue;counts[slot]++;
                var cell=key*Cells+new Vector2Int(x,z);var p=(Vector2)cell*CellSize;
                var a=p;var b=p+new Vector2(2,0);var c=p+new Vector2(0,2);var d=p+Vector2.one*2;
                bool playable=content.CheckTerrain(cell,Vector2Int.one,out _,out _);
                // No blue fill on blocked terrain; red crosshatch carries a second visual cue.
                if(playable)Quad(a,b,c,d,Fill);
                else {Line(a,d,Blocked,.025f);Line(b,c,Blocked,.025f);}
                void Border(Vector2 start,Vector2 end,bool neighbor,bool ownInternal) {
                    if(!neighbor||ownInternal)Line(start,end,!playable?Blocked:neighbor?Grid:Edge,neighbor?.012f:.035f);
                }
                Border(a,b,mask[index-Stride],false);Border(a,c,mask[index-1],false);
                Border(c,d,mask[index+Stride],true);Border(b,d,mask[index+1],true);
            }
            var mesh=meshes[slot];mesh.Clear();mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
        }
        void OnDisable()=>SetVisible(false);
        void OnDestroy(){foreach(var mesh in meshes)if(mesh!=null)Destroy(mesh);}
    }
}
