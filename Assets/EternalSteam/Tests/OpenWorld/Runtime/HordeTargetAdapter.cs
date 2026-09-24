using System.Collections.Generic;
using EternalSteam.Demo;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    public sealed class HordeTargetAdapter : ITargetQuery,ITargetQueryCapacity,System.IDisposable
    {
        readonly HordeEnemyWorld world;readonly List<int> indices;readonly Receiver[] receivers;
        readonly Dictionary<int,Receiver> affected;readonly List<int> expired;
        float time;
        public HordeTargetAdapter(HordeEnemyWorld world){this.world=world;receivers=new Receiver[world.MaxCount];expired=new List<int>(world.MaxCount);indices=new List<int>(world.MaxCount);affected=new Dictionary<int,Receiver>(world.MaxCount);world.SpawnedEnemy+=OnSpawn;}
        public int Capacity=>world.MaxCount;
        public bool CanAct(int id)=>world.GetEnemy(id).alive&&(receivers[id]==null||receivers[id].CanAct);
        void OnSpawn(int id){receivers[id]=new Receiver(this,id,world.Generation(id));}
        public void Dispose(){world.SpawnedEnemy-=OnSpawn;Reset();}
        Receiver Get(int id){var r=receivers[id];if(r==null||r.Generation!=world.Generation(id))receivers[id]=r=new Receiver(this,id,world.Generation(id));return r;}
        public bool TryGet(TargetHandle handle,out TargetInfo target)
        {
            target=default;if(handle.Id<0||handle.Id>=world.MaxCount||world.Generation(handle.Id)!=handle.Generation||!world.GetEnemy(handle.Id).alive)return false;
            var e=world.GetEnemy(handle.Id);target=new TargetInfo(handle,e.position,e.air?TargetKind.Air:TargetKind.Ground,Get(handle.Id));return true;
        }
        public void Query(Vector3 origin,float range,List<TargetInfo> results)
        {results.Clear();world.QueryIndices(origin,range,indices);foreach(int id in indices)if(TryGet(new TargetHandle(id,world.Generation(id)),out var t))results.Add(t);}
        public void Tick(float dt)
        {
            time+=dt;expired.Clear();foreach(var pair in affected){var r=pair.Value;if(!r.Alive){expired.Add(pair.Key);continue;}
                while(r.NextBurn<=time&&r.NextBurn<=r.BurnUntil){r.Receive(r.BurnDamage,WeaponDamageSource.Normal);r.NextBurn+=1;}
                float penalty=r.StunUntil>time?1:Mathf.Max(r.SlowUntil>time?r.Slow:0,r.OverheatUntil>time?r.HeatSlow:0);
                world.SetMovementPenalty(pair.Key,penalty);
                if(r.BurnUntil<time&&r.StunUntil<=time&&r.SlowUntil<=time&&r.OverheatUntil<=time)expired.Add(pair.Key);
            }foreach(int id in expired)affected.Remove(id);
        }
        public void Reset(){foreach(var r in affected.Values)if(r.Alive)world.SetMovementPenalty(r.Id,0);affected.Clear();time=0;System.Array.Clear(receivers,0,receivers.Length);}
        sealed class Receiver : IWeaponDamageReceiver
        {
            readonly HordeTargetAdapter host;public readonly int Id,Generation;float fraction;
            public float BurnUntil=-1,NextBurn=float.PositiveInfinity,BurnDamage,StunUntil,SlowUntil,Slow,OverheatUntil,HeatSlow,HeatBonus;
            public Receiver(HordeTargetAdapter host,int id,int generation){this.host=host;Id=id;Generation=generation;}
            public bool CanAct=>Alive&&StunUntil<=host.time;
            public bool Alive=>host.world.Generation(Id)==Generation&&host.world.GetEnemy(Id).alive;
            public void ApplyDamage(float amount)=>Receive(amount,WeaponDamageSource.Normal);
            public void Receive(float damage,WeaponDamageSource source)
            {if(!Alive||!float.IsFinite(damage)||damage<=0)return;if(source==WeaponDamageSource.PlasmaLaser&&OverheatUntil>host.time)damage*=1+HeatBonus;fraction+=damage;int whole=Mathf.FloorToInt(fraction);if(whole>0){fraction-=whole;host.world.ApplyDamage(Id,whole);}}
            public void Inflict(WeaponStatus status,float duration,float strength,float tickDamage)
            {
                if(!Alive||duration<=0)return;float until=host.time+duration;
                switch(status){case WeaponStatus.Overheat:if(OverheatUntil<=host.time){HeatSlow=0;HeatBonus=0;}OverheatUntil=Mathf.Max(OverheatUntil,until);HeatSlow=Mathf.Max(HeatSlow,strength);HeatBonus=Mathf.Max(HeatBonus,strength);break;
                    case WeaponStatus.Burn:if(BurnUntil<=host.time){NextBurn=host.time+1;BurnDamage=0;}BurnUntil=Mathf.Max(BurnUntil,until);BurnDamage=Mathf.Max(BurnDamage,tickDamage);break;
                    case WeaponStatus.Stun:StunUntil=Mathf.Max(StunUntil,until);break;
                    case WeaponStatus.Slow:if(SlowUntil<=host.time)Slow=0;SlowUntil=Mathf.Max(SlowUntil,until);Slow=Mathf.Max(Slow,strength);break;default:return;}
                host.affected[Id]=this;
                host.world.SetMovementPenalty(Id,StunUntil>host.time?1:Mathf.Max(SlowUntil>host.time?Slow:0,OverheatUntil>host.time?HeatSlow:0));
            }
        }
    }
}
