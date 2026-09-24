using System.Collections.Generic;
using UnityEngine;
namespace EternalSteam
{
    [CreateAssetMenu(menuName = "Eternal Steam/Attacks/Homing Projectile")]
    public sealed class ProjectileAttackDefinition : AttackExecutionDefinition
    {
        public float Speed = 12;
        public float Lifetime = 10;
        public override IAttackExecution CreateRuntime() => new Runtime(Speed,Lifetime);
        public override void Validate(List<string> errors)
        { if (!float.IsFinite(Speed) || Speed<=0 || !float.IsFinite(Lifetime) || Lifetime<=0) errors.Add("Projectile speed/lifetime must be positive and finite."); }
        sealed class Runtime : IAttackExecution,IPendingExecution
        {
            struct Flight { public AttackContext Context; public TargetHandle Target; public Vector3 Position; public float Damage, Age; }
            public bool HasPendingExecution=>flights.Count>0;
            readonly float speed,lifetime; readonly List<Flight> flights=new();
            public Runtime(float speed,float lifetime) { this.speed=speed; this.lifetime=lifetime; }
            public void Execute(AttackContext context,TargetInfo target,float damage)
            { flights.Add(new Flight { Context=context,Target=target.Handle,Position=context.Owner.Position,Damage=damage }); }
            public void Tick(float dt)
            {
                for (int i=flights.Count-1;i>=0;i--)
                {
                    var flight=flights[i]; flight.Age+=dt;
                    if (flight.Age>lifetime || !flight.Context.Query.TryGet(flight.Target,out var target)) { flights.RemoveAt(i); continue; }
                    flight.Position=Vector3.MoveTowards(flight.Position,target.Position,speed*dt);
                    flight.Context.Owner.ReportProjectile(flight.Position);
                    if ((flight.Position-target.Position).sqrMagnitude<0.000001f) { flight.Context.Hit(target,flight.Damage); flights.RemoveAt(i); }
                    else flights[i]=flight;
                }
            }
            public void Dispose() => flights.Clear();
        }
    }
}
