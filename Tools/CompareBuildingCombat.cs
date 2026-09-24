using System;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Unity.Profiling;
using EternalSteam;
using EternalSteam.Demo;
using EternalSteam.OpenWorld;
public static class CompareBuildingCombat
{
 public static string Run1()=>Measure(4000);
 public static string Run2()=>Measure(10000);
 public static string Run3()=>Measure(100000);
 public static string Main()=>Measure(4000);
 static string Measure(int population){
  var report=new StringBuilder("CPU only, flat terrain, no rendering/UI; 101 static buildings, mixed ground/air; PAIRED same compiler/runtime, 10 warmup + 30 frames, dt=1/60. GC.Alloc marker event count (NOT duration as bytes).\n");
  using(var probe=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC.Alloc",16,ProfilerRecorderOptions.StartImmediately|ProfilerRecorderOptions.CollectOnlyOnCurrentThread)){
   var allocation=new byte[16384];GC.KeepAlive(allocation);probe.Stop();if(!probe.Valid||probe.Count==0)throw new Exception("GC.Alloc positive control failed");report.AppendLine("GC.Alloc positive control: "+probe.Count+" event(s)");
  }
  for(int repeat=1;repeat<=2;repeat++){
   int count=population;
   var registry=new BuildingTargetRegistry();var assets=new List<UnityEngine.Object>();var buildings=new List<BuildingInstance>();
   var health=ScriptableObject.CreateInstance<HealthModuleDefinition>();health.Maximum=1e9f;assets.Add(health);
   for(int i=0;i<101;i++){
    var d=ScriptableObject.CreateInstance<BuildingDefinition>();var body=ScriptableObject.CreateInstance<BuildingCombatDefinition>();assets.Add(d);assets.Add(body);d.Footprint=Vector2Int.one;body.Role=i==100?BuildingCombatRole.Nexus:i%3==0?BuildingCombatRole.Wall:i%3==1?BuildingCombatRole.Defense:BuildingCombatRole.General;d.Modules.Add(health);d.Modules.Add(body);
    var b=new BuildingInstance(i,d,default,i==100?Vector3.zero:new Vector3((i%10)*16-72,0,(i/10)*16-72),new BuildingServices(null));b.Activate();registry.Register(b,2,Quaternion.Euler(0,45,0));buildings.Add(b);
   }
   var world=new HordeEnemyWorld(null,false,count,new Rect(-200,-200,400,400)){ArrivalPolicy=EnemyArrivalPolicy.Remain};
   using(var effects=new HordeTargetAdapter(world)){
    IDisposable owner;Action<float> prepareStep,attackStep;
    if(repeat==1){var c=new BaselineEnemyBuildingCombat(world,registry,effects,null);owner=c;prepareStep=c.Prepare;attackStep=c.Attack;}
    else{var c=new CandidateEnemyBuildingCombat(world,registry,effects,null);owner=c;prepareStep=c.Prepare;attackStep=c.Attack;}
    using(owner){
    int side=Mathf.CeilToInt(Mathf.Sqrt(count));for(int i=0;i<count;i++){var p=new Vector3((i%side-side*.5f)*.75f,0,(i/side-side*.5f)*.75f);world.TrySpawn(p,Vector3.zero,2.4f,1000,i%2==0);}
    const float dt=1f/60;for(int i=0;i<10;i++){effects.Tick(dt);prepareStep(dt);world.MoveAndIndex(dt);attackStep(dt);}
    var prepare=new double[30];var move=new double[30];var attack=new double[30];var total=new double[30];var timer=new Stopwatch();int gc=GC.CollectionCount(0);int allocEvents;
    using(var alloc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC.Alloc",65536,ProfilerRecorderOptions.StartImmediately|ProfilerRecorderOptions.CollectOnlyOnCurrentThread)){
     for(int i=0;i<30;i++){
      timer.Restart();effects.Tick(dt);prepareStep(dt);timer.Stop();prepare[i]=timer.Elapsed.TotalMilliseconds;
      timer.Restart();world.MoveAndIndex(dt);timer.Stop();move[i]=timer.Elapsed.TotalMilliseconds;
      timer.Restart();attackStep(dt);timer.Stop();attack[i]=timer.Elapsed.TotalMilliseconds;total[i]=prepare[i]+move[i]+attack[i];
     }
     alloc.Stop();allocEvents=alloc.Count;
    }
    Array.Sort(prepare);Array.Sort(move);Array.Sort(attack);Array.Sort(total);
    report.AppendLine($"variant {repeat} (1=before,2=after), {count}: prepare {prepare[15]:F2}/{prepare[28]:F2}, move+sweep {move[15]:F2}/{move[28]:F2}, attack {attack[15]:F2}/{attack[28]:F2}, TOTAL {total[15]:F2}/{total[28]:F2} ms median/p95; GC.Alloc {allocEvents} events, GC0 {GC.CollectionCount(0)-gc}");
    if(allocEvents>0)report.AppendLine("FAIL: measured loop allocated; investigate before acceptance.");
   }
   }
   foreach(var b in buildings){registry.Remove(b);b.Dispose();}foreach(var a in assets)UnityEngine.Object.DestroyImmediate(a);
  }
  System.IO.File.WriteAllText("/private/tmp/building-combat-paired-"+population+".txt",report.ToString());
  return report.ToString();
 }
}

namespace EternalSteam.OpenWorld
{
    public sealed class BaselineEnemyBuildingCombat:IEnemyMovementConstraint,IDisposable
    {
        public const int Damage=1;
        struct State {public int Generation;public BuildingTargetHandle Target;public float SearchAt,AttackTime;public bool InReach;}
        readonly State[] states;readonly HordeEnemyWorld enemies;readonly IBuildingTargetQuery buildings;readonly HordeTargetAdapter effects;readonly TileWorldGround terrain;
        readonly float interval,reach,radius,searchInterval;float time;
        public BaselineEnemyBuildingCombat(HordeEnemyWorld enemies,IBuildingTargetQuery buildings,HordeTargetAdapter effects,EnemyCombatSettings settings,TileWorldGround terrain=null){
            if(settings!=null&&!settings.Valid)throw new ArgumentException("Valid enemy combat settings required.");
            this.enemies=enemies;this.buildings=buildings;this.effects=effects;this.terrain=terrain;
            interval=settings?.AttackInterval??1;reach=settings?.AttackReach??1;radius=settings?.SearchRadius??10;searchInterval=settings?.SearchInterval??.25f;states=new State[enemies.MaxCount];
            enemies.SpawnedEnemy+=Spawn;enemies.MovementConstraint=this;
        }
        void Spawn(int id){states[id]=new State{Generation=enemies.Generation(id),SearchAt=time};}
        void Assign(ref State state,BuildingTarget target){var next=target?.Handle??default;if(state.Target.Equals(next))return;state.Target=next;state.AttackTime=0;state.InReach=false;}
        bool CanAct(int id)=>effects==null||effects.CanAct(id);
        public void Prepare(float dt){
            time+=dt;
            for(int i=0;i<states.Length;i++){
                ref readonly var enemy=ref enemies.GetEnemy(i);if(!enemy.alive)continue;ref var state=ref states[i];if(state.Generation!=enemies.Generation(i))Spawn(i);
                bool valid=buildings.TryGet(state.Target,out var target);
                if(!valid){Assign(ref state,null);state.InReach=false;}
                if((!valid||target.Role==BuildingCombatRole.Nexus&&!state.InReach)&&time>=state.SearchAt){
                    var preferred=buildings.Nearby(enemy.position,radius,enemy.air,enemy.air||!buildings.HasNexus);
                    if(preferred!=null)target=preferred;else if(!valid)target=buildings.NearestNexus(enemy.position);
                    Assign(ref state,target);valid=target!=null;
                    // Stable phases prevent all slots repeatedly querying on one frame after a burst spawn.
                    float phase=(i%31)*(searchInterval/31);state.SearchAt=(Mathf.Floor((time-phase)/searchInterval)+1)*searchInterval+phase;
                }
                if(!valid||!CanAct(i)){state.AttackTime=0;state.InReach=false;enemies.SetDestination(i,null);continue;}
                state.InReach=target.DistanceSquared(enemy.position)<=reach*reach+.0001f;
                if(!state.InReach)state.AttackTime=0;
                enemies.SetDestination(i,state.InReach?(Vector3?)null:target.Approach(enemy.position,reach*.98f));
            }
        }
        public Vector3 Constrain(int id,Vector3 from,Vector3 proposed){
            if(enemies.GetEnemy(id).air)return proposed;
            if(terrain!=null&&!terrain.HasClearRoute(from,proposed))return from;
            if(buildings.FirstBlocker(from,proposed,out var blocker,out float fraction)){
                Assign(ref states[id],blocker);states[id].InReach=false;
                float distance=Vector3.Distance(from,proposed);return Vector3.Lerp(from,proposed,Mathf.Max(0,fraction-.01f/Mathf.Max(.01f,distance)));
            }return proposed;
        }
        public void Attack(float dt){
            for(int i=0;i<states.Length;i++){
                ref readonly var enemy=ref enemies.GetEnemy(i);if(!enemy.alive)continue;ref var state=ref states[i];
                if(!CanAct(i)||!buildings.TryGet(state.Target,out var target)){state.AttackTime=0;state.InReach=false;continue;}
                if(target.DistanceSquared(enemy.position)>reach*reach+.0001f){state.AttackTime=0;state.InReach=false;continue;}
                if(!enemy.air&&buildings.FirstBlocker(enemy.position,target.Closest(enemy.position),out var blocker,out _)&&!blocker.Handle.Equals(target.Handle)){
                    Assign(ref state,blocker);enemies.SetDestination(i,null);continue;
                }
                // Reaching the edge during this movement step does not count that entire step as attack time.
                if(!state.InReach){state.InReach=true;state.AttackTime=0;continue;}
                state.AttackTime+=dt;
                while(state.AttackTime+.00001f>=interval){state.AttackTime=Mathf.Max(0,state.AttackTime-interval);if(!buildings.TryGet(state.Target,out target))break;target.Receiver.ApplyDamage(Damage);}
            }
        }
        public void Reset(){Array.Clear(states,0,states.Length);time=0;}
        public void Dispose(){enemies.SpawnedEnemy-=Spawn;if(ReferenceEquals(enemies.MovementConstraint,this))enemies.MovementConstraint=null;}
    }
}

namespace EternalSteam.OpenWorld
{
    public sealed class CandidateEnemyBuildingCombat:IEnemyMovementConstraint,IDisposable
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

        public CandidateEnemyBuildingCombat(HordeEnemyWorld enemies,IBuildingTargetQuery buildings,HordeTargetAdapter effects,EnemyCombatSettings settings,TileWorldGround terrain=null)
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
