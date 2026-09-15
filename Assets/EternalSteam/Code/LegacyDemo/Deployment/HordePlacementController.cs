using System;
using UnityEngine;
namespace EternalSteam.Demo
{
    public readonly struct HordePlacementInput
    {
        public readonly bool HasWorldPoint, CancelPressed, ConfirmPressed;
        public readonly Vector3 WorldPoint;
        public HordePlacementInput(bool hasWorldPoint,Vector3 worldPoint,bool cancelPressed=false,bool confirmPressed=false)
        { HasWorldPoint=hasWorldPoint; WorldPoint=worldPoint; CancelPressed=cancelPressed; ConfirmPressed=confirmPressed; }
    }
    // No devices, camera, renderers or simulation implementation: callers provide placement commands.
    public sealed class HordePlacementController
    {
        readonly Func<Vector3,bool> canPlace;
        readonly Func<Vector3,Vector3,float,bool> place;
        public bool ChoosingDirection { get; private set; }
        public bool Visible { get; private set; }
        public bool CanPlace { get; private set; }
        public Vector3 Position { get; private set; }
        public Vector3 LockedPosition { get; private set; }
        public Vector3 Direction { get; private set; }=Vector3.forward;
        public float Range { get; private set; }=HordeDeploymentGeometry.Range;
        public HordePlacementController(Func<Vector3,bool> canPlace,Func<Vector3,Vector3,float,bool> place)
        { this.canPlace=canPlace??throw new ArgumentNullException(nameof(canPlace)); this.place=place??throw new ArgumentNullException(nameof(place)); }
        public void Cancel()=>ChoosingDirection=false;
        public bool Begin(Vector3 position,HordeMapKind map)
        {
            position=HordeMapLayout.Snap(position);
            if(!canPlace(position)) return false;
            LockedPosition=position; Direction=HordeMapLayout.DefaultDirection(map,position);
            Range=HordeDeploymentGeometry.Range; ChoosingDirection=true; return true;
        }
        public bool Confirm(Vector3 direction,float range)
        {
            if(!ChoosingDirection || !place(LockedPosition,direction,range)) return false;
            Cancel(); return true;
        }
        public void Tick(bool canBuild,bool pointerBlocked,HordeMapKind map,HordePlacementInput input)
        {
            if(input.CancelPressed) Cancel();
            var pointer=input.WorldPoint; var size=HordeMapLayout.Size(map);
            Visible=canBuild && !pointerBlocked && input.HasWorldPoint
                && (ChoosingDirection || Mathf.Abs(pointer.x)<=size.x/2 && Mathf.Abs(pointer.z)<=size.y/2);
            if(!Visible) return;
            Position=HordeMapLayout.Snap(ChoosingDirection?LockedPosition:pointer);
            CanPlace=canPlace(Position);
            if(ChoosingDirection)
            {
                var aim=pointer-LockedPosition; aim.y=0;
                if(aim.sqrMagnitude>0.25f) Direction=aim.normalized;
                Range=Mathf.Clamp(aim.magnitude,HordeDeploymentGeometry.MinRange,HordeDeploymentGeometry.MaxRange);
            }
            else Direction=HordeMapLayout.DefaultDirection(map,Position);
            if(CanPlace && input.ConfirmPressed)
            {
                if(ChoosingDirection) Confirm(Direction,Range);
                else Begin(Position,map);
            }
        }
    }
}
