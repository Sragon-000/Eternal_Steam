using System;
using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    public enum WeaponSchedule { Periodic, Sustained, Burst }
    public enum WeaponDelivery { Instant, Projectile, Pierce, Chain, Pulse }
    [CreateAssetMenu(menuName="Eternal Steam/Modules/Composed Weapon")]
    public sealed class WeaponModuleDefinition : BuildingModuleDefinition
    {
        public WeaponSchedule Schedule;
        public WeaponDelivery Delivery;
        public WeaponDamageSource Source;
        public TargetKind Targets=TargetKind.All;
        public float BaseDamage=100,DamageCoefficient=1,Range=20,Interval=1;
        [HideInInspector] public float Angle=360; // Serialized compatibility only; targeting is always radial.
        public float BurstDuration=5,Rest=2,Radius,ProjectileSpeed=30,ProjectileLifetime=12,PierceWidth=.6f;
        public int MaximumTargets=1,MaximumHits=3;
        public float JumpRange=8,JumpInterval=.5f,ChainDuration=5,Retention=.2f;
        public WeaponStatus Status;
        public float StatusDuration=3,StatusStrength=.2f,TickDamage=10,SameTargetSeconds=3;
        public override IBuildingModule CreateRuntime()=>new WeaponRuntime(new WeaponSettings(this));
        public override void Validate(List<string> errors)
        {
            foreach(float v in new[]{BaseDamage,DamageCoefficient,Range,Angle,Interval,BurstDuration,Rest,Radius,ProjectileSpeed,ProjectileLifetime,PierceWidth,JumpRange,JumpInterval,ChainDuration,Retention,StatusDuration,StatusStrength,TickDamage,SameTargetSeconds})
                if(!float.IsFinite(v)||v<0){errors.Add("Weapon values must be finite and nonnegative.");break;}
            if(Range<=0||Angle>360||Interval<=0||MaximumTargets<1||MaximumTargets>100000||MaximumHits<1||Targets==0||Retention>1||StatusStrength>1)errors.Add("Invalid weapon configuration.");
            if(!Enum.IsDefined(typeof(WeaponSchedule),Schedule)||!Enum.IsDefined(typeof(WeaponDelivery),Delivery)||!Enum.IsDefined(typeof(WeaponDamageSource),Source)||!Enum.IsDefined(typeof(WeaponStatus),Status)||(Targets&~TargetKind.All)!=0)errors.Add("Unknown weapon policy.");
            if(Delivery==WeaponDelivery.Projectile&&(ProjectileSpeed<=0||ProjectileLifetime<=0))errors.Add("Projectile needs speed/lifetime.");
            if(Delivery==WeaponDelivery.Chain&&(JumpInterval<=0||ChainDuration<=0||JumpRange<=0))errors.Add("Chain needs bounded duration and jump interval.");
            if(Schedule==WeaponSchedule.Burst&&BurstDuration<=0)errors.Add("Burst needs duration.");
        }
    }
    public sealed class WeaponSettings
    {
        public readonly WeaponSchedule Schedule;public readonly WeaponDelivery Delivery;public readonly WeaponDamageSource Source;
        public readonly TargetKind Targets;public readonly WeaponStatus Status;
        public readonly float BaseDamage,Coefficient,Range,Angle,Interval,BurstDuration,Rest,Radius,Speed,Lifetime,Width,JumpRange,JumpInterval,ChainDuration,Retention,StatusDuration,Strength,TickDamage,SameTargetSeconds;
        public readonly int MaximumTargets,MaximumHits;
        public WeaponSettings(WeaponModuleDefinition d){Schedule=d.Schedule;Delivery=d.Delivery;Source=d.Source;Targets=d.Targets;Status=d.Status;BaseDamage=d.BaseDamage;Coefficient=d.DamageCoefficient;Range=d.Range;Angle=d.Angle;Interval=d.Interval;BurstDuration=d.BurstDuration;Rest=d.Rest;Radius=d.Radius;Speed=d.ProjectileSpeed;Lifetime=d.ProjectileLifetime;Width=d.PierceWidth;JumpRange=d.JumpRange;JumpInterval=d.JumpInterval;ChainDuration=d.ChainDuration;Retention=d.Retention;StatusDuration=d.StatusDuration;Strength=d.StatusStrength;TickDamage=d.TickDamage;SameTargetSeconds=d.SameTargetSeconds;MaximumTargets=d.MaximumTargets;MaximumHits=d.MaximumHits;}
    }
    public sealed class WeaponRuntime : IBuildingModule,ICombatModule,IAttackControl, IUnpoweredCombat, IPendingExecution
    {
        readonly WeaponSettings config;BuildingInstance owner;ITargetQuery query;IPerformanceScaling scaling;
        readonly List<TargetInfo> candidates=new(),effects=new(),targets=new(),piercing=new();
        readonly ProjectionOrder projection=new();
        sealed class ProjectionOrder:IComparer<TargetInfo>{public Vector3 Origin,Direction;public int Compare(TargetInfo a,TargetInfo b){int order=Vector3.Dot(a.Position-Origin,Direction).CompareTo(Vector3.Dot(b.Position-Origin,Direction));return order!=0?order:a.Handle.Id.CompareTo(b.Handle.Id);}}
        struct Flight{public TargetHandle Target;public Vector3 Position;public float Age,Damage;}
        struct Chain{public TargetHandle Last;public Vector3 Position;public float Age,Next,Damage;public int Hops;}
        readonly List<Flight> flights=new();readonly List<Chain> chains=new();
        TargetHandle? current;[Saved(0)] float cooldown,tracked,burstTime;[Saved] bool firing=true;
        public bool HasPendingExecution=>flights.Count>0||chains.Count>0;
        public float Damage=>Up(config.BaseDamage*config.Coefficient);
        public float Interval=>Mathf.Max(.01f,Down(config.Interval));
        public float Range=>Up(config.Range);public float Angle=>360;
        [Saved] public TargetKind Kinds{get;set;}
        public WeaponRuntime(WeaponSettings config){this.config=config;Kinds=config.Targets;}
        float Up(float x)=>scaling?.Increase(x)??x;float Down(float x)=>scaling?.Decrease(x)??x;int Count(int x)=>scaling?.Count(x)??x;
        public void Initialize(BuildingInstance owner,BuildingServices services){this.owner=owner;query=services.Targets??throw new InvalidOperationException("Weapon requires targets.");scaling=owner.Module<IPerformanceScaling>();owner.DirectionChanged+=Clear;
            if(query is ITargetQueryCapacity bounded){int capacity=bounded.Capacity;if(config.Delivery!=WeaponDelivery.Pulse)candidates.Capacity=capacity;if(config.Delivery!=WeaponDelivery.Instant||config.Radius>0)effects.Capacity=capacity;if(config.Delivery==WeaponDelivery.Pierce)piercing.Capacity=capacity;}
            targets.Capacity=Mathf.Min(100000,config.MaximumTargets*2);}
        void Clear(){current=null;tracked=0;}
        public void Activate(){}
        bool Valid(TargetInfo t)=>Sector.Contains(owner.Position,owner.Direction,Range,Angle,Kinds,t);
        bool Find(out TargetInfo target)
        {
            if(current.HasValue&&query.TryGet(current.Value,out target)&&Valid(target))return true;
            Clear();query.Query(owner.Position,Range,candidates);float best=float.PositiveInfinity;target=default;bool found=false;
            foreach(var c in candidates){if(!Valid(c))continue;var d=c.Position-owner.Position;d.y=0;float sq=d.sqrMagnitude;if(sq>best||(sq==best&&found&&c.Handle.Id>=target.Handle.Id))continue;best=sq;target=c;found=true;}
            if(found)current=target.Handle;return found;
        }
        public void TickUnpowered(float dt){TickFlights(dt);TickChains(dt);}
        public void Tick(float dt)
        {
            if(!float.IsFinite(dt)||dt<=0)return;
            float step=Mathf.Max(.01f,Mathf.Min(.05f,Down(config.Interval)));
            while(dt>.000001f){float part=Mathf.Min(step,dt);Advance(part);dt-=part;}
        }
        void Advance(float dt)
        {
            TickFlights(dt);TickChains(dt);
            cooldown=Mathf.Max(0,cooldown-dt);
            if(config.Schedule==WeaponSchedule.Burst){float elapsed=burstTime;burstTime+=dt;if(firing&&elapsed+.00001f>=Up(config.BurstDuration)){firing=false;burstTime=0;cooldown=Down(config.Rest);}if(!firing){if(cooldown>.00001f)return;firing=true;burstTime=0;}}
            if(config.Delivery==WeaponDelivery.Pulse){if(cooldown<=.00001f){query.Query(owner.Position,Range,candidates);bool found=false;foreach(var candidate in candidates)if(Valid(candidate)){found=true;break;}if(!found)return;Impact(owner.Position,default,0);cooldown=Down(config.Interval);}return;}
            var old=current;if(!Find(out var target))return;if(owner.Module<ITurretRotation>() is ITurretRotation rotation){if(!rotation.AimAt(target.Position,dt))return;}else owner.ReportAim(target.Position);if(old.HasValue&&old.Value.Equals(target.Handle))tracked+=dt;
            if(cooldown>.00001f)return;cooldown=Mathf.Max(.01f,Down(config.Interval));
            targets.Clear();targets.Add(target);
            if(Count(config.MaximumTargets)>1){query.Query(owner.Position,Range,candidates);while(targets.Count<Count(config.MaximumTargets)){
                bool found=false;TargetInfo next=default;float best=float.PositiveInfinity;
                foreach(var c in candidates){if(!Valid(c))continue;bool used=false;foreach(var t in targets)if(t.Handle.Equals(c.Handle)){used=true;break;}if(used)continue;var d=c.Position-owner.Position;d.y=0;float sq=d.sqrMagnitude;if(sq>best||(sq==best&&found&&c.Handle.Id>=next.Handle.Id))continue;best=sq;next=c;found=true;}
                if(!found)break;targets.Add(next);
            }}
            foreach(var t in targets)Fire(t,Up(config.BaseDamage*config.Coefficient));
        }
        void Fire(TargetInfo target,float damage)
        {
            switch(config.Delivery){
                case WeaponDelivery.Projectile:flights.Add(new Flight{Target=target.Handle,Position=owner.Position+Vector3.up,Damage=damage});break;
                case WeaponDelivery.Chain:Hit(target,damage);chains.Add(new Chain{Last=target.Handle,Position=target.Position,Damage=damage,Next=Down(config.JumpInterval)});break;
                case WeaponDelivery.Pierce:
                    var direction=target.Position-owner.Position;direction.y=0;direction.Normalize();query.Query(owner.Position,Range,effects);
                    piercing.Clear();
                    foreach(var t in effects){if(!Valid(t))continue;var delta=t.Position-owner.Position;delta.y=0;float along=Vector3.Dot(delta,direction);if(along>=0&&along<=Range&&(delta-direction*along).sqrMagnitude<=Up(config.Width)*Up(config.Width)*.25f)piercing.Add(t);}
                    projection.Origin=owner.Position;projection.Direction=direction;piercing.Sort(projection);
                    for(int n=0;n<Mathf.Min(Count(config.MaximumHits),piercing.Count);n++)Hit(piercing[n],damage);
                    break;
                default:Impact(target.Position,target,damage);break;
            }
        }
        void Hit(TargetInfo target,float damage)
        {
            if(!query.TryGet(target.Handle,out target))return;
            if(target.Receiver is IWeaponDamageReceiver receiver){receiver.Receive(damage,config.Source);
                if(config.Status!=WeaponStatus.None&&(config.Schedule!=WeaponSchedule.Sustained||tracked>=Down(config.SameTargetSeconds)))receiver.Inflict(config.Status,Up(config.StatusDuration),Mathf.Min(1,Up(config.Strength)),Up(config.TickDamage));
            }else if(damage>0)target.Receiver.ApplyDamage(damage);
            owner.ReportShot(target.Position);
        }
        void Impact(Vector3 position,TargetInfo target,float damage)
        {
            float radius=config.Delivery==WeaponDelivery.Pulse?Range:Up(config.Radius);
            if(radius<=0){Hit(target,damage);return;}
            query.Query(position,radius,effects);foreach(var t in effects)if((t.Kind&Kinds)!=0)Hit(t,damage);
        }
        void TickFlights(float dt)
        {
            for(int i=flights.Count-1;i>=0;i--){var f=flights[i];f.Age+=dt;if(f.Age>config.Lifetime||!query.TryGet(f.Target,out var target)){flights.RemoveAt(i);continue;}f.Position=Vector3.MoveTowards(f.Position,target.Position,Up(config.Speed)*dt);owner.ReportProjectile(f.Position);if((f.Position-target.Position).sqrMagnitude<.001f){Impact(target.Position,target,f.Damage);flights.RemoveAt(i);}else flights[i]=f;}
        }
        void TickChains(float dt)
        {
            for(int i=chains.Count-1;i>=0;i--){var c=chains[i];c.Age+=dt;c.Next-=dt;if(c.Age>Up(config.ChainDuration)||c.Hops>=64){chains.RemoveAt(i);continue;}if(c.Next>.00001f){chains[i]=c;continue;}query.Query(c.Position,Up(config.JumpRange),effects);bool found=false;float best=float.PositiveInfinity;TargetInfo next=default;
                foreach(var t in effects){if(t.Handle.Equals(c.Last)||(t.Kind&Kinds)==0||!t.Receiver.Alive)continue;var d=t.Position-c.Position;d.y=0;float sq=d.sqrMagnitude;if(sq>best||(sq==best&&found&&t.Handle.Id>=next.Handle.Id))continue;best=sq;found=true;next=t;}
                if(!found){chains.RemoveAt(i);continue;}c.Damage*=config.Retention;Hit(next,c.Damage);c.Last=next.Handle;c.Position=next.Position;c.Hops++;c.Next+=Mathf.Max(.01f,Down(config.JumpInterval));chains[i]=c;
            }
        }
        public void Dispose(){if(owner!=null)owner.DirectionChanged-=Clear;flights.Clear();chains.Clear();Clear();}
    }
}
