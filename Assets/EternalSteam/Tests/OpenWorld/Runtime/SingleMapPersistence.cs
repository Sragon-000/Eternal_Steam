using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EternalSteam.Demo;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace EternalSteam.OpenWorld
{
    public sealed class SingleMapPersistence:ISaveEligibility
    {
        readonly OpenWorldSandbox s;readonly ISaveStore store;readonly string configuration;
        public string RunId {get;private set;}=Guid.NewGuid().ToString("N");
        public string Status {get;private set;}="아직 저장하지 않았습니다.";
        public string LastSavedUtc {get;private set;}
        public bool Blocked {get;private set;}
        public bool Automatic=true;
        bool busy,dirty=true,failedRecorded;double sinceSave,retry;
        public UpgradePurchase Upgrades {get;}
        public SingleMapPersistence(OpenWorldSandbox sandbox,ISaveStore store=null)
        {
            s=sandbox;configuration=SaveConfiguration.Compute(s);string root=Path.Combine(Application.persistentDataPath,"EternalSteam","SingleMap");
#if UNITY_EDITOR
            root=Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")??root;
#endif
            s.Content.Failed+=FailNow;this.store=store??new JsonSaveStore(root);Upgrades=new UpgradePurchase(s.Content.Resources,new VerificationFreeUpgrade());
        }
        public void Initialize()
        {
            var loaded=store.Read();
            if(loaded.Blocked){Blocked=true;RunId=loaded.RunId??RunId;Status=loaded.Error;s.StartingBase.gameObject.SetActive(false);s.Clock.Paused=true;return;}
            if(loaded.Payload==null){InstallMain();return;}
            try{var data=JsonUtility.FromJson<SingleMapSnapshot>(loaded.Payload);if(data.runId!=loaded.RunId)throw new InvalidDataException("진행 ID 불일치");Validate(data);Restore(data);Status=(loaded.Recovered?"직전 정상 백업에서 복구했습니다. ":"")+"불러오기 완료 · 계속 진행을 누르세요.";}
            catch(Exception e){Blocked=true;s.Clock.Paused=true;Status="이어하기 차단: "+e.Message;ClearPartial();}
        }
        void InstallMain(){var result=s.Content.InstallStartingBase(s.StartingBase);if(!result.Success)throw new InvalidOperationException(result.Message);s.Content.Bases.Refresh();}
        void ClearPartial(){s.Content.PrepareRestoredBuilding=null;foreach(var p in s.Foundations.Platforms.ToArray())s.Foundations.RemoveFoundation(p);s.Content.GroundWorld.Clear();if(s.StartingBase!=null)s.StartingBase.gameObject.SetActive(false);}
        public bool CanSave(out string reason)
        {
            reason=null;if(Blocked||s.Content.Defeated){reason="패배 또는 복원 오류 상태입니다.";return false;}if(busy){reason="저장/복원 처리 중입니다.";return false;}
            var input=s.GetComponent<OpenWorldInput>();if(input.IsEditing||(input.Edits?.Count??0)>0||s.Content.GroundPlacement.Pending.Count>0||s.Foundations.Platforms.Any(p=>p.Placement.Pending.Count>0)){reason="수정 작업을 확정하거나 취소하세요.";return false;}
            if(s.Clock.Phase!=DayPhase.Day){reason="낮에만 안전 저장할 수 있습니다.";return false;}
            if(s.Enemies.Alive>0||s.Assault.BossId>=0||s.Assault.Energy.Stage==MapStage.BossBattle){reason="적 또는 보스와 전투 중입니다.";return false;}
            if(s.SpawnStream.Pending>0){reason="개발자 소환 대기가 남아 있습니다.";return false;}
            foreach(var b in s.Content.Bases.Buildings)foreach(var module in b.Modules)if(module is IPendingExecution pending&&pending.HasPendingExecution){reason="투사체 또는 연쇄 공격이 진행 중입니다.";return false;}
            return true;
        }
        public void Changed(){dirty=true;}
        public void RequestAutoSave(){dirty=true;if(Automatic&&CanSave(out _))Save();}
        public void Tick(double seconds)
        {
            if(s.Content.Defeated){RecordFailure(seconds);return;}
            if(Blocked||!Automatic)return;sinceSave+=seconds;
            if(dirty&&sinceSave>=30&&CanSave(out _))Save();
        }
        void RecordFailure(double seconds)
        {
            if(failedRecorded)return;retry-=seconds;if(retry>0)return;
            try{store.MarkFailed(RunId);failedRecorded=true;Status="패배 기록 완료 · 새 게임만 가능합니다.";}
            catch(Exception e){retry=1;Status="패배 기록 실패 · 재시도 중: "+e.Message;}
        }
        public void FailNow(){RecordFailure(0);}
        public bool Save()
        {
            if(!CanSave(out var reason)){Status="저장 불가: "+reason;return false;}
            busy=true;
            try{var data=Capture();Validate(data);store.Write(RunId,JsonUtility.ToJson(data));LastSavedUtc=data.savedUtc;Status="안전 저장 완료 · "+DateTime.Parse(data.savedUtc).ToLocalTime().ToString("HH:mm:ss");dirty=false;sinceSave=0;return true;}
            catch(Exception e){Status="저장 실패: "+e.Message;sinceSave=29;return false;}
            finally{busy=false;}
        }
        public bool HasContinue=>!s.Content.Defeated&&!Blocked&&LastSavedUtc!=null;
        public void ContinueSaved(){if(!HasContinue){Status="이어갈 수 있는 정상 저장이 없습니다.";return;}Reload();}
        public void NewGame()
        {
            if(s.Content.Defeated&&!failedRecorded){FailNow();if(!failedRecorded){Status="패배 기록을 완료한 뒤 새 게임을 시작할 수 있습니다.";return;}}
            try{store.Archive();Reload();}catch(Exception e){Status="새 게임 준비 실패: "+e.Message;}
        }
        void Reload()
        {
            s.Clock.Paused=true;var path=s.gameObject.scene.path;
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(path);
#endif
        }
        Dictionary<string,BuildingDefinition> Definitions()
        {
            var all=s.ContentCatalog.Buildings.ToDictionary(d=>d.Id,StringComparer.Ordinal);
            // Explicit stable IDs; enum integer order is never a save key.
            all.Add("openworld.legacy.MachineGun",s.Foundations.Definition(HordeTowerKind.MachineGun));
            all.Add("openworld.legacy.Cannon",s.Foundations.Definition(HordeTowerKind.Cannon));
            all.Add("openworld.legacy.Frost",s.Foundations.Definition(HordeTowerKind.Frost));
            all.Add("openworld.legacy.Arrow",s.Foundations.Definition(HordeTowerKind.Arrow));return all;
        }
        static bool Legacy(string id,out HordeTowerKind kind){switch(id){case "openworld.legacy.MachineGun":kind=HordeTowerKind.MachineGun;return true;case "openworld.legacy.Cannon":kind=HordeTowerKind.Cannon;return true;case "openworld.legacy.Frost":kind=HordeTowerKind.Frost;return true;case "openworld.legacy.Arrow":kind=HordeTowerKind.Arrow;return true;default:kind=default;return false;}}
        public SingleMapSnapshot Capture()
        {
            s.Content.Bases.Refresh();s.Content.Power.Refresh();
            var data=new SingleMapSnapshot{runId=RunId,mapId=s.AssaultSettings.MapId,configuration=configuration,savedUtc=DateTime.UtcNow.ToString("O"),campaign=SavedState.Capture(s.Campaign),clock=SavedState.Capture(s.Clock),power=SavedState.Capture(s.Content.Power),assault=SavedState.Capture(s.Assault),planner=SavedState.Capture(s.Assault.Planner),energy=JsonUtility.FromJson<MapEnergyState>(JsonUtility.ToJson(s.Assault.Energy)),stocks=s.Content.Resources.Capture(),cameraFocus=s.CameraRig.Focus,cameraZoom=s.CameraRig.Zoom,pendingTime=s.PendingSimulationTime};
            void Add(BuildingInstance b,string platform){var record=new BuildingRecord{id=b.PersistentId,definition=b.DefinitionId,owner=b.OwnerBaseId,baseId=b.Module<IBaseIdentity>()?.BaseId,cell=b.Cell,direction=b.Direction,foundation=platform,supply=b.Module<PowerModule>()?.Supply?.Owner.PersistentId};foreach(var module in b.Modules)record.modules.Add(SavedState.Capture(module));var tower=s.Towers.Find(t=>ReferenceEquals(t.building,b));if(tower!=null){record.legacyCooldown=tower.cooldown;record.legacyShot=tower.shot;record.legacyDirection=tower.head.forward;}data.buildings.Add(record);}
            foreach(var b in s.Content.GroundWorld.Buildings)Add(b,null);
            foreach(var p in s.Foundations.Platforms){data.foundations.Add(new FoundationRecord{id=p.PersistentId,position=p.View.transform.position});foreach(var b in p.World.Buildings)Add(b,p.PersistentId);}
            data.retiredBases=data.buildings.Select(b=>b.owner).Where(id=>!string.IsNullOrEmpty(id)&&!s.Content.Bases.Bases.ContainsKey(id)).Distinct().OrderBy(id=>id,StringComparer.Ordinal).ToList();return data;
        }
        static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        static bool Id(string id)=>Guid.TryParseExact(id,"N",out _);
        static bool Finite(Vector3 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z);
        public void Validate(SingleMapSnapshot data)
        {
            Require(data!=null&&data.version==1,"지원하지 않는 맵 저장 버전");Require(Id(data.runId)&&data.mapId==s.AssaultSettings.MapId&&data.configuration==configuration,"맵/콘텐츠 구성이 저장과 다릅니다.");
            Require(data.buildings!=null&&data.buildings.Count<=10000&&data.foundations!=null&&data.foundations.Count<=10000,"잘못된 건물 목록");
            Require(Finite(data.cameraFocus)&&float.IsFinite(data.cameraZoom)&&data.cameraZoom>0&&double.IsFinite(data.pendingTime)&&data.pendingTime>=0,"잘못된 화면/시간 상태");
            var campaign=new CampaignProgression();data.campaign.Restore(campaign);var clock=new GameClock(s.DayDurationSeconds,s.NightDurationSeconds);data.clock.Restore(clock);Require(clock.Phase==DayPhase.Day&&clock.RemainingSeconds>0&&clock.RemainingSeconds<=s.DayDurationSeconds,"안전 저장 시각 오류");
            data.power.Validate(s.Content.Power);data.assault.Validate(s.Assault);var planner=new NightSpawnPlanner(s.Assault.Coverage.Centers.Length,1);data.planner.Restore(planner);Require(planner.LastBudgetDay<=clock.Day&&planner.Points.Count<=12&&planner.Points.Distinct().Count()==planner.Points.Count&&planner.Points.All(p=>p>=0&&p<s.Assault.Coverage.Centers.Length),"잘못된 소환 상태");
            var energy=data.energy;Require(energy!=null&&energy.MapId==data.mapId&&energy.Target==s.AssaultSettings.Target&&energy.Remaining?.Length==s.Assault.Coverage.Centers.Length,"에너지 구성 불일치");
            Require(energy.Remaining.All(v=>v>=0&&v<=s.AssaultSettings.EnergyPerCell)&&energy.Extracted==energy.Remaining.Sum(v=>(long)s.AssaultSettings.EnergyPerCell-v)&&energy.KillReward>=0&&energy.KillReward<=energy.PreBossLimit&&energy.Extracted<=energy.PreBossLimit,"잘못된 에너지 잔량");
            Require(energy.Stage is MapStage.Gathering or MapStage.WaitingForNight or MapStage.AwaitingCraft or MapStage.Cleared,"저장할 수 없는 진행 단계");
            bool won=energy.Stage is MapStage.AwaitingCraft or MapStage.Cleared;Require(energy.BossReward==(won?energy.Target/10:0)&&energy.Earned<=energy.Target&&energy.Balance==energy.Earned-(energy.PerfectOrb?energy.Target:0)&&energy.Balance>=0&&energy.PerfectOrb==(energy.Stage==MapStage.Cleared)&&energy.IncompleteOrb==(energy.Stage==MapStage.AwaitingCraft),"오브/보상 상태 불일치");
            if(won)Require(energy.Earned==energy.Target,"보스 보상 이전 에너지 부족");
            var bank=new ResourceBank();bank.Restore(data.stocks);var expected=s.Content.Resources.Capture().Select(x=>x.id).OrderBy(x=>x).ToArray();Require(expected.SequenceEqual(data.stocks.Select(x=>x.id).OrderBy(x=>x)),"자원 종류 불일치");
            var defs=Definitions();var ids=new HashSet<string>();var bases=new HashSet<string>();var platforms=new Dictionary<string,FoundationRecord>();var footprint=new Dictionary<string,HashSet<Vector2Int>>();
            foreach(var p in data.foundations){Require(p!=null&&Id(p.id)&&ids.Add(p.id)&&Finite(p.position),"토대 ID/위치 오류");Require(WorldGridGeometry.TerrainPlacement(s.Ground,p.position,out var center,out _)&&(center-Vector3.up*.2f-p.position).sqrMagnitude<.01f,"토대 지면 불일치");Require(!s.Content.ReservedSpawnOverlap(p.position,new Vector2Int(4,4)),"보스 구역 토대");foreach(var old in platforms.Values)Require(!SpawnAreaValidator.Overlaps(p.position,new Vector2Int(4,4),old.position,new Vector2Int(4,4)),"토대 중복 점유");platforms.Add(p.id,p);}
            int mains=0,subs=0;
            foreach(var r in data.buildings){
                Require(r!=null&&Id(r.id)&&ids.Add(r.id)&&r.definition!=null&&defs.ContainsKey(r.definition),"건물 ID 중복 또는 누락 콘텐츠");Require(Finite(r.direction)&&r.direction.sqrMagnitude>.001f&&float.IsFinite(r.legacyCooldown)&&r.legacyCooldown>=0&&r.legacyShot>=0&&Finite(r.legacyDirection),"건물 방향/쿨다운 오류");
                var def=defs[r.definition];bool ground=string.IsNullOrEmpty(r.foundation);Require(ground||platforms.ContainsKey(r.foundation),"존재하지 않는 토대 참조");
                var role=OpenWorldContent.RoleOf(def);if(role==BaseRole.Main){mains++;Require(ground&&r.cell==s.StartingBase.Cell&&def.Id==s.StartingBase.Definition.Id,"메인 고정 위치 불일치");}if(role==BaseRole.Sub)subs++;
                if(role!=BaseRole.Legacy)Require(ground,"기지는 지면에만 복원 가능");Require(role==BaseRole.Main||(def.Placement?.RequiredNexusLevel??1)<=campaign.MainLevel,"해금 레벨 불일치");
                Require(string.IsNullOrEmpty(r.owner)||Id(r.owner),"소속 ID 오류");if(!string.IsNullOrEmpty(r.baseId))Require(Id(r.baseId)&&bases.Add(r.baseId),"기지 ID 중복");
                string key=ground?"ground":r.foundation;if(!footprint.TryGetValue(key,out var used))footprint.Add(key,used=new HashSet<Vector2Int>());
                for(int z=0;z<def.Footprint.y;z++)for(int x=0;x<def.Footprint.x;x++){var c=r.cell+new Vector2Int(x,z);Require((ground?s.Content.GroundWorld.Grid.Bounds.Contains(c):new RectInt(0,0,4,4).Contains(c))&&used.Add(c),"건물 경계/점유 충돌");}
                if(ground){Require(def.Placement?.Surface!=BuildingSurface.Foundation,"설치 표면 불일치");Require(s.Content.CheckGround(r.cell,def.Footprint,out _,out var reason),reason??"지면 오류");var p=s.Content.GroundWorld.Grid.Center(r.cell,def.Footprint);foreach(var platform in platforms.Values)Require(!SpawnAreaValidator.Overlaps(p,def.Footprint,platform.position,new Vector2Int(4,4)),"지면 건물과 토대 충돌");}
                else Require(def.Placement?.Surface!=BuildingSurface.Ground,"지면 전용 건물의 토대 복원");
                using(var probe=new BuildingInstance(1,def,r.cell,Vector3.zero,new BuildingServices(s.TargetAdapter,resources:bank,levelLimit:s.Content,campaign:campaign))){ApplyRecord(probe,r);Require((probe.Module<IBaseIdentity>()?.BaseId??"")== (r.baseId??""),"기지 식별자 불일치");var level=probe.Module<IUpgradeControl>();Require(level==null||(level.Level<=level.MaximumLevel&&(role!=BaseRole.Sub||level.Level<=campaign.MainLevel)),"건물 레벨 범위 오류");var health=probe.Module<HealthModule>();Require(health==null||health.Current<=health.Maximum,"체력 범위 오류");if(health!=null){var baseHealth=def.Modules.OfType<HealthModuleDefinition>().Single().Maximum;var growth=def.Modules.OfType<UpgradeModuleDefinition>().FirstOrDefault();float expectedHealth=baseHealth*(1+(growth?.HealthPerLevel??0)*((level?.Level??1)-1));Require(Math.Abs(health.Maximum-expectedHealth)<.001f,"최대 체력과 레벨 불일치");}var power=probe.Module<PowerModule>();Require(power==null||power.Stored<=power.Capacity,"전력 용량 오류");}
            }
            Require(mains==1&&subs<=Math.Min(campaign.MainLevel*5,10),"기지 수 제한 오류");
            Require(data.retiredBases!=null&&data.retiredBases.Distinct().Count()==data.retiredBases.Count&&data.retiredBases.All(id=>Id(id)&&!bases.Contains(id)),"상실 기지 기록 오류");
            foreach(var r in data.buildings)Require(string.IsNullOrEmpty(r.owner)||bases.Contains(r.owner)||data.retiredBases.Contains(r.owner),"존재하지 않는 소속 기지 참조");
            foreach(var r in data.buildings)if(!string.IsNullOrEmpty(r.supply)){var provider=data.buildings.SingleOrDefault(b=>b.id==r.supply);Require(provider!=null&&defs[provider.definition].Modules.OfType<PowerModuleDefinition>().Any(p=>p.Role==PowerRole.Storage),"전력 연결 참조 오류");}
        }
        static void ApplyRecord(BuildingInstance b,BuildingRecord r)
        {
            Require(r.modules!=null&&r.modules.Count==b.Modules.Count,"모듈 구성 불일치");b.RestoreIdentity(r.id,string.IsNullOrEmpty(r.owner)?null:r.owner);
            for(int i=0;i<b.Modules.Count;i++)r.modules[i].Validate(b.Modules[i]);
            for(int i=0;i<b.Modules.Count;i++)r.modules[i].Restore(b.Modules[i]);
        }
        void Restore(SingleMapSnapshot data)
        {
            busy=true;
            try{
                RunId=data.runId;data.campaign.Restore(s.Campaign);data.clock.Restore(s.Clock);s.Content.Resources.Restore(data.stocks);var defs=Definitions();
                var main=data.buildings.Single(r=>OpenWorldContent.RoleOf(defs[r.definition])==BaseRole.Main);s.Content.PrepareRestoredBuilding=b=>ApplyRecord(b,main);InstallMain();s.Content.PrepareRestoredBuilding=null;
                foreach(var p in data.foundations){Require(s.Foundations.AddFoundation(p.position,out var why),why);s.Foundations.Platforms.Single(f=>f.Key==FoundationPlacement.Key(p.position)).PersistentId=p.id;}
                foreach(var record in data.buildings.Where(r=>r!=main).OrderBy(r=>OpenWorldContent.RoleOf(defs[r.definition])==BaseRole.Sub?0:1)){
                    var def=defs[record.definition];var platform=string.IsNullOrEmpty(record.foundation)?s.Foundations.GroundPlatform:s.Foundations.Platforms.Single(p=>p.PersistentId==record.foundation);
                    var session=platform==s.Foundations.GroundPlatform?s.Content.GroundPlacement:platform.Placement;var added=session.Add(def,record.cell,out var request);Require(added.Success,added.Message);request.Direction=record.direction;s.Content.PrepareRestoredBuilding=b=>ApplyRecord(b,record);
                    try{if(Legacy(def.Id,out var kind)){var position=platform.World.Grid.Center(record.cell,def.Footprint);if(platform==s.Foundations.GroundPlatform){s.Content.CheckGround(record.cell,def.Footprint,out float h,out _);position.y=h+.05f;}var view=s.Foundations.CreateTowerPreview(kind,position,record.direction);view.SetActive(false);platform.Factory.ConfigureRequest(request.Id,22,kind);platform.Factory.ConfigureExistingRequest(request.Id,view.GetComponent<SceneTower>().RuntimeView());}
                        var result=session.Confirm();Require(result.Success,result.Message);
                    }finally{s.Content.PrepareRestoredBuilding=null;platform.Factory.ForgetRequest(request.Id);session.Cancel();}
                }
                s.Content.Bases.Refresh();s.Content.Power.Refresh();var restored=s.Content.Bases.Buildings.ToDictionary(b=>b.PersistentId);
                foreach(var record in data.buildings){var b=restored[record.id];for(int i=0;i<b.Modules.Count;i++)record.modules[i].Restore(b.Modules[i]);if(b.Module<ITurretRotation>() is ITurretRotation rotation)b.ReportAim(b.Position+rotation.Direction);var power=b.Module<PowerModule>();if(power!=null){power.RestoreSupply(string.IsNullOrEmpty(record.supply)?null:restored[record.supply].Module<PowerModule>());record.modules[b.Modules.ToList().IndexOf(power)].Restore(power);}var tower=s.Towers.Find(t=>ReferenceEquals(t.building,b));if(tower!=null){tower.cooldown=record.legacyCooldown;tower.shot=record.legacyShot;tower.head.rotation=Quaternion.LookRotation(record.legacyDirection.sqrMagnitude>.001f?record.legacyDirection:record.direction);}}
                s.Content.Power.RecalculateRates();data.power.Restore(s.Content.Power);s.Assault.Coverage.Refresh();JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(data.energy),s.Assault.Energy);data.assault.Restore(s.Assault);data.planner.Restore(s.Assault.Planner);
                s.Content.Resources.Restore(data.stocks);s.PendingSimulationTime=data.pendingTime;s.CameraRig.Focus=data.cameraFocus;s.CameraRig.Zoom=data.cameraZoom;s.Clock.Paused=true;LastSavedUtc=data.savedUtc;dirty=false;
            }finally{s.Content.PrepareRestoredBuilding=null;busy=false;}
        }
    }
}
