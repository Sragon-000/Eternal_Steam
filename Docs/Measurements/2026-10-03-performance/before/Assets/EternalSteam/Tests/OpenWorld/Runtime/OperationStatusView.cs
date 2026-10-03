using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // A crossed footprint remains legible without changing selection/recovery materials.
    public sealed class OperationStatusView:MonoBehaviour
    {
        BuildingInstance building;LineRenderer cross;
        public static void Attach(GameObject view,BuildingInstance building,Material material)
        {
            var status=view.AddComponent<OperationStatusView>();status.building=building;
            status.cross=BuildingView.MakeLine("Inactive footprint cross",view.transform,material,.12f);
            status.cross.startColor=status.cross.endColor=new Color(1,.5f,.1f);
            status.cross.positionCount=5;
            var half=(Vector2)building.Footprint;var origin=building.Position+Vector3.up*.3f;
            status.cross.SetPosition(0,origin+WorldGridGeometry.ToWorld(new Vector3(-half.x,0,-half.y)));
            status.cross.SetPosition(1,origin+WorldGridGeometry.ToWorld(new Vector3(half.x,0,half.y)));
            status.cross.SetPosition(2,origin);
            status.cross.SetPosition(3,origin+WorldGridGeometry.ToWorld(new Vector3(-half.x,0,half.y)));
            status.cross.SetPosition(4,origin+WorldGridGeometry.ToWorld(new Vector3(half.x,0,-half.y)));
            building.OperationChanged+=status.Refresh;status.Refresh();
        }
        void Refresh(){if(cross!=null)cross.enabled=!building.Operational;}
        void OnDestroy(){if(building!=null)building.OperationChanged-=Refresh;}
    }
}
