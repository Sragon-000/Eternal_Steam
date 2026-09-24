using System;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class VerifyBasePlanning
{
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 public static string Main(){
 var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(Application.isPlaying&&s.BasePlanningRules,"New start configuration required");var input=s.GetComponent<OpenWorldInput>();input.Cancel();
 var mainDef=s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.main_base");var subDef=s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.nexus");
 Check(!mainDef.Recoverable&&subDef.Footprint==new Vector2Int(3,3),"Authored roles");
 bool Reserve(BuildingDefinition d){for(int z=-30;z<40;z++)for(int x=-30;x<40;x++){if(s.Content.GroundPlacement.Add(d,new Vector2Int(x,z),out _).Success)return true;}return false;}
 try{
 if(s.StartingBase==null){Check(!Reserve(subDef),"Sub requires confirmed main");Check(Reserve(mainDef),"Main reservation");Check(!Reserve(mainDef),"Duplicate pending main rejected");Check(s.Content.GroundPlacement.Confirm().Success,"Main commit");}else Check(s.Content.MainBase!=null&&!Reserve(mainDef),"Fixed main already installed and cannot be selected again");var main=s.Content.MainBase;Check(main!=null&&s.Content.LevelCap==1&&s.Content.SubLimit==5,"Actual main level authority");
 {var recovery=new RecoverySession(s.Content.GroundWorld);Check(!recovery.Toggle(main.Id),"Main cannot be recovered");}
 for(int i=0;i<5;i++)Check(Reserve(subDef),"Sub reservation "+i);Check(!Reserve(subDef),"Sixth sub reservation rejected at Lv1");Check(s.Content.GroundPlacement.Confirm().Success,"Five subs committed");
 var subs=s.Content.GroundWorld.Buildings.Where(b=>b.Module<IBaseRole>()?.Role==BaseRole.Sub).ToArray();Check(subs.Length==5,"Sub count");var sub=subs[0];Check(!sub.Module<IUpgradeControl>().TryUpgrade(out _),"Sub level capped by main");
 Check(main.Module<IUpgradeControl>().TryUpgrade(out _),"Main upgrade");Check(s.Content.SubLimit==10,"Lv2 sub limit ten");for(int i=0;i<5;i++)Check(Reserve(subDef),"Additional sub "+i);Check(!Reserve(subDef),"Eleventh sub rejected");Check(s.Content.GroundPlacement.Confirm().Success,"Ten subs commit");Check(sub.Module<IUpgradeControl>().TryUpgrade(out _),"Sub upgrade");Check(sub.Module<IBuildArea>().Radius==12&&Math.Abs(sub.Module<HealthModule>().Maximum-120)<.01f,"Sub area and linear health");Check(main.Module<IBuildArea>().Radius==11,"Main area fixed");
 for(int i=2;i<10;i++)Check(main.Module<IUpgradeControl>().TryUpgrade(out _),"Main to ten");for(int i=2;i<10;i++)Check(sub.Module<IUpgradeControl>().TryUpgrade(out _),"Sub to ten");Check(sub.Module<IBuildArea>().Radius==20&&Math.Abs(sub.Module<HealthModule>().Maximum-280)<.01f,"Sub max area/health");Check(!main.Module<IUpgradeControl>().TryUpgrade(out _),"Main max ten");
 // Synthetic placement isolates overlapping base coverage from the small test map's cliffs.
 var registry=new BaseRegistry(2,WorldGridGeometry.Rotation){AnyNormalBaseCoverage=true};var services=new BuildingServices(null,levelLimit:s.Content);
 var a=new BuildingInstance(800,subDef,Vector2Int.zero,Vector3.zero,services);var b=new BuildingInstance(801,subDef,Vector2Int.zero,Vector3.zero,services);
 var def=UnityEngine.Object.Instantiate(s.ContentCatalog.Buildings.Single(d=>d.Id=="resource.iron"));var profile=UnityEngine.Object.Instantiate(def.Placement);profile.RequiresOwnerBase=true;profile.RequiresOperationalArea=true;def.Placement=profile;var producer=new BuildingInstance(802,def,Vector2Int.zero,Vector3.zero,new BuildingServices(null,resources:s.Content.Resources));
 try{a.Activate();registry.Register(a);producer.Activate();registry.Register(producer);b.Activate();registry.Register(b);registry.Refresh();Check(producer.Operational,"Initial covered");registry.Remove(a);a.Dispose();registry.Refresh();Check(producer.Operational,"Other base maintains operation despite lost owner");registry.Remove(b);b.Dispose();registry.Refresh();Check(!producer.Operational&&!producer.Disposed,"No coverage disables but preserves building");}finally{a.Dispose();b.Dispose();producer.Dispose();UnityEngine.Object.Destroy(def);UnityEngine.Object.Destroy(profile);}
 sub.Module<HealthModule>().ApplyDamage(10000);Check(!s.Content.Defeated,"Sub destruction does not fail map");main.Module<HealthModule>().ApplyDamage(10000);Check(s.Content.Defeated&&!s.Spawn("1"),"Main destruction ends map and blocks spawn");Check(!Reserve(mainDef),"Cannot reinstall after failure");
 return "PASS: one main including reservations, no recovery, main Lv1 sub cap5/Lv2 cap10, sub level cap, 11..20 range, HP100..280, overlapping bases preserve operation, only main destruction fails map, no reinstall.";
 }finally{input.Cancel();s.Content.GroundPlacement.Cancel();foreach(var b in s.Content.GroundWorld.Buildings.ToArray())s.Content.GroundWorld.Remove(b.Id);}
 }
}
