using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // North-up (world +Z). UI Y grows down, independent of the construction grid's rotation.
    public readonly struct MinimapProjection
    {
        public readonly Rect Bounds;
        public MinimapProjection(Rect bounds){Bounds=bounds;}
        public Vector2 ToMap(Vector3 world)=>new((world.x-Bounds.xMin)/Bounds.width,1-(world.z-Bounds.yMin)/Bounds.height);
        public Vector3 ToWorld(Vector2 point)=>new(Bounds.xMin+Mathf.Clamp01(point.x)*Bounds.width,0,Bounds.yMin+(1-Mathf.Clamp01(point.y))*Bounds.height);
        public static MinimapProjection ForTerrain(Terrain terrain)
        {
            var origin=terrain.transform.position;var size=terrain.terrainData.size;var bounds=new Rect(origin.x,origin.z,size.x,size.z);
            var tiles=terrain.GetComponent<TileWorldGround>();
            if(tiles!=null&&tiles.GridRoot!=null&&tiles.Width>0&&tiles.Height>0&&tiles.CellSize>0){
                var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
                for(int z=0;z<2;z++)for(int x=0;x<2;x++){
                    var p=tiles.GridRoot.TransformPoint(new Vector3((x*tiles.Width-.5f)*tiles.CellSize,0,(z*tiles.Height-.5f)*tiles.CellSize));
                    min=Vector2.Min(min,new Vector2(p.x,p.z));max=Vector2.Max(max,new Vector2(p.x,p.z));
                }
                bounds=Rect.MinMaxRect(min.x,min.y,max.x,max.y);
            }
            float side=Mathf.Max(bounds.width,bounds.height);return new MinimapProjection(new Rect(bounds.center-Vector2.one*side*.5f,Vector2.one*side));
        }
    }
}
