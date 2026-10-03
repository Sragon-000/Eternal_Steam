using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    public readonly struct BuildingSelectionItem
    {
        public readonly BuildingWorld World;
        public readonly BuildingInstance Building;
        public BuildingSelectionItem(BuildingWorld world,BuildingInstance building){World=world;Building=building;}
    }
    public static class BuildingRectangleSelection
    {
        public static Rect Bounds(Vector2 a,Vector2 b)=>Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.y,b.y),Mathf.Max(a.x,b.x),Mathf.Max(a.y,b.y));
        // Screen-space rectangle, inclusive building centers. Foundations are floors, not implicit recovery targets.
        public static void Collect(Camera camera,Rect rectangle,BuildingWorld ground,IEnumerable<FoundationPlacement.Platform> platforms,List<BuildingSelectionItem> results)
        {
            results.Clear();CollectWorld(camera,rectangle,ground,results);
            foreach(var platform in platforms)CollectWorld(camera,rectangle,platform.World,results);
        }
        static void CollectWorld(Camera camera,Rect rectangle,BuildingWorld world,List<BuildingSelectionItem> results)
        {
            foreach(var b in world.Buildings){
                if(!b.Active||b.Disposed||!b.Recoverable)continue;
                var p=camera.WorldToScreenPoint(b.Position);
                if(p.z>=camera.nearClipPlane&&p.x>=rectangle.xMin&&p.x<=rectangle.xMax&&p.y>=rectangle.yMin&&p.y<=rectangle.yMax)results.Add(new BuildingSelectionItem(world,b));
            }
        }
    }
}
