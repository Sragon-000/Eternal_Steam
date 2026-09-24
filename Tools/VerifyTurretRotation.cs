using System;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Demo;
public static class VerifyTurretRotation
{
    sealed class Receiver:IDamageReceiver{public bool Alive=>true;public int Hits;public void ApplyDamage(float damage){Hits++;}}
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static string Main(){
        var definition=ScriptableObject.CreateInstance<BuildingDefinition>();var rotation=ScriptableObject.CreateInstance<TurretRotationDefinition>();var upgrade=ScriptableObject.CreateInstance<PerformanceUpgradeDefinition>();var weapon=ScriptableObject.CreateInstance<WeaponModuleDefinition>();
        rotation.DegreesPerSecond=90;rotation.AlignmentTolerance=0;definition.Modules.Add(rotation);definition.Modules.Add(upgrade);definition.Modules.Add(weapon);var targets=new TargetRegistry();var receiver=new Receiver();targets.Register(1,Vector3.right*5,TargetKind.Ground,receiver);
        try {
            using(var a=new BuildingInstance(1,definition,default,Vector3.zero,new BuildingServices(targets)))using(var b=new BuildingInstance(2,definition,default,Vector3.zero,new BuildingServices(targets))){
                a.Activate();b.Activate();a.Tick(.5f);var r=a.Module<ITurretRotation>();
                Check(Mathf.Abs(Vector3.Angle(Vector3.forward,r.Direction)-45)<.02f&&receiver.Hits==0,"Rate limited turn; no shot before alignment");
                Check(Vector3.Angle(Vector3.forward,b.Module<ITurretRotation>().Direction)<.001f,"Per-instance rotation independent");
                a.Tick(.6f);Check(receiver.Hits==1,"Fire when aligned");
                a.Module<IUpgradeControl>().TryUpgrade(out _);Check(Mathf.Abs(r.DegreesPerSecond-99)<.01f&&b.Module<ITurretRotation>().DegreesPerSecond==90,"Independent upgrade scaling");
                r.SetSpeedMultiplier(2);Check(Mathf.Abs(r.DegreesPerSecond-198)<.01f&&rotation.DegreesPerSecond==90,"Dedicated multiplier preserves shared asset");
                var direction=r.Direction;a.Tick(1,false);Check(Vector3.Angle(direction,r.Direction)<.001f,"Preparation cannot rotate combat module");
            }
        }finally{UnityEngine.Object.DestroyImmediate(definition);UnityEngine.Object.DestroyImmediate(rotation);UnityEngine.Object.DestroyImmediate(upgrade);UnityEngine.Object.DestroyImmediate(weapon);}
        return "PASS angular speed cap, no pre-alignment damage, aligned shot, independent instances/upgrade, dedicated multiplier and shared definition preserved.";
    }
}
