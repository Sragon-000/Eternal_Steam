using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // Logical cells share the 2m, 45-degree construction lattice. Cached on base/obstacle changes.
    public sealed class MapCoverage
    {
        public readonly Vector3[] Centers;
        public readonly bool[] Covered,Eligible;
        public int BaseCount {get;private set;}
        readonly bool[] playable;
        readonly SpawnAreaValidator spawnAreas;
        readonly OpenWorldContent content;
        readonly TileWorldGround tiles;
        readonly NexusDestinationQuery destinations;
        int revision=-1;
        public MapCoverage(OpenWorldContent content,Terrain terrain,NexusDestinationQuery destinations)
        {
            spawnAreas=new SpawnAreaValidator(terrain,content);this.content=content;this.destinations=destinations;tiles=terrain.GetComponent<TileWorldGround>();
            var origin=terrain.transform.position;var size=terrain.terrainData.size;
            var min=new Vector2Int(int.MaxValue,int.MaxValue);var max=new Vector2Int(int.MinValue,int.MinValue);
            for(int z=0;z<2;z++)for(int x=0;x<2;x++){var c=WorldGridGeometry.Cell(origin+new Vector3(x*size.x,0,z*size.z),2);min=Vector2Int.Min(min,c);max=Vector2Int.Max(max,c);}
            int width=max.x-min.x+1,height=max.y-min.y+1;
            Centers=new Vector3[width*height];Covered=new bool[Centers.Length];Eligible=new bool[Centers.Length];playable=new bool[Centers.Length];
            for(int z=0;z<height;z++)for(int x=0;x<width;x++){
                int i=z*width+x;var p=WorldGridGeometry.Center(min+new Vector2Int(x,z),2);p.y=terrain.SampleHeight(p)+origin.y;Centers[i]=p;
                playable[i]=p.x>=origin.x&&p.x<origin.x+size.x&&p.z>=origin.z&&p.z<origin.z+size.z&&(tiles==null||tiles.IsPlayable(p));
            }
        }
        public int NearestCell(Vector3 point){int best=-1;float distance=float.PositiveInfinity;for(int i=0;i<Centers.Length;i++){var d=Centers[i]-point;d.y=0;if(playable[i]&&d.sqrMagnitude<distance){best=i;distance=d.sqrMagnitude;}}return best;}
        public bool Refresh()
        {
            content.Bases.Refresh();if(revision==content.Bases.Revision)return false;revision=content.Bases.Revision;destinations.Refresh();
            BaseCount=0;foreach(var context in content.Bases.Bases.Values)if(Normal(context))BaseCount++;
            for(int i=0;i<Centers.Length;i++){
                var p=Centers[i];bool covered=false,safe=false;
                foreach(var context in content.Bases.Bases.Values){if(!Normal(context))continue;var b=context.Nexus;var area=b.Module<IBuildArea>();if(area==null)continue;
                    var local=area is IBuildAreaGeometry geometry?geometry.ToLocal(p):WorldGridGeometry.ToLocal(p-b.Position);
                    // Half-open center inclusion gives exactly 11 cells at odd-sized base boundaries.
                    covered|=Inside(local,area.Radius);safe|=Inside(local,area.Radius+10);
                }
                Covered[i]=playable[i]&&covered;
                Eligible[i]=playable[i]&&!safe&&spawnAreas.Check(p,1,out _)&&!content.BuildingTargets.FirstBlocker(p,p,out _,out _)&&destinations.TryNearest(p,out var target)&&(tiles==null||tiles.HasClearRoute(p,target));
            }
            return true;
        }
        static bool Inside(Vector3 p,float r)=>p.x>=-r&&p.x<r&&p.z>=-r&&p.z<r;
        static bool Normal(BaseContext context)=>context.Active&&(context.Nexus.Module<IBaseRole>()?.Role is BaseRole.Main or BaseRole.Sub);
    }
}
