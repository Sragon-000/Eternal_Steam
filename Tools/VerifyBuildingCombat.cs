using System;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.Demo;
using EternalSteam.OpenWorld;
public static class VerifyBuildingCombat
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    sealed class Fixture:IDisposable
    {
        public readonly BuildingTargetRegistry Targets=new();
        public readonly HordeEnemyWorld Enemies;
        public readonly HordeTargetAdapter Effects;
        public readonly EnemyBuildingCombat Combat;
        readonly List<BuildingInstance> buildings=new();readonly List<UnityEngine.Object> assets=new();
        public Fixture(int capacity=32,Func<Vector3,float> ground=null){Enemies=new HordeEnemyWorld(ground,false,capacity){ArrivalPolicy=EnemyArrivalPolicy.Remain};Effects=new HordeTargetAdapter(Enemies);Combat=new EnemyBuildingCombat(Enemies,Targets,Effects,null);}
        public BuildingInstance Add(BuildingCombatRole role,Vector3 position,float hp=100, float yaw=0, bool active=true){
            var d=ScriptableObject.CreateInstance<BuildingDefinition>();var h=ScriptableObject.CreateInstance<HealthModuleDefinition>();var b=ScriptableObject.CreateInstance<BuildingCombatDefinition>();
            d.Id="test";d.Footprint=Vector2Int.one;h.Maximum=hp;b.Role=role;d.Modules.Add(h);d.Modules.Add(b);assets.Add(d);assets.Add(h);assets.Add(b);
            // Deliberately duplicate local building IDs, as happens across foundation worlds.
            var instance=new BuildingInstance(1,d,default,position,new BuildingServices(null));buildings.Add(instance);
            instance.Destroyed+=Targets.Remove;if(active)instance.Activate();Targets.Register(instance,2,Quaternion.Euler(0,yaw,0));return instance;
        }
        public void Spawn(Vector3 p,bool air=false,float speed=2){Check(Enemies.TrySpawn(p,Vector3.zero,speed,20,air),"spawn");}
        public void Step(float dt=.1f){Effects.Tick(dt);Combat.Prepare(dt);Enemies.MoveAndIndex(dt);Combat.Attack(dt);}
        public void Steps(int n,float dt=.1f){for(int i=0;i<n;i++)Step(dt);}
        public float Hp(BuildingInstance b)=>b.Module<HealthModule>().Current;
        public void Dispose(){Combat.Dispose();Effects.Dispose();foreach(var b in buildings){Targets.Remove(b);b.Dispose();}foreach(var a in assets)UnityEngine.Object.DestroyImmediate(a);}
    }
    public static string Main(){
        using(var f=new Fixture()){
            var n=f.Add(BuildingCombatRole.Nexus,Vector3.zero,1000);f.Spawn(new Vector3(2,0,0));f.Steps(9);Check(f.Hp(n)==1000,"No hit before full second");f.Steps(20,0);Check(f.Hp(n)==1000,"Zero simulation time cannot damage");f.Step();Check(f.Hp(n)==999,"First hit after one second");f.Steps(20);Check(f.Hp(n)==997&&f.Enemies.Alive==1&&f.Enemies.Escaped==0,"One damage per second and no arrival removal");
            f.Effects.TryGet(new TargetHandle(0,f.Enemies.Generation(0)),out var target);((IWeaponDamageReceiver)target.Receiver).Inflict(WeaponStatus.Stun,2,1,0);f.Steps(19);Check(f.Hp(n)==997,"Stun prevents attack");
            f.Steps(11);Check(f.Hp(n)==996,"Resume full attack interval after stun");((IWeaponDamageReceiver)target.Receiver).Inflict(WeaponStatus.Slow,3,.8f,0);f.Steps(10);Check(f.Hp(n)==995,"Slow does not change attack interval");
            f.Enemies.Remove(0,true);f.Spawn(new Vector3(2,0,0));f.Steps(9);Check(f.Hp(n)==995,"Reused enemy slot resets timer");f.Step();Check(f.Hp(n)==994,"Reused slot attacks after full interval");
        }
        using(var f=new Fixture()){
            var wall=f.Add(BuildingCombatRole.Wall,Vector3.zero,2);var turret=f.Add(BuildingCombatRole.Defense,new Vector3(0,0,4),100);var nexus=f.Add(BuildingCombatRole.Nexus,new Vector3(0,0,12),1000);
            f.Spawn(new Vector3(0,0,-4),false,100);f.Step();Check(f.Enemies.GetEnemy(0).position.z<-1&&f.Hp(wall)==2&&f.Hp(turret)==100,"Large movement swept against wall; no approach damage");f.Steps(20);Check(wall.Disposed&&f.Hp(turret)==100,"Wall destroyed before turret takes damage");f.Steps(15);Check(f.Enemies.GetEnemy(0).position.z>1&&f.Hp(turret)<100&&f.Hp(nexus)==1000,"Advance through opened wall and reacquire defense");
        }
        using(var f=new Fixture()){
            var ordinary=f.Add(BuildingCombatRole.General,Vector3.zero,2);var nexus=f.Add(BuildingCombatRole.Nexus,new Vector3(0,0,8),1000);f.Spawn(new Vector3(0,0,-4),false,100);f.Steps(21);Check(ordinary.Disposed&&f.Hp(nexus)==1000,"General building blocks ground movement");
        }
        using(var f=new Fixture()){
            var wall=f.Add(BuildingCombatRole.Wall,Vector3.zero);var general=f.Add(BuildingCombatRole.General,new Vector3(0,0,3));var defense=f.Add(BuildingCombatRole.Defense,new Vector3(0,0,6));f.Add(BuildingCombatRole.Nexus,new Vector3(0,0,12),1000);
            f.Spawn(new Vector3(0,0,-3),true,100);f.Steps(12);Check(f.Hp(defense)<100&&f.Hp(general)==100&&f.Hp(wall)==100&&Mathf.Abs(f.Enemies.GetEnemy(0).position.y-5)<.001,"Air crosses walls and prioritizes defense, preserving altitude");defense.Destroy();f.Steps(15);Check(f.Hp(general)<100&&f.Hp(wall)==100,"Air reacquires general before nexus");
        }
        using(var f=new Fixture()){
            var a=f.Add(BuildingCombatRole.Defense,new Vector3(-4,0,0));var b=f.Add(BuildingCombatRole.Defense,new Vector3(4,0,0));Check(f.Targets.Nearby(Vector3.zero,10,false,false).Building==a,"Distance tie uses registration ID despite duplicate local IDs");
            f.Spawn(Vector3.zero,false,0);f.Step();var closer=f.Add(BuildingCombatRole.Defense,new Vector3(0,0,2));Check(f.Enemies.GetEnemy(0).destination.x<0,"Existing target retained");f.Step();Check(f.Enemies.GetEnemy(0).destination.x<0,"Closer newcomer cannot replace held target");a.Destroy();f.Steps(3);Check(f.Enemies.GetEnemy(0).destination.z>0||f.Enemies.GetEnemy(0).waitingForDestination,"Destroyed handle reacquires nearby target");
            var pending=f.Add(BuildingCombatRole.Defense,Vector3.zero,100,0,false);Check(f.Targets.Nearby(Vector3.zero,.1f,false,false)==null,"Inactive reservation cannot be targeted");
        }
        using(var f=new Fixture()){
            var a=f.Add(BuildingCombatRole.Nexus,Vector3.zero,1);var b=f.Add(BuildingCombatRole.Nexus,new Vector3(20,0,0),1000);f.Spawn(new Vector3(2,0,0),false,100);f.Steps(10);Check(a.Disposed,"Nexus can be destroyed");f.Steps(10);Check(f.Enemies.GetEnemy(0).position.x>10&&f.Enemies.Alive==1,"Another nexus becomes destination");b.Destroy();f.Steps(4);Check(f.Enemies.GetEnemy(0).waitingForDestination&&!f.Targets.HasNexus,"No nexus or nearby buildings: idle without despawn");
        }
        using(var f=new Fixture()){
            var wall=f.Add(BuildingCombatRole.Wall,Vector3.zero,100,45);Check(f.Targets.FirstBlocker(new Vector3(-4,0,0),new Vector3(4,0,0),out var hit,out float fraction)&&hit.Building==wall&&Mathf.Abs(fraction-(4-Mathf.Sqrt(2))/8)<.0001,"Rotated footprint segment intersection");
            f.Spawn(new Vector3(-4,0,0),false,100);f.Step();Check(f.Enemies.GetEnemy(0).position.x<-1.4,"Rotated body cannot be tunneled through");
        }
        using(var f=new Fixture()){
            var wall=f.Add(BuildingCombatRole.Wall,Vector3.zero,1);f.Spawn(new Vector3(2,0,0));f.Spawn(new Vector3(-2,0,0));f.Steps(10);Check(wall.Disposed&&f.Targets.Count==0&&f.Enemies.Alive==2,"Simultaneous attackers tolerate target destruction during attack loop");f.Steps(5);Check(f.Enemies.GetEnemy(0).waitingForDestination&&f.Enemies.GetEnemy(1).waitingForDestination,"Destroyed target cannot receive stale damage");
        }
        int samples=0;
        using(var f=new Fixture(32,p=>{samples++;return 0;})){
            f.Add(BuildingCombatRole.Nexus,Vector3.zero,1000);f.Spawn(new Vector3(20,0,0),false,0);f.Step();int initial=samples;f.Steps(20);Check(samples==initial,"Unchanged destination and stopped height cached");
        }
        using(var f=new Fixture()){
            var nexus=f.Add(BuildingCombatRole.Nexus,Vector3.zero,1000);f.Spawn(new Vector3(-20,0,0),false,1);f.Step();
            var wall=f.Add(BuildingCombatRole.Wall,new Vector3(-18,0,0));f.Steps(25);Check(f.Enemies.GetEnemy(0).position.x<-19&&f.Hp(wall)<100,"New wall invalidates cached clear bucket");
            wall.Destroy();f.Targets.Remove(nexus);f.Steps(5);Check(f.Enemies.GetEnemy(0).waitingForDestination,"Unregistration invalidates cached live target");
        }
        var legacy=new HordeEnemyWorld(null,false,1);legacy.TrySpawn(Vector3.zero,Vector3.zero);legacy.MoveAndIndex(.1f);Check(legacy.Alive==0&&legacy.Escaped==1,"Legacy arrival removal unchanged");
        return "PASS: first-hit delay, cadence/damage, arrival persistence, stun/slow, reused slots, swept/rotated blockers, wall/general destruction, air priority/height, stable IDs/targets, inactive reservations, nexus retarget/idle, legacy arrival.";
    }
}
