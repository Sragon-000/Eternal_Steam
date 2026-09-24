using System;
using UnityEngine;
using EternalSteam.Demo;
namespace EternalSteam.OpenWorld
{
    public sealed class EnemyBuildingCombat:IEnemyMovementConstraint,IDisposable
    {
        public const int Damage=1;
        struct State
        {
            public int Generation, Revision, CandidateFrame, SightRevision, EmptyRevision;
            public BuildingTarget Target;
            public float SearchAt, AttackTime;
            public bool InReach, DestinationKnown, SightKnown, CellKnown, CellClear;
            public float MinX,MinZ,MaxX,MaxZ;
        }
        readonly State[] states;
        readonly int[] attackCandidates;
        readonly HordeEnemyWorld enemies;
        readonly IBuildingTargetQuery buildings;
        readonly IBuildingTargetChanges changes;
        readonly IBuildingMovementCells movementCells;
        TileWorldGround.TraversalSnapshot traversal;
        readonly HordeTargetAdapter effects;
        readonly TileWorldGround terrain;
        readonly float interval, reach, radius, searchInterval;
        float time;
        int frame, candidateCount;

        public EnemyBuildingCombat(HordeEnemyWorld enemies,IBuildingTargetQuery buildings,HordeTargetAdapter effects,EnemyCombatSettings settings,TileWorldGround terrain=null)
        {
            if(settings!=null&&!settings.Valid)throw new ArgumentException("Valid enemy combat settings required.");
            this.enemies=enemies;this.buildings=buildings;changes=buildings as IBuildingTargetChanges;movementCells=buildings as IBuildingMovementCells;this.effects=effects;this.terrain=terrain;
            interval=settings?.AttackInterval??1;reach=settings?.AttackReach??1;radius=settings?.SearchRadius??10;searchInterval=settings?.SearchInterval??.25f;
            states=new State[enemies.MaxCount];attackCandidates=new int[enemies.MaxCount];
            enemies.SpawnedEnemy+=Spawn;enemies.MovementConstraint=this;
        }
        void Spawn(int id)=>states[id]=new State{Generation=enemies.Generation(id),SearchAt=time,CandidateFrame=-1};
        void Assign(ref State state,BuildingTarget target)
        {
            if(ReferenceEquals(state.Target,target))return;
            state.Target=target;state.Revision=changes?.Revision??-1;
            state.AttackTime=0;state.InReach=false;state.DestinationKnown=false;state.SightKnown=false;
        }
        bool Valid(ref State state)
        {
            if(state.Target==null||!state.Target.Alive)return false;
            if(changes!=null&&state.Revision==changes.Revision)return true;
            state.Revision=changes?.Revision??-1;
            return buildings.TryGet(state.Target.Handle,out _);
        }
        void Candidate(int id)
        {
            if(states[id].CandidateFrame==frame)return;
            states[id].CandidateFrame=frame;attackCandidates[candidateCount++]=id;
        }
        bool CanAct(int id)=>effects==null||effects.CanAct(id);
        void Stop(int id,ref State state)
        {
            if(!enemies.GetEnemy(id).waitingForDestination)enemies.SetDestination(id,null);
            state.DestinationKnown=false;
        }
        public void Prepare(float dt)
        {
            time+=dt;frame++;candidateCount=0;
            if(terrain!=null)traversal=terrain.CaptureTraversal();
            for(int i=0;i<states.Length;i++)
            {
                ref readonly var enemy=ref enemies.GetEnemy(i);if(!enemy.alive)continue;
                ref var state=ref states[i];if(state.Generation!=enemies.Generation(i))Spawn(i);
                bool valid=Valid(ref state);
                if(!valid){Assign(ref state,null);state.InReach=false;}
                var target=state.Target;
                if((!valid||target.Role==BuildingCombatRole.Nexus&&!state.InReach)&&time>=state.SearchAt)
                {
                    var preferred=buildings.Nearby(enemy.position,radius,enemy.air,enemy.air||!buildings.HasNexus);
                    if(preferred!=null)target=preferred;else if(!valid)target=buildings.NearestNexus(enemy.position);
                    Assign(ref state,target);valid=target!=null;
                    float phase=(i%31)*(searchInterval/31);
                    state.SearchAt=(Mathf.Floor((time-phase)/searchInterval)+1)*searchInterval+phase;
                }
                if(!valid||!CanAct(i)){state.AttackTime=0;state.InReach=false;Stop(i,ref state);continue;}
                float distance=target.DistanceSquared(enemy.position);
                state.InReach=distance<=reach*reach+.0001f;
                // Only actors already in range or capable of entering it this step need the post-move attack pass.
                float potentialReach=reach+enemy.speed*Mathf.Max(0,dt);
                if(distance<=potentialReach*potentialReach+.0001f)Candidate(i);
                if(state.InReach)Stop(i,ref state);
                else
                {
                    state.AttackTime=0;state.SightKnown=false;
                    // Static target geometry: the approach point stays valid along this straight segment.
                    if(!state.DestinationKnown){enemies.SetDestination(i,target.Approach(enemy.position,reach*.98f));state.DestinationKnown=true;}
                }
            }
        }
        static bool Inside(ref State s,Vector3 p)=>p.x>=s.MinX&&p.x<s.MaxX&&p.z>=s.MinZ&&p.z<s.MaxZ;
        public Vector3 Constrain(int id,Vector3 from,Vector3 proposed)
        {
            if(enemies.GetEnemy(id).air)return proposed;
            if(terrain!=null&&!traversal.HasClearRoute(from,proposed))return from;
            // A cached empty cell is conservative: the complete segment must remain inside it.
            if(changes!=null&&movementCells!=null){
                ref var state=ref states[id];
                if(!state.CellKnown||state.EmptyRevision!=changes.Revision||!Inside(ref state,from)){
                    state.CellClear=movementCells.TryGetClearCell(from,out var bounds);state.CellKnown=true;state.EmptyRevision=changes.Revision;
                    state.MinX=bounds.xMin;state.MinZ=bounds.yMin;state.MaxX=bounds.xMax;state.MaxZ=bounds.yMax;
                }
                if(state.CellClear&&Inside(ref state,proposed))return proposed;
            }
            if(buildings.FirstBlocker(from,proposed,out var blocker,out float fraction))
            {
                Assign(ref states[id],blocker);states[id].InReach=false;Candidate(id);
                float distance=Vector3.Distance(from,proposed);
                return Vector3.Lerp(from,proposed,Mathf.Max(0,fraction-.01f/Mathf.Max(.01f,distance)));
            }
            return proposed;
        }
        public void Attack(float dt)
        {
            for(int index=0;index<candidateCount;index++)
            {
                int i=attackCandidates[index];ref readonly var enemy=ref enemies.GetEnemy(i);
                if(!enemy.alive)continue;ref var state=ref states[i];
                if(!CanAct(i)||!Valid(ref state)){state.AttackTime=0;state.InReach=false;continue;}
                var target=state.Target;
                if(target.DistanceSquared(enemy.position)>reach*reach+.0001f){state.AttackTime=0;state.InReach=false;continue;}
                // Validate the obstruction when entering range, and immediately before every damage delivery.
                bool due=state.AttackTime+dt+.00001f>=interval;
                if((!state.SightKnown||changes==null||state.SightRevision!=changes.Revision||due)&&!enemy.air&&buildings.FirstBlocker(enemy.position,target.Closest(enemy.position),out var blocker,out _)&&!blocker.Handle.Equals(target.Handle))
                {Assign(ref state,blocker);Stop(i,ref state);continue;}
                state.SightKnown=true;state.SightRevision=changes?.Revision??-1;
                if(!state.InReach){state.InReach=true;state.AttackTime=0;continue;}
                state.AttackTime+=dt;
                while(state.AttackTime+.00001f>=interval)
                {
                    state.AttackTime=Mathf.Max(0,state.AttackTime-interval);
                    if(!buildings.TryGet(state.Target.Handle,out target))break;
                    target.Receiver.ApplyDamage(Damage);
                }
            }
        }
        public void Reset(){Array.Clear(states,0,states.Length);time=0;candidateCount=0;frame=0;}
        public void Dispose(){enemies.SpawnedEnemy-=Spawn;if(ReferenceEquals(enemies.MovementConstraint,this))enemies.MovementConstraint=null;}
    }
}
