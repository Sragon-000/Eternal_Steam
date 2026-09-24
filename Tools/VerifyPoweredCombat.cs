using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Demo;
public static class VerifyPoweredCombat
{
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    sealed class Receiver:IDamageReceiver{public bool Alive=>true;public int Hits;public void ApplyDamage(float damage){Hits++;}}
    public static string Main()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var registry=new BaseRegistry(2,Quaternion.Euler(0,45,0)){AnyNormalBaseCoverage=true};var campaign=new CampaignProgression();var buildings=new List<BuildingInstance>();var roots=new List<GameObject>();
        BuildingInstance Add(BuildingDefinition def,int id,ITargetQuery targets=null){var b=new BuildingInstance(id,def,default,Vector3.zero,new BuildingServices(targets,campaign:campaign));b.Activate();registry.Register(b);buildings.Add(b);return b;}
        var main=Add(s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.main_base"),1);var generator=Add(s.ContentCatalog.Buildings.Single(d=>d.Id=="resource.power_generator"),2);
        var towers=new List<HordeTower>();
        foreach(HordeTowerKind kind in Enum.GetValues(typeof(HordeTowerKind))){var b=Add(s.Foundations.Definition(kind),3+(int)kind);var root=new GameObject("Power test "+kind);roots.Add(root);var head=new GameObject("Head");head.transform.SetParent(root.transform);
            LineRenderer Line(int count){var go=new GameObject("Line");go.transform.SetParent(root.transform);var line=go.AddComponent<LineRenderer>();line.positionCount=count;return line;}
            towers.Add(new HordeTower{building=b,root=root,head=head.transform,kind=kind,range=20,halfAngle=30,tracer=Line(2),coverage=Line(65),impact=Line(35)});
        }
        try{
            var world=new HordeEnemyWorld(capacity:8);world.TrySpawn(Vector3.forward*5,Vector3.forward*30,0,100000);world.MoveAndIndex(0);var combat=new HordeTowerCombat(towers,world,new HordeAttackResolver(world),true,true);var power=new BasePowerSimulation(registry,2,Quaternion.Euler(0,45,0));
            combat.Update(2,true,int.MaxValue);Check(towers.All(t=>t.shot==0),"Four legacy types block without power");power.Tick(1);combat.Update(.1f,true,int.MaxValue);Check(towers.All(t=>t.shot>0),"Four legacy types fire with power");
            var shots=towers.Select(t=>t.shot).ToArray();var cooldown=towers.Select(t=>t.cooldown).ToArray();registry.Remove(generator);generator.Dispose();power.Tick(1);combat.Update(2,true,int.MaxValue);
            Check(towers.Select((t,i)=>t.shot==shots[i]&&t.cooldown==cooldown[i]).All(x=>x),"No attack or cooldown catch-up while unpowered");
            foreach(var tower in towers){registry.Remove(tower.building);tower.building.Dispose();}
            var query=new TargetRegistry();var receiver=new Receiver();query.Register(0,Vector3.forward*5,TargetKind.Ground,receiver);
            var d=ScriptableObject.CreateInstance<BuildingDefinition>();var weapon=ScriptableObject.CreateInstance<WeaponModuleDefinition>();weapon.Delivery=WeaponDelivery.Projectile;weapon.ProjectileSpeed=10;weapon.BaseDamage=1;weapon.Interval=10;d.Modules.Add(weapon);d.Modules.Add(s.LegacyPower);
            try{
                var b=Add(d,20,query);b.Tick(1);Check(receiver.Hits==0,"New weapon blocks");generator=Add(s.ContentCatalog.Buildings.Single(d=>d.Id=="resource.power_generator"),21);power.Tick(1);b.Tick(.01f);Check(receiver.Hits==0,"Projectile flight started");registry.Remove(generator);generator.Dispose();power.Tick(4);Check(!CombatPermission.Allows(b),"Power depleted after launch");for(int i=0;i<100;i++)b.Tick(.02f);Check(receiver.Hits==1,"Existing projectile continues after power loss, no new shots");
            }finally{UnityEngine.Object.DestroyImmediate(d);UnityEngine.Object.DestroyImmediate(weapon);}
            return "PASS: actual four legacy types blocked/powered/frozen cooldown; composed weapon gated; existing projectile continues without new attacks";
        }finally{foreach(var b in buildings)b.Dispose();foreach(var root in roots)UnityEngine.Object.DestroyImmediate(root);}
    }
}
