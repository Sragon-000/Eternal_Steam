using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // The world plane remains XZ. Only the construction lattice rotates about Y.
    public static class WorldGridGeometry
    {
        public const float Yaw = 45;
        public static readonly Quaternion Rotation = Quaternion.Euler(0,Yaw,0);
        public static Vector3 ToWorld(Vector3 local) => Rotation * local;
        public static Vector3 ToLocal(Vector3 world) => Quaternion.Inverse(Rotation) * world;
        public static Vector2Int Cell(Vector3 point,float size)
        { var p=ToLocal(point);return new Vector2Int(Mathf.FloorToInt(p.x/size),Mathf.FloorToInt(p.z/size)); }
        public static Vector3 Center(Vector2Int cell,float size,float height=0)
        { var p=ToWorld(new Vector3((cell.x+.5f)*size,0,(cell.y+.5f)*size));p.y=height;return p; }
        public static BuildGrid Grid(Transform foundation,float top)
        {
            var origin=foundation.position-foundation.rotation*new Vector3(4,0,4);origin.y=top-.01f;
            return new BuildGrid(new RectInt(0,0,4,4),2,origin,foundation.eulerAngles.y);
        }
        public static bool TerrainPlacement(Terrain terrain,Vector3 point,out Vector3 center,out string reason)
        {
            if(terrain.TryGetComponent<TileWorldGround>(out var tileWorld))return tileWorld.CheckFoundation(point,out center,out reason);
            center=Center(Cell(point,8),8);var origin=terrain.transform.position;var size=terrain.terrainData.size;
            float low=float.MaxValue,high=float.MinValue;
            for(int z=0;z<=4;z++)for(int x=0;x<=4;x++) {
                var p=center+ToWorld(new Vector3(x*2-4,0,z*2-4));
                if(p.x<origin.x || p.z<origin.z || p.x>origin.x+size.x || p.z>origin.z+size.z)
                {reason="회전된 토대의 모서리가 지형 밖입니다.";return false;}
                float h=terrain.SampleHeight(p)+origin.y;low=Mathf.Min(low,h);high=Mathf.Max(high,h);
            }
            center.y=high+.35f;
            if(high-low>1.2f){reason="경사가 큽니다. 평탄한 곳을 선택하세요.";return false;}
            reason="45° 회전된 3D 토대 · 클릭하여 임시 배치";return true;
        }
        public static bool Overlaps(Vector3 center,Transform other)
        {
            var delta=other.position-center;delta.y=0;
            var a=Rotation*Vector3.right;var b=Rotation*Vector3.forward;
            var c=other.rotation*Vector3.right;var d=other.rotation*Vector3.forward;
            foreach(var axis in new[]{a,b,c,d}) {
                float extent=4*(Mathf.Abs(Vector3.Dot(a,axis))+Mathf.Abs(Vector3.Dot(b,axis))+Mathf.Abs(Vector3.Dot(c,axis))+Mathf.Abs(Vector3.Dot(d,axis)));
                if(Mathf.Abs(Vector3.Dot(delta,axis))>=extent-.001f)return false;
            }
            return true;
        }
    }
}
