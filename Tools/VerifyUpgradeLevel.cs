using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class VerifyUpgradeLevel
{
 const string Root="/tmp/eternal-fix01-live";
 static void Check(bool b,string m){if(!b)throw new Exception(m);}
 static OpenWorldSandbox World(){Check(Application.isPlaying,"Play required");Check(Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")==Root,"Isolated save root required");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();s.Persistence.Automatic=false;s.Clock.Paused=true;Check(!s.Persistence.Blocked,s.Persistence.Status);return s;}
 static void Select(OpenWorldSandbox s,BuildingInstance b){var input=s.GetComponent<OpenWorldInput>();input.Cancel();Check(input.ClickWorld(b.Position),"Select building");Check(ReferenceEquals(input.SelectedContent,b)||ReferenceEquals(input.SelectedTower?.building,b),"Selection identity");}
 static UnityEngine.UI.Button Button(OpenWorldSandbox s){var hud=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();hud.SendMessage("Refresh");return hud.Buttons.Single(b=>b.Id=="upgrade").View;}
 public static string Prepare()
 {
  var s=World();Check(s.Campaign.MainLevel==1,"Fresh main1");var def=s.ContentCatalog.Buildings.Single(d=>d.Id=="defense.tesla");var area=s.Content.MainBase.Module<IBuildArea>();BuildingInstance tower=null;
  for(int z=-50;z<60&&tower==null;z++)for(int x=-50;x<60&&tower==null;x++){var cell=new Vector2Int(x,z);if(!area.Contains(s.Content.GroundWorld.Grid.Center(cell,def.Footprint),(Vector2)def.Footprint,WorldGridGeometry.Rotation))continue;if(!s.Content.GroundPlacement.Add(def,cell,out _).Success)continue;var result=s.Content.GroundPlacement.Confirm();Check(result.Success,result.Message);tower=s.Content.GroundWorld.Buildings.Last();}
  Check(tower!=null,"Install actual tesla asset");Select(s,tower);var button=Button(s);Check(!button.interactable,"Main1 button disabled");button.onClick.Invoke();Check(tower.Module<IUpgradeControl>().Level==1,"Callback cannot bypass cap");
  Check(s.Persistence.Save(),s.Persistence.Status);var good=s.Persistence.Capture();s.Persistence.Validate(good);var bad=JsonUtility.FromJson<SingleMapSnapshot>(JsonUtility.ToJson(good));var record=bad.buildings.Single(b=>b.definition=="defense.tesla");var state=record.modules.Single(m=>m.type==typeof(PerformanceUpgrade).FullName);state.values.Single(v=>v.name=="Level").text="2";
  bool rejected=false;try{s.Persistence.Validate(bad);}catch(InvalidDataException e){rejected=e.Message.Contains("레벨");}Check(rejected,"Over-level snapshot rejected");
  Select(s,s.Content.MainBase);Check(Button(s).interactable,"Main can grow");Button(s).onClick.Invoke();Check(s.Campaign.MainLevel==2,"Main growth callback");
  Select(s,tower);Check(Button(s).interactable,"Main2 enables tower");Button(s).onClick.Invoke();Check(tower.Module<IUpgradeControl>().Level==2&&!Button(s).interactable,"Exactly one upgrade, then blocked");
  Check(s.Persistence.Save(),s.Persistence.Status);File.WriteAllText(Root+"/expected.json",JsonUtility.ToJson(s.Persistence.Capture()));s.Persistence.ContinueSaved();return "PASS actual tesla, disabled button and callback guard, main growth, level2 callback, over-level validation rejection; normal scene reload requested";
 }
 public static string Reloaded()
 {
  var s=World();var before=JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText(Root+"/expected.json"));var after=s.Persistence.Capture();before.savedUtc=after.savedUtc;Check(JsonUtility.ToJson(before)==JsonUtility.ToJson(after),"Exact snapshot restore");var tower=s.Content.GroundWorld.Buildings.Single(b=>b.DefinitionId=="defense.tesla");Select(s,tower);Check(tower.Module<IUpgradeControl>().Level==2&&!Button(s).interactable,"Restored cap and UI");
  var bad=JsonUtility.FromJson<SingleMapSnapshot>(JsonUtility.ToJson(after));bad.buildings.Single(b=>b.definition=="defense.tesla").modules.Single(m=>m.type==typeof(PerformanceUpgrade).FullName).values.Single(v=>v.name=="Level").text="3";
  var store=new JsonSaveStore(Root);store.Write(bad.runId,JsonUtility.ToJson(bad));File.WriteAllText(Root+"/bad-current-copy.json",File.ReadAllText(store.CurrentPath));s.Persistence.ContinueSaved();return "PASS actual normal reload with level2 and disabled UI; over-level file reload requested";
 }
 public static string Rejected()
 {
  Check(Application.isPlaying,"Play required");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();s.Persistence.Automatic=false;s.Clock.Paused=true;Check(s.Persistence.Blocked,"Invalid saved level must block restore");Check(s.Persistence.Status.Contains("레벨"),s.Persistence.Status);var store=new JsonSaveStore(Root);Check(File.ReadAllText(store.CurrentPath)==File.ReadAllText(Root+"/bad-current-copy.json"),"Rejected save remains byte identical");return "PASS actual over-level save reload blocked with reason; original file preserved";
 }
}
