using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // A transient outline: no shared material mutation and no permanent health bar.
    public sealed class BuildingHitView:MonoBehaviour
    {
        BuildingInstance building;LineRenderer flash;float until;
        public static void Attach(GameObject view,BuildingInstance building,Material material,float cellSize,Quaternion rotation){
            if(building.Module<IDamageReceiver>()==null)return;
            var component=view.AddComponent<BuildingHitView>();component.building=building;
            component.flash=BuildingView.MakeLine("Damage flash",view.transform,material,.18f);component.flash.positionCount=5;
            var half=(Vector2)building.Footprint*(cellSize*.5f);var center=building.Position+Vector3.up*.4f;
            var offsets=new[]{new Vector3(-half.x,0,-half.y),new Vector3(-half.x,0,half.y),new Vector3(half.x,0,half.y),new Vector3(half.x,0,-half.y),new Vector3(-half.x,0,-half.y)};
            for(int i=0;i<5;i++)component.flash.SetPosition(i,center+rotation*offsets[i]);
            component.flash.startColor=component.flash.endColor=Color.red;component.flash.enabled=false;building.Damaged+=component.Hit;
        }
        void Hit(float amount){until=Time.unscaledTime+.15f;flash.enabled=true;}
        void Update(){flash.enabled=Time.unscaledTime<until;}
        void OnDestroy(){if(building!=null)building.Damaged-=Hit;}
    }
}
