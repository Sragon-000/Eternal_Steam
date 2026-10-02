using System;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // The baked clip contains the Blender wheel/rod/piston linkage. Geometry is authored in the prefab.
    public sealed class TrainConsistView : MonoBehaviour
    {
        public GameObject Model;
        public AnimationClip LinkedMotion;
        public Transform CargoFront,LocomotiveRear,CargoRear,FoundationFront,TurretMount;
        public float MetresPerCycle=1.67f;
        Vector3 previous;bool initialized;float phase;
        public float Phase=>phase;
        public void Present(bool moving)
        {
            if(Model==null||LinkedMotion==null)throw new InvalidOperationException("Train model and baked linkage clip must be saved in the prefab.");
            var position=transform.position;
            if(initialized&&moving){var delta=position-previous;
                // Restores/teleports are presentation resets, not travelled distance.
                if(delta.sqrMagnitude<16){float sign=Vector3.Dot(delta,transform.forward)<0?-1:1;
                    phase=Mathf.Repeat(phase+sign*delta.magnitude/Mathf.Max(.01f,MetresPerCycle),1);}}
            LinkedMotion.SampleAnimation(Model,phase*LinkedMotion.length);previous=position;initialized=true;
        }
    }
}
