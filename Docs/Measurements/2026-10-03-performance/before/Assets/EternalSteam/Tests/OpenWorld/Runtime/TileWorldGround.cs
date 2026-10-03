using System;
using UnityEngine;

namespace EternalSteam.OpenWorld
{
    // TWC owns the visible meshes. Terrain is an invisible height cache for the existing bulk simulation.
    public sealed class TileWorldGround : MonoBehaviour
    {
        public Transform GridRoot;
        public int Width, Height;
        public float CellSize = 2;
        public LayerMask SurfaceMask;
        public byte[] PlayableCells;
        TerrainFlowNavigation navigation;
        // Derived terrain paths are shared by every spawn query and enemy in this map.
        public TerrainFlowNavigation Navigation
        {
            get {var snapshot=CaptureTraversal();if(navigation==null)navigation=new TerrainFlowNavigation(snapshot);else navigation.Update(snapshot);return navigation;}
        }

        // Capture Unity Transform data once per simulation step; queries below use only managed value data.
        public TraversalSnapshot CaptureTraversal()=>new TraversalSnapshot(GridRoot!=null?GridRoot.worldToLocalMatrix:Matrix4x4.identity,Width,Height,CellSize,GridRoot!=null?PlayableCells:null);
        public readonly struct TraversalSnapshot
        {
            readonly Matrix4x4 inverse,forward;readonly int width,height;readonly float cellSize;readonly byte[] cells;
            public TraversalSnapshot(Matrix4x4 inverse,int width,int height,float cellSize,byte[] cells){this.inverse=inverse;forward=inverse.inverse;this.width=width;this.height=height;this.cellSize=cellSize;this.cells=cells;}
            public bool Valid=>width>0&&height>0&&float.IsFinite(cellSize)&&cellSize>0&&cells!=null&&cells.LongLength==(long)width*height;
            public int Width=>width;
            public int Count=>Valid?cells.Length:0;
            public bool SameGrid(TraversalSnapshot other)=>inverse.Equals(other.inverse)&&width==other.width&&height==other.height&&cellSize.Equals(other.cellSize)&&ReferenceEquals(cells,other.cells);
            public bool Walkable(int index)=>Valid&&index>=0&&index<cells.Length&&cells[index]!=0;
            public bool TryIndex(Vector3 point,out int index){index=-1;if(!Valid||!Cell(point,out int x,out int z))return false;index=z*width+x;return true;}
            public Vector3 Center(int index)=>forward.MultiplyPoint3x4(new Vector3(index%width*cellSize,0,index/width*cellSize));
            bool WalkableCell(int x,int z)=>x>=0&&z>=0&&x<width&&z<height&&cells[z*width+x]!=0;
            bool Cell(Vector3 point,out int x,out int z){var p=inverse.MultiplyPoint3x4(point);x=Mathf.FloorToInt(p.x/cellSize+.5f);z=Mathf.FloorToInt(p.z/cellSize+.5f);return float.IsFinite(p.x)&&float.IsFinite(p.z)&&WalkableCell(x,z);}
            public bool HasClearRoute(Vector3 from,Vector3 to){
                if(!Valid)return false;
                if(!Cell(from,out int x,out int z)||!Cell(to,out int endX,out int endZ))return false;
                if(x==endX&&z==endZ)return true;
                // Visit every crossed cell, including both sides of an exact corner.
                var a=inverse.MultiplyPoint3x4(from)/cellSize;var b=inverse.MultiplyPoint3x4(to)/cellSize;
                float dx=b.x-a.x,dz=b.z-a.z;int sx=Math.Sign(dx),sz=Math.Sign(dz);
                float tx=sx==0?float.PositiveInfinity:(x+sx*.5f-a.x)/dx;
                float tz=sz==0?float.PositiveInfinity:(z+sz*.5f-a.z)/dz;
                float stepX=sx==0?float.PositiveInfinity:1/Mathf.Abs(dx),stepZ=sz==0?float.PositiveInfinity:1/Mathf.Abs(dz);
                while(x!=endX||z!=endZ){
                    if(Mathf.Abs(tx-tz)<.000001f){
                        if(!WalkableCell(x+sx,z)||!WalkableCell(x,z+sz))return false;
                        x+=sx;z+=sz;tx+=stepX;tz+=stepZ;
                    }else if(tx<tz){x+=sx;tx+=stepX;}
                    else {z+=sz;tz+=stepZ;}
                    if(!WalkableCell(x,z))return false;
                }
                return true;
            }
        }

        public bool IsPlayable(Vector3 world)
        {
            if (GridRoot == null || CellSize <= 0) return false;
            var p = GridRoot.InverseTransformPoint(world);
            int x = Mathf.FloorToInt(p.x / CellSize + .5f), z = Mathf.FloorToInt(p.z / CellSize + .5f);
            return x >= 0 && z >= 0 && x < Width && z < Height &&
                PlayableCells != null && PlayableCells.Length == Width * Height && PlayableCells[z * Width + x] != 0;
        }

        public bool TrySurface(Vector3 point, out float height)
        {
            height = 0;
            if (!Physics.Raycast(new Vector3(point.x, 200, point.z), Vector3.down, out var hit, 400, SurfaceMask, QueryTriggerInteraction.Ignore)) return false;
            height = hit.point.y;
            return true;
        }

        public bool CheckFoundation(Vector3 point, out Vector3 center, out string reason)
        {
            center = WorldGridGeometry.Center(WorldGridGeometry.Cell(point, 8), 8);
            float low = float.MaxValue, high = float.MinValue;
            // Test the whole footprint at half-cell intervals, including its boundary.
            for (int z = 0; z <= 8; z++) for (int x = 0; x <= 8; x++)
            {
                var p = center + WorldGridGeometry.ToWorld(new Vector3(x - 4, 0, z - 4));
                if (!IsPlayable(p)) { reason = "강·산지·맵 경계에는 토대를 설치할 수 없습니다."; return false; }
                if (!TrySurface(p, out float h)) { reason = "토대 아래에 지면이 없습니다."; return false; }
                low = Mathf.Min(low, h); high = Mathf.Max(high, h);
            }
            center.y = high + .35f;
            if (high - low > 1.2f) { reason = "경사가 큽니다. 평탄한 곳을 선택하세요."; return false; }
            reason = "TileWorldCreator 평지 · 45° 건설 격자";
            return true;
        }

        // Straight visibility; Navigation supplies routes around blocked terrain.
        public bool HasClearRoute(Vector3 from,Vector3 to)=>CaptureTraversal().HasClearRoute(from,to);
    }
}
