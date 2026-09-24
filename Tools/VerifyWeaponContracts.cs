using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.Demo;
using EternalSteam.OpenWorld;
public static class VerifyWeaponContracts
{
 sealed class Receiver:IWeaponDamageReceiver{public bool Alive=>true;public bool CanAct=>true;public int Hits,Statuses;public float Damage;public WeaponStatus Last;public void ApplyDamage(float d)=>Receive(d,WeaponDamageSource.Normal);public void Receive(float d,WeaponDamageSource s){Hits++;Damage+=d;}public void Inflict(WeaponStatus s,float d,float strength,float tick){Statuses++;Last=s;}}
 static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
 public static string Main()
 {
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();int tests=0;
  void Test(string id,Action<BuildingInstance,WeaponModuleDefinition,TargetRegistry,List<Receiver>> test){
   var source=s.ContentCatalog.Buildings.First(d=>d.Id=="defense."+id);var definition=ScriptableObject.CreateInstance<BuildingDefinition>();var weapon=UnityEngine.Object.Instantiate((WeaponModuleDefinition)source.Modules[0]);definition.Modules.Add(weapon);var registry=new TargetRegistry();var receivers=new List<Receiver>();
   for(int i=0;i<5;i++){var r=new Receiver();receivers.Add(r);registry.Register(i,new Vector3(0,0,5+i),i==4?TargetKind.Ground:TargetKind.Air,r);}
   using(var b=new BuildingInstance(1,definition,Vector2Int.zero,Vector3.zero,new BuildingServices(registry))){b.Activate();test(b,weapon,registry,receivers);}
   UnityEngine.Object.Destroy(definition);UnityEngine.Object.Destroy(weapon);tests++;
  }
  Test("vulcan_aa",(b,w,q,r)=>{for(int i=0;i<50;i++)b.Tick(.1f);Check(r[0].Hits==25,"Burst must fire 25 shots, got "+r[0].Hits);Check(r[4].Hits==0,"AA ground filter");for(int i=0;i<19;i++)b.Tick(.1f);Check(r[0].Hits==25,"Burst rest");});
  Test("plasma_laser",(b,w,q,r)=>{for(int i=0;i<30;i++)b.Tick(.1f);Check(r[0].Statuses==0,"No overheat before 3 seconds");for(int i=0;i<6;i++)b.Tick(.1f);Check(r[0].Last==WeaponStatus.Overheat,"Overheat after sustained target");var handle=new TargetHandle(0,1);q.Unregister(handle);b.Tick(.1f);Check(r[1].Statuses==0,"New target resets tracking");});
  Test("railgun",(b,w,q,r)=>{b.Tick(.1f);Check(r.Sum(x=>x.Hits)==3,"Railgun hits maximum three");});
  Test("tesla",(b,w,q,r)=>{b.Tick(.1f);Check(r[0].Damage==80,"Chain initial coefficient");for(int i=0;i<5;i++)b.Tick(.1f);Check(Mathf.Abs(r[1].Damage-16)<.01f,"Chain retains 20 percent after delay");for(int i=0;i<6;i++)b.Tick(.1f);Check(r[0].Hits>=2,"Chain may revisit previous target");});
  Test("smart_missile",(b,w,q,r)=>{b.Tick(.1f);Check(r.Sum(x=>x.Hits)==0,"Projectile does not hit instantly");for(int i=0;i<10;i++)b.Tick(.1f);Check(r[0].Hits==3,"Three separate projectiles with overlapping explosions");});
  Test("emp",(b,w,q,r)=>{b.Tick(.1f);Check(r.All(x=>x.Damage==0&&x.Last==WeaponStatus.Stun),"Self centered zero damage stun");});
  Test("sky_plasma",(b,w,q,r)=>{for(int i=0;i<10;i++)b.Tick(.1f);Check(r[0].Last==WeaponStatus.Burn&&r[4].Hits==0,"Air-only plasma explosion and burn");});
  // Real array-backed status behavior, independent of game objects.
  var world=new HordeEnemyWorld(capacity:10);var adapter=new HordeTargetAdapter(world);world.TrySpawn(new Vector3(0,0,2),new Vector3(0,0,20),2,1000);world.MoveAndIndex(0);var h=new TargetHandle(0,world.Generation(0));Check(adapter.TryGet(h,out var info),"Array handle resolves");var receiver=(IWeaponDamageReceiver)info.Receiver;
  receiver.Inflict(WeaponStatus.Overheat,3,.2f,0);receiver.Receive(100,WeaponDamageSource.Normal);receiver.Receive(100,WeaponDamageSource.PlasmaLaser);Check(world.GetEnemy(0).health==780,"Overheat only amplifies plasma laser");
  receiver.Inflict(WeaponStatus.Stun,2,1,0);var before=world.GetEnemy(0).position;world.MoveAndIndex(1);Check(!receiver.CanAct&&world.GetEnemy(0).position==before,"Stun blocks movement and action contract");adapter.Tick(2.01f);Check(receiver.CanAct,"Stun expires");
  receiver.Inflict(WeaponStatus.Burn,3,.2f,10);adapter.Tick(3);Check(world.GetEnemy(0).health==750,"Three burn ticks");world.Remove(0,true);world.TrySpawn(new Vector3(0,0,2),new Vector3(0,0,20),2,1000);Check(!adapter.TryGet(h,out _)&&!receiver.Alive,"Slot reuse invalidates old handles and receivers");adapter.Tick(1);Check(world.GetEnemy(0).health==1000&&world.GetEnemy(0).movementPenalty==0,"Reused slot has no stale effects");
  return $"PASS: {tests} composed weapon contracts, burst count/rest, target tracking reset, chain delay/20% retention/revisit, three-hit piercing, multi-projectile splash, EMP, air filters, overheat source, stun/action gate, 3 burn ticks, generation reuse.";
 }
}
