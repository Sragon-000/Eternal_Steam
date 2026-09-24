using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // Footprints use the same 2m / 45-degree lattice as construction.
    public sealed class SpawnAreaValidator
    {
        readonly Terrain terrain;readonly TileWorldGround tiles;readonly OpenWorldContent content;
        public SpawnAreaValidator(Terrain terrain,OpenWorldContent content){this.terrain=terrain;this.content=content;tiles=terrain.GetComponent<TileWorldGround>();}
        public bool Check(Vector3 center,int side,out string reason)=>Check(center,new Vector2Int(side,side),out reason);
        public bool Check(Vector3 center,Vector2Int footprint,out string reason)
        {
            reason=null;var origin=terrain.transform.position;var size=terrain.terrainData.size;
            for(int z=0;z<=footprint.y*2;z++)for(int x=0;x<=footprint.x*2;x++){
                var p=center+WorldGridGeometry.ToWorld(new Vector3(x-footprint.x,0,z-footprint.y));
                if(p.x<origin.x||p.z<origin.z||p.x>=origin.x+size.x||p.z>=origin.z+size.z||(tiles!=null&&!tiles.IsPlayable(p))){reason="스폰 구역 전체가 유효 지면 안에 있어야 합니다.";return false;}
            }
            foreach(var b in content.Bases.Buildings)if(b.Active&&!b.Disposed&&Overlaps(center,footprint,b.Position,b.Footprint)){reason="스폰 구역에 건물이 있습니다.";return false;}
            if(content.SpawnFoundationOverlap(center,footprint)){reason="스폰 구역에 토대가 있습니다.";return false;}
            return true;
        }
        public static bool Overlaps(Vector3 center,int side,Vector3 other,Vector2Int size)=>Overlaps(center,new Vector2Int(side,side),other,size);
        public static bool Overlaps(Vector3 center,Vector2Int footprint,Vector3 other,Vector2Int size){var d=WorldGridGeometry.ToLocal(center-other);return Mathf.Abs(d.x)<footprint.x+size.x-.001f&&Mathf.Abs(d.z)<footprint.y+size.y-.001f;}
    }
}
