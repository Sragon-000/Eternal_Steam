using UnityEngine;
namespace EternalSteam.OpenWorld
{
    public sealed class TrainDefenseView:MonoBehaviour
    {
        public Transform Turret,Muzzle;public LineRenderer ShotLine;
        BuildingInstance building;float visible;Vector3 target;
        public void Bind(BuildingInstance value)
        {
            if(ReferenceEquals(building,value))return;
            if(building!=null){building.Shot-=Shot;building.Projectile-=Shot;building.Aim-=Aim;}
            building=value;visible=0;Turret.gameObject.SetActive(value!=null);ShotLine.enabled=false;
            if(value!=null){value.Shot+=Shot;value.Projectile+=Shot;value.Aim+=Aim;}
        }
        void Aim(Vector3 point){var direction=point-Turret.position;direction.y=0;if(direction.sqrMagnitude>.001f)Turret.rotation=Quaternion.LookRotation(direction);}
        void Shot(Vector3 point){target=point;visible=.12f;ShotLine.enabled=true;ShotLine.SetPosition(0,Muzzle.position);ShotLine.SetPosition(1,target);}
        void Update(){if(visible>0){visible-=Time.deltaTime;ShotLine.SetPosition(0,Muzzle.position);}ShotLine.enabled=visible>0&&building!=null;}
        void OnDestroy(){if(building!=null){building.Shot-=Shot;building.Projectile-=Shot;building.Aim-=Aim;}}
    }
}
