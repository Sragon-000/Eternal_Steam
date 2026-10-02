using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;
using Newtonsoft.Json.Linq;

public static class VerifyRailwayShuttleCompatibility
{
    const string Report="Docs/Validation/2026-10-02-railway-shuttle-play.json";
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static OpenWorldSandbox Sandbox(){var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")?.StartsWith("/tmp/eternal-")==true,"Isolated Play required");s.Persistence.Automatic=false;s.Clock.Paused=true;return s;}
    static void Record(string key){var report=JObject.Parse(File.ReadAllText(Report));report[key]=true;File.WriteAllText(Report,report.ToString());}
    public static string PrepareLegacy()
    {
        var s=Sandbox();var c=s.Content;var n=c.Railway.Network;var r=n.Routes.Single();n.Stop(r);Check(!r.train.segmentPaid,"Stationary fixture");
        var origin=n.Station(r.stops[0].stationId).Cell;string owner=n.Station(r.stops[0].stationId).OwnerBaseId;var input=s.GetComponent<OpenWorldInput>();
        var cells=new List<Vector2Int>();for(int y=1;y<=4;y++)cells.Add(new Vector2Int(8,y));for(int x=7;x>=-1;x--)cells.Add(new Vector2Int(x,4));for(int y=3;y>=0;y--)cells.Add(new Vector2Int(-1,y));
        foreach(var cell in cells){c.Bases.Select(owner);input.BeginEditing();Check(input.Edits.AddContent(s.RailwayHud.Track,c.GroundWorld.Grid.Center(cell+origin,Vector2Int.one),WorldGridGeometry.Rotation*Vector3.forward,out var why),why);var result=input.Edits.Confirm();Check(result.Success,result.Message);input.Cancel();}
        c.Railway.Refresh();var saved=n.Capture();var old=saved.routes.Single();old.mode=RailRouteMode.LegacyCycle;old.train.reverse=false;foreach(var stop in old.stops){stop.arrival=0;stop.departure=1;}
        Check(n.ValidateDraft(old.stops,old.id,out var legs,out var error),error);old.legs=legs;n.Restore(saved);
        var d=s.Persistence.Capture();d.version=2;s.Persistence.Validate(d);string payload=JsonUtility.ToJson(d);var json=JObject.Parse(payload);
        foreach(var route in json["railway"]["routes"]){((JObject)route).Remove("mode");((JObject)route["train"]).Remove("reverse");foreach(var leg in route["legs"]){((JObject)leg).Remove("startPort");((JObject)leg).Remove("endPort");}}
        payload=json.ToString();File.WriteAllText("/tmp/eternal-rx02-legacy-v2.json",payload);new JsonSaveStore(Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")).Write(d.runId,payload);s.Persistence.ContinueSaved();return "Synthetic original-field v2 cyclic save written; actual initialization reload requested";
    }
    public static string VerifyLegacy()
    {
        var s=Sandbox();Check(!s.Persistence.Blocked,s.Persistence.Status);var before=JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText("/tmp/eternal-rx02-legacy-v2.json"));var after=s.Persistence.Capture();before.version=3;before.savedUtc=after.savedUtc;
        Check(JsonUtility.ToJson(before)==JsonUtility.ToJson(after),"Legacy v2 exact state retained in v3");var r=s.Content.Railway.Network.Routes.Single();Check(r.mode==RailRouteMode.LegacyCycle&&r.legs.Count==2&&!r.train.reverse,"Legacy missing mode remains cyclic");Check(s.Persistence.Save(),s.Persistence.Status);Record("legacyV2ReloadExactAndV3Write");return "PASS original-field v2 cycle actual reload and v3 write preserve all states";
    }
    public static string VerifyV1()
    {
        var s=Sandbox();Check(!s.Persistence.Blocked,s.Persistence.Status);var d=s.Persistence.Capture();string original=File.ReadAllText("/tmp/eternal-railway-v1-payload.json");string main=s.Content.MainBase.Module<IBaseIdentity>().BaseId;
        Check(d.version==3&&d.migratedVersion==1&&d.migratedFrom==JsonSaveStore.Digest(original),"v1 source marker in v3");Check(d.inventories.Single(i=>i.baseId==main).stocks.Single(v=>v.id=="iron").amount==1234,"v1 overflow preserved at main");Check(d.inventories.Where(i=>i.baseId!=main).SelectMany(i=>i.stocks).All(v=>v.amount==0),"No stock duplicated at sub");
        Check(s.Persistence.Save(),s.Persistence.Status);var root=Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT");var sources=Directory.GetFiles(Path.Combine(root,"migrations"),"source-payload.json",SearchOption.AllDirectories);Check(sources.Length==1&&File.ReadAllText(sources[0])==original,"v1 source retained");
        Record("legacyV1InitializationPreservesOverflowAndSource");s.Persistence.ContinueSaved();return "PASS v1 stock/source preserved and v3 write; reload next";
    }
    public static string VerifyV1Reload()
    {
        var s=Sandbox();var d=s.Persistence.Capture();string main=s.Content.MainBase.Module<IBaseIdentity>().BaseId;Check(!s.Persistence.Blocked&&d.version==3&&d.inventories.Single(i=>i.baseId==main).stocks.Single(v=>v.id=="iron").amount==1234,"No duplicate stock on v3 reload");Record("v1ToV3SecondReloadNoDuplicateStock");return "PASS v1→v3 second reload retains stock once";
    }
}
