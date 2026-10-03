using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using EternalSteam;
using EternalSteam.OpenWorld;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Editor-only observation host. No inventory/clock/health/placement mutations.
public static class BalanceRecorder
{
 const string Key="EternalSteam.BalanceRun";
 static MonoBehaviour Observer()=>Resources.FindObjectsOfTypeAll<MonoBehaviour>().FirstOrDefault(v=>v!=null&&v.GetType().Name=="BalanceRunObserver"&&v.gameObject.scene.IsValid());
 static object Call(MonoBehaviour recorder,string method,params object[] args)=>recorder.GetType().GetMethod(method).Invoke(recorder,args);
 public static string Describe()=>"Ledger events + lifecycle/damage/enemy events + 1-second snapshots + milestone screenshots; normal campaign only.";
 public static string Setup(string runName,string purpose="normal-survival")
 {
  if(purpose!="normal-survival"&&purpose!="recorder-integration")throw new Exception("Unknown recording purpose");
  if(string.IsNullOrWhiteSpace(runName)||runName.Any(c=>!char.IsLetterOrDigit(c)&&c!='-'))throw new Exception("Use a unique letters/digits/hyphens run name");
  var scene=SceneManager.GetActiveScene();var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
  if(Application.isPlaying||scene.isDirty||SceneManager.sceneCount!=1||s==null||s.StartingBase==null||!s.BasePlanningRules)throw new Exception("A clean current campaign scene is required. No scene will be opened automatically.");
  string root=Path.GetFullPath("Docs/Measurements/Balance/"+runName);if(Directory.Exists(root))throw new Exception("Run already exists; never overwrite a run");
  Directory.CreateDirectory(root);Directory.CreateDirectory(root+"/screenshots");
  SessionState.SetString(Key,root);SessionState.SetString(Key+".Scene",scene.path);SessionState.SetBool(Key+".Paused",EditorApplication.isPaused);SessionState.SetBool(Key+".Background",Application.runInBackground);SessionState.SetString(Key+".SaveRoot",Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")??"");
  Environment.SetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT",root+"/save");Application.runInBackground=true;EditorApplication.isPaused=true;
  var catalog=new JArray(s.ContentCatalog.Buildings.Select(d=>new JObject{{"id",d.Id},{"name",d.DisplayName},{"asset",AssetDatabase.GetAssetPath(d)},{"placement",d.Placement==null?null:JObject.Parse(JsonUtility.ToJson(d.Placement))},{"placementAsset",AssetDatabase.GetAssetPath(d.Placement)},{"definition",JObject.Parse(JsonUtility.ToJson(d))},{"modules",new JArray(d.Modules.Select(m=>new JObject{{"type",m.GetType().FullName},{"asset",AssetDatabase.GetAssetPath(m)},{"values",JObject.Parse(JsonUtility.ToJson(m))}}))}}));
  var metadata=new JObject{{"run",runName},{"utc",DateTime.UtcNow.ToString("O")},{"scene",scene.path},{"unity",Application.unityVersion},{"normalPlay",true},{"daySeconds",s.DayDurationSeconds},{"nightSeconds",s.NightDurationSeconds},{"constructionPolicy",s.ConstructionCosts==null?"verification-free":AssetDatabase.GetAssetPath(s.ConstructionCosts)},{"upgradePolicy",s.UpgradeCosts==null?"verification-free":AssetDatabase.GetAssetPath(s.UpgradeCosts)},{"assault",JObject.Parse(JsonUtility.ToJson(s.AssaultSettings))},{"catalog",catalog},{"status","prepared-not-started"}};
  var hud=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();
  metadata["hudCatalog"]=new JArray(hud.Catalog.Select(c=>new JObject{{"definition",c.Definition!=null?c.Definition.Id:null},{"tool",c.Tool.ToString()},{"kind",c.Kind.ToString()},{"category",c.Category.ToString()}}));
  metadata["regions"]=new JArray(s.Regions.Select(r=>new JObject{{"asset",AssetDatabase.GetAssetPath(r)},{"values",JObject.Parse(JsonUtility.ToJson(r))}}));
  metadata["enemyCombat"]=JObject.Parse(JsonUtility.ToJson(s.EnemyCombat));
  using(var hash=System.Security.Cryptography.SHA256.Create())metadata["sourceHashes"]=new JArray(Directory.GetFiles("Assets/EternalSteam","*.cs",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>new JObject{{"path",p},{"sha256",BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(p))).Replace("-","").ToLowerInvariant()}}));
  metadata["purpose"]=purpose;metadata["normalPlay"]=purpose=="normal-survival";
  File.WriteAllText(root+"/configuration.json",metadata.ToString());return root;
 }
 public static string Start()
 {
  var root=SessionState.GetString(Key,"");var scene=SceneManager.GetActiveScene();
  if(!Application.isPlaying||!EditorApplication.isPaused||root==""||scene.path!=SessionState.GetString(Key+".Scene","")||Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")!=root+"/save")throw new Exception("Prepared current campaign must be paused before recording starts");
  if(Observer()!=null)throw new Exception("Recorder already active");
  var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
  if(s.Persistence==null||s.Persistence.Blocked||s.Content.InfiniteResources||s.Content.Defeated||Time.timeScale!=1)throw new Exception("Normal fresh campaign required");
  var recorder=new GameObject("Balance run observer").AddComponent<BalanceRunObserver>();recorder.Begin(s,root);return recorder.Status();
 }
 public static string Mark(string action,string detail=""){var r=Observer();if(r==null)throw new Exception("No active recorder");Call(r,"Mark",action,detail,true);return (string)Call(r,"Status");}
 public static string Status(){var r=Observer();return r==null?"No active recorder":(string)Call(r,"Status");}
 public static string Stop(string reason){var r=Observer();if(r==null)throw new Exception("No active recorder");Call(r,"Finish",reason);return (string)Call(r,"Status");}
 public static string Restore(){if(Application.isPlaying)throw new Exception("Stop Play first");if(SceneManager.GetActiveScene().path!=SessionState.GetString(Key+".Scene",""))throw new Exception("Scene changed during run");string previous=SessionState.GetString(Key+".SaveRoot","");Environment.SetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT",previous==""?null:previous);Application.runInBackground=SessionState.GetBool(Key+".Background",false);EditorApplication.isPaused=SessionState.GetBool(Key+".Paused",false);return "Current scene retained; editor/save overrides restored";}
}

public sealed class BalanceRunObserver:MonoBehaviour
{
 OpenWorldSandbox s;string root,scenePath,lastPhase,lastStage;StreamWriter writer;long sequence,screenshot;double nextSample,nextScreenshot;bool stopped;string endingReason;public string LastError {get;private set;}
 readonly Dictionary<ResourceBank,Action<ResourceBank.Change>> banks=new();
 readonly Dictionary<BuildingInstance,Action<float>> buildings=new();
 sealed class WeaponObservation
 {
  public BuildingInstance Building;public string Scope;public long Shots,Projectiles;public Vector3 LastTarget;
  public Action<Vector3> Shot,Projectile;
 }
 readonly Dictionary<BuildingInstance,WeaponObservation> weapons=new();
 readonly Dictionary<string,string> trainStates=new();
 readonly Queue<(string path,long seq,string reason)> shots=new();
 public void Begin(OpenWorldSandbox world,string directory)
 {
  s=world;root=directory;scenePath=SceneManager.GetActiveScene().path;writer=new StreamWriter(new FileStream(root+"/events.jsonl",FileMode.CreateNew,FileAccess.Write,FileShare.Read));
  s.Content.Inventories.Created+=Bank;s.Content.Bases.MembershipChanged+=Building;s.Enemies.SpawnedEnemy+=Spawn;s.Enemies.KilledEnemy+=Killed;
  Bank("shared",s.Content.Resources);foreach(var record in s.Content.Inventories.Capture())Bank(record.baseId,s.Content.Inventories.Ensure(record.baseId));foreach(var b in s.Content.Bases.Buildings)Building(b,true);
  lastPhase=s.Clock.Day+":"+s.Clock.Phase;lastStage=s.Assault.Energy.Stage.ToString();Mark("run-start","Captured while Editor paused before normal simulation resumes",true);Sample();StartCoroutine(CaptureScreens());
 }
 double GameSeconds=>(s.Clock.Day-1)*(double)(s.DayDurationSeconds+s.NightDurationSeconds)+(s.Clock.Phase==DayPhase.Day?s.DayDurationSeconds-s.Clock.RemainingSeconds:s.DayDurationSeconds+s.NightDurationSeconds-s.Clock.RemainingSeconds);
 JObject Stamp(string kind)=>new JObject{{"sequence",++sequence},{"utc",DateTime.UtcNow.ToString("O")},{"realtime",Time.realtimeSinceStartupAsDouble},{"gameSeconds",GameSeconds},{"day",s.Clock.Day},{"phase",s.Clock.Phase.ToString()},{"kind",kind}};
 void Write(string kind,JObject data)
 {
  if(stopped||writer==null||LastError!=null)return;
  try{var row=Stamp(kind);foreach(var p in data.Properties().ToArray())row[p.Name]=p.Value;writer.WriteLine(row.ToString(Formatting.None));}
  catch(Exception e){LastError=e.ToString();EditorApplication.isPaused=true;}
 }
 void Bank(string id,ResourceBank bank)
 {
  if(banks.ContainsKey(bank))return;
  Action<ResourceBank.Change> callback=c=>Write("resource",new JObject{{"base",id},{"operation",c.Operation},{"resource",c.Resource},{"requested",c.Requested},{"before",c.Before},{"after",c.After},{"delta",c.After-c.Before},{"capacityBefore",c.CapacityBefore},{"capacityAfter",c.CapacityAfter}});
  banks.Add(bank,callback);bank.Changed+=callback;Write("inventory-observed",new JObject{{"base",id},{"stocks",JArray.FromObject(bank.Capture())}});
 }
 void ObserveWeapon(BuildingInstance b,string scope)
 {
  if(weapons.ContainsKey(b))return;
  var w=new WeaponObservation{Building=b,Scope=scope};
  w.Shot=p=>{w.Shots++;w.LastTarget=p;};w.Projectile=p=>{w.Projectiles++;w.LastTarget=p;};
  b.Shot+=w.Shot;b.Projectile+=w.Projectile;weapons.Add(b,w);
 }
 void Building(BuildingInstance b,bool added)
 {
  if(added)ObserveWeapon(b,"stationary");
  if(added&&!buildings.ContainsKey(b)){Action<float> damage=n=>Write("building-damage",new JObject{{"building",b.PersistentId},{"owner",b.OwnerBaseId},{"damage",n},{"health",b.Module<HealthModule>()?.Current}});buildings.Add(b,damage);b.Damaged+=damage;Write("building-added",DescribeBuilding(b));RequestScreenshot("building-added");}
  else if(!added&&buildings.Remove(b,out var callback)){b.Damaged-=callback;Write("building-removed",new JObject{{"building",b.PersistentId},{"definition",b.DefinitionId},{"owner",b.OwnerBaseId}});RequestScreenshot("building-removed");}
 }
 static JObject Vector(Vector3 p)=>new JObject{{"x",p.x},{"y",p.y},{"z",p.z}};
 JObject DescribeBuilding(BuildingInstance b)
 {
  var power=b.Module<PowerModule>();var health=b.Module<HealthModule>();
  return new JObject{{"building",b.PersistentId},{"definition",b.DefinitionId},{"name",b.DisplayName},{"owner",b.OwnerBaseId},{"baseId",b.Module<IBaseIdentity>()?.BaseId},{"position",Vector(b.Position)},{"operational",b.Operational},{"block",b.OperationBlock.ToString()},{"level",b.Module<IUpgradeControl>()?.Level},{"health",health?.Current},{"maximumHealth",health?.Maximum},{"power",power==null?null:new JObject{{"role",power.Role.ToString()},{"stored",power.Stored},{"capacity",power.Capacity},{"production",power.Production},{"requested",power.Requested},{"consumedLastSecond",power.Consumed},{"supplied",power.Supplied},{"supply",power.Supply?.Owner.PersistentId}}},{"modules",new JArray(b.Modules.Select(m=>JObject.Parse(JsonUtility.ToJson(SavedState.Capture(m)))))}};
 }
 void Spawn(int id){ref readonly var e=ref s.Enemies.GetEnemy(id);Write("enemy-spawn",new JObject{{"id",id},{"generation",s.Enemies.Generation(id)},{"health",e.health},{"speed",e.speed},{"air",e.air},{"position",Vector(e.position)},{"destination",Vector(e.destination)}});}
 void Killed(int id,int generation)=>Write("enemy-killed",new JObject{{"id",id},{"generation",generation}});
 public void Mark(string action,string detail,bool capture){Write("action",new JObject{{"action",action},{"detail",detail}});if(capture)RequestScreenshot(action);Flush();}
 void RequestScreenshot(string reason)
 {
  if(stopped||root==null)return;Write("screenshot-request",new JObject{{"reason",reason}});shots.Enqueue(("",sequence,reason));
 }
 IEnumerator CaptureScreens()
 {
  while(!stopped){yield return new WaitForEndOfFrame();if(shots.Count==0)continue;
   // Milestones in the same rendered frame share one physical capture, with every cause retained.
   var pending=shots.ToArray();shots.Clear();string path="screenshots/"+(++screenshot).ToString("D6")+".png";Texture2D image=null;
   try{image=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(root+"/"+path,image.EncodeToPNG());Write("screenshot-captured",new JObject{{"file",path},{"requests",new JArray(pending.Select(p=>new JObject{{"sequence",p.seq},{"reason",p.reason}}))},{"width",image.width},{"height",image.height}});Flush();}
   catch(Exception e){LastError=e.ToString();EditorApplication.isPaused=true;}
   finally{if(image!=null)Destroy(image);}
   if(endingReason!=null)FinishNow(endingReason);
  }
 }
 void LateUpdate()
 {
  if(stopped||endingReason!=null||s==null||LastError!=null)return;
  if(SceneManager.GetActiveScene().path!=scenePath){Finish("scene-changed-invalid-run");return;}
  if(s.Content.InfiniteResources||Time.timeScale!=1){Mark("protocol-violation","Infinite resources or timeScale changed",true);Finish("protocol-violation");return;}
  string phase=s.Clock.Day+":"+s.Clock.Phase,stage=s.Assault.Energy.Stage.ToString();if(phase!=lastPhase){Mark("phase-change",phase,true);lastPhase=phase;}if(stage!=lastStage){Mark("stage-change",stage,true);lastStage=stage;}
  foreach(var route in s.Content.Railway.Network.Routes){string state=route.train.status+":"+route.train.stop+":"+route.train.reverse+":"+route.train.cargo+":"+route.train.resource; if(!trainStates.TryGetValue(route.id,out var before)||state!=before){Write("train-change",new JObject{{"route",route.id},{"previous",before},{"current",state},{"train",JObject.Parse(JsonUtility.ToJson(route.train))}});trainStates[route.id]=state;RequestScreenshot("train-change");}}
  foreach(var mobile in s.Content.Railway.Defense.Mounted){var route=s.Content.Railway.Network.Routes.FirstOrDefault(r=>ReferenceEquals(r.train.armament,mobile.State));ObserveWeapon(mobile.Building,"train:"+route?.trainId);}
  if(Time.realtimeSinceStartupAsDouble>=nextSample){nextSample=Time.realtimeSinceStartupAsDouble+1;Sample();}
  if(Time.realtimeSinceStartupAsDouble>=nextScreenshot){nextScreenshot=Time.realtimeSinceStartupAsDouble+60;RequestScreenshot("periodic");}
 }
 void Sample()
 {
  var e=s.Assault.Energy;
  Write("weapon-counters",new JObject{{"actors",new JArray(weapons.Values.Select(w=>new JObject{{"building",w.Building.PersistentId},{"definition",w.Building.DefinitionId},{"scope",w.Scope},{"disposed",w.Building.Disposed},{"shotSignals",w.Shots},{"projectileSignals",w.Projectiles},{"lastTarget",Vector(w.LastTarget)}}))}});
  Write("sample",new JObject{{"paused",s.SimulationPaused},{"pendingSimulationSeconds",s.PendingSimulationTime},{"stage",e.Stage.ToString()},{"energy",new JObject{{"earned",e.Earned},{"extracted",e.Extracted},{"killReward",e.KillReward},{"bossReward",e.BossReward},{"balance",e.Balance},{"target",e.Target},{"perfectOrb",e.PerfectOrb}}},{"enemies",new JObject{{"alive",s.Enemies.Alive},{"spawned",s.Enemies.Spawned},{"killed",s.Enemies.Killed},{"budgetDay",s.Assault.Planner.LastBudgetDay},{"pending",s.Assault.Planner.Pending},{"spawnPoints",JArray.FromObject(s.Assault.Planner.Points)},{"bossHealth",s.Assault.BossHealth}}},{"inventories",JArray.FromObject(s.Content.Inventories.Capture())},{"buildings",new JArray(s.Content.Bases.Buildings.Select(DescribeBuilding))},{"routes",new JArray(s.Content.Railway.Network.Routes.Select(r=>new JObject{{"route",r.id},{"name",r.name},{"error",r.error},{"train",JObject.Parse(JsonUtility.ToJson(r.train))}}))},{"observationErrors",banks.Keys.Sum(b=>b.ObservationErrors)+s.Content.Inventories.ObservationErrors+s.Content.Bases.ObservationErrors},{"frameMs",Time.unscaledDeltaTime*1000}});Flush();
 }
 void Flush(){try{writer?.Flush();}catch(Exception e){LastError=e.ToString();EditorApplication.isPaused=true;}}
 public string Status()=>new JObject{{"root",root},{"sequence",sequence},{"stopped",stopped},{"stopRequested",endingReason},{"error",LastError},{"screenshotsPending",shots.Count},{"day",s?.Clock.Day},{"phase",s?.Clock.Phase.ToString()}}.ToString();
 public void Finish(string reason)
 {
  if(stopped||endingReason!=null)return;
  if(LastError!=null){FinishNow(reason);return;}
  endingReason=reason;RequestScreenshot("run-end");
 }
 void FinishNow(string reason)
 {
  if(stopped)return;Sample();Write("run-end",new JObject{{"reason",reason},{"screenshotsPending",shots.Count},{"error",LastError}});Flush();stopped=true;
  s.Content.Inventories.Created-=Bank;s.Content.Bases.MembershipChanged-=Building;s.Enemies.SpawnedEnemy-=Spawn;s.Enemies.KilledEnemy-=Killed;
  foreach(var w in weapons.Values){w.Building.Shot-=w.Shot;w.Building.Projectile-=w.Projectile;}
  foreach(var p in banks)p.Key.Changed-=p.Value;foreach(var p in buildings)p.Key.Damaged-=p.Value;writer?.Dispose();writer=null;
  File.WriteAllText(root+"/status.json",new JObject{{"reason",reason},{"stopped",true},{"sequence",sequence},{"error",LastError},{"screenshotsPending",shots.Count},{"day",s.Clock.Day},{"phase",s.Clock.Phase.ToString()},{"defeated",s.Content.Defeated},{"stage",s.Assault.Energy.Stage.ToString()}}.ToString());
 }
 void OnDestroy(){if(s!=null&&!stopped){try{FinishNow("observer-destroyed-before-controlled-stop");}catch(Exception e){LastError=e.ToString();writer?.Dispose();}}}
}
