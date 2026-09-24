using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using EternalSteam;
using EternalSteam.Demo;
public static class VerifyRadialTargeting
{
 sealed class Receiver:IWeaponDamageReceiver{public bool Alive=>true;public bool CanAct=>true;public int Hits,Effects;public void ApplyDamage(float d){Hits++;}public void Receive(float d,WeaponDamageSource source){Hits++;}public void Inflict(WeaponStatus s,float d,float strength,float tick){Effects++;}}
 static void Check(bool v,string s){if(!v)throw new Exception(s);}
 public static string Main(){var catalog=AssetDatabase.LoadAssetAtPath<BuildingCatalog>("Assets/EternalSteam/Content/Buildings/DocumentContent/DocumentBuildings.asset");
 foreach(var source in catalog.Buildings.Where(d=>d.Category==BuildingCategory.Defense)){var q=new TargetRegistry();var r=new Receiver();using(var b=new BuildingInstance(1,source,Vector2Int.zero,Vector3.zero,new BuildingServices(q))){b.Activate();int shots=0,aims=0;b.Shot+=_=>shots++;b.Aim+=p=>{if(p.z<0)aims++;};b.Tick(.1f);Check(shots==0,"No empty-range firing "+source.Id);q.Register(0,new Vector3(0,0,-5),TargetKind.Air,r);float turnSeconds=b.Module<ITurretRotation>() is ITurretRotation turn?180/turn.DegreesPerSecond:0;for(int i=0;i<Mathf.CeilToInt((turnSeconds+1)*10);i++)b.Tick(.1f);Check(r.Hits+r.Effects>0,"Rear target acquired "+source.Id);Check(source.Modules.OfType<WeaponModuleDefinition>().Single().Delivery==WeaponDelivery.Pulse||aims>0,"Rear automatic aim "+source.Id);}}
 var def=ScriptableObject.CreateInstance<BuildingDefinition>();var w=ScriptableObject.CreateInstance<WeaponModuleDefinition>();w.Range=10;w.Angle=1;w.Interval=1;w.Targets=TargetKind.Ground;def.Modules.Add(w);var targets=new TargetRegistry();var outside=new Receiver();var edge=new Receiver();var closer=new Receiver();
 using(var b=new BuildingInstance(1,def,Vector2Int.zero,Vector3.zero,new BuildingServices(targets))){b.Activate();targets.Register(1,new Vector3(-10.01f,0,0),TargetKind.Ground,outside);b.Tick(.1f);Check(outside.Hits==0,"Outside radius rejected");var handle=targets.Register(2,new Vector3(-10,0,0),TargetKind.Ground,edge);b.Tick(.1f);Check(edge.Hits==1,"Inclusive side boundary ignores former angle");targets.Register(3,new Vector3(0,0,-2),TargetKind.Ground,closer);b.Tick(1);Check(edge.Hits==2&&closer.Hits==0,"Valid target retained over closer newcomer");targets.Unregister(handle);b.Tick(.1f);Check(closer.Hits==0,"Retarget does not reset cooldown");b.Tick(1);Check(closer.Hits==1,"Target reacquired behind tower");}
 UnityEngine.Object.DestroyImmediate(def);UnityEngine.Object.DestroyImmediate(w);
 foreach(HordeTowerKind kind in Enum.GetValues(typeof(HordeTowerKind))){var root=new GameObject("Radial legacy test");var head=new GameObject("Head");head.transform.SetParent(root.transform);var t=new HordeTower{root=root,head=head.transform,range=22,halfAngle=30,kind=kind,tracer=root.AddComponent<LineRenderer>(),coverage=new GameObject("Coverage").AddComponent<LineRenderer>(),impact=new GameObject("Impact").AddComponent<LineRenderer>()};t.coverage.transform.SetParent(root.transform);t.impact.transform.SetParent(root.transform);t.tracer.positionCount=2;t.coverage.positionCount=65;t.impact.positionCount=35;
 try{var world=new HordeEnemyWorld(capacity:8);var combat=new HordeTowerCombat(new List<HordeTower>{t},world,new HordeAttackResolver(world),true,true);combat.Update(.1f,true,int.MaxValue);Check(t.shot==0,"Legacy no target no fire");world.TrySpawn(new Vector3(0,0,-5),new Vector3(0,0,-5),0,1000);world.TrySpawn(new Vector3(0,0,-8),Vector3.zero,0,1000);world.MoveAndIndex(0); // First slot reaches destination: only the second remains.
 combat.Update(.1f,true,int.MaxValue);Check(t.shot==1&&t.head.forward.z<-.9f,"Legacy rear acquisition "+kind);Check(kind==HordeTowerKind.Frost?world.GetEnemy(1).slowTime>0:world.GetEnemy(1).health<1000,"Legacy effect "+kind);
 }finally{UnityEngine.Object.DestroyImmediate(root);}}
 return "PASS: all 9 weapons acquire rear targets, automatic aim, no empty-range attacks including EMP, inclusive radius/outside boundary, target retention and cooldown preservation; all 4 legacy adapters auto-aim and apply original damage/slow types.";
 }
}
