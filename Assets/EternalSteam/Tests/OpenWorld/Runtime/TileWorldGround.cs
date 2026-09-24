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

        // Capture Unity Transform data once per simulation step; queries below use only managed value data.
        public TraversalSnapshot CaptureTraversal()=>new TraversalSnapshot(GridRoot!=null?GridRoot.worldToLocalMatrix:Matrix4x4.identity,Width,Height,CellSize,GridRoot!=null?PlayableCells:null);
        public readonly struct TraversalSnapshot
        {
            readonly Matrix4x4 inverse;readonly int width,height;readonly float cellSize;readonly byte[] cells;
            public TraversalSnapshot(Matrix4x4 inverse,int width,int height,float cellSize,byte[] cells){this.inverse=inverse;this.width=width;this.height=height;this.cellSize=cellSize;this.cells=cells;}
            bool Cell(Vector3 point,out int x,out int z){var p=inverse.MultiplyPoint3x4(point);x=Mathf.FloorToInt(p.x/cellSize+.5f);z=Mathf.FloorToInt(p.z/cellSize+.5f);return x>=0&&z>=0&&x<width&&z<height&&cells[z*width+x]!=0;}
            public bool HasClearRoute(Vector3 from,Vector3 to){
                if(cellSize<=0||cells==null||cells.Length!=width*height)return false;
                if(!Cell(from,out int x,out int z)||!Cell(to,out int endX,out int endZ))return false;
                if(x==endX&&z==endZ)return true;
                int steps=Mathf.CeilToInt(new Vector2(to.x-from.x,to.z-from.z).magnitude/(cellSize*.25f));
                for(int i=1;i<steps;i++)if(!Cell(Vector3.Lerp(from,to,(float)i/steps),out _,out _))return false;
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

        // Existing enemies move straight. Only admit spawn routes wholly inside the safe plain.
        // This is a route eligibility check, not obstacle-avoiding pathfinding.
        public bool HasClearRoute(Vector3 from, Vector3 to)
        {
            int steps = Mathf.CeilToInt(new Vector2(to.x - from.x, to.z - from.z).magnitude / (CellSize * .25f));
            for (int i = 0; i <= steps; i++)
                if (!IsPlayable(Vector3.Lerp(from, to, steps == 0 ? 0 : (float)i / steps))) return false;
            return true;
        }
    }
}
