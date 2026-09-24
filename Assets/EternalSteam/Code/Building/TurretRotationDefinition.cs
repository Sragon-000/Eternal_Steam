using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    public interface ITurretRotation
    {
        Vector3 Direction {get;}
        float DegreesPerSecond {get;}
        bool AimAt(Vector3 position,float seconds);
        void SetSpeedMultiplier(float multiplier);
    }
    [CreateAssetMenu(menuName="Eternal Steam/Modules/Turret Rotation")]
    public sealed class TurretRotationDefinition:BuildingModuleDefinition
    {
        [Min(.01f)] public float DegreesPerSecond=180;
        [Range(0,10)] public float AlignmentTolerance=1;
        public override IBuildingModule CreateRuntime()=>new TurretRotation(DegreesPerSecond,AlignmentTolerance);
        public override bool Provides(Type type)=>type==typeof(ITurretRotation);
        public override void Validate(List<string> errors){if(!float.IsFinite(DegreesPerSecond)||DegreesPerSecond<=0||!float.IsFinite(AlignmentTolerance)||AlignmentTolerance<0||AlignmentTolerance>10)errors.Add("Invalid turret rotation speed or alignment tolerance.");}
    }
    public sealed class TurretRotation:IBuildingModule,ITurretRotation
    {
        readonly float speed,tolerance;[Saved(.00001)] float multiplier=1;BuildingInstance owner;IPerformanceScaling scaling;
        [Saved] public Vector3 Direction {get;private set;}=Vector3.forward;
        public float DegreesPerSecond=>scaling?.Increase(speed*multiplier)??speed*multiplier;
        public TurretRotation(float speed,float tolerance){this.speed=speed;this.tolerance=tolerance;}
        public void Initialize(BuildingInstance building,BuildingServices services){owner=building;scaling=owner.Module<IPerformanceScaling>();owner.DirectionChanged+=ResetDirection;ResetDirection();}
        void ResetDirection()=>Direction=owner.Direction;
        public void SetSpeedMultiplier(float value){if(!float.IsFinite(value)||value<=0)throw new ArgumentOutOfRangeException(nameof(value));multiplier=value;}
        public bool AimAt(Vector3 position,float seconds){
            if(!owner.Operational||!CombatPermission.Allows(owner)||!float.IsFinite(seconds)||seconds<0)return false;
            var target=position-owner.Position;target.y=0;if(!float.IsFinite(target.sqrMagnitude))return false;
            if(target.sqrMagnitude<.000001f)return true;
            var current=Quaternion.LookRotation(Direction);var desired=Quaternion.LookRotation(target.normalized);
            Direction=Quaternion.RotateTowards(current,desired,DegreesPerSecond*seconds)*Vector3.forward;
            owner.ReportAim(owner.Position+Direction);
            return Vector3.Angle(Direction,target)<=tolerance+.0001f;
        }
        public void Activate(){}public void Tick(float dt){}
        public void Dispose(){if(owner!=null)owner.DirectionChanged-=ResetDirection;}
    }
}
