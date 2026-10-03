using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using EternalSteam;using EternalSteam.OpenWorld;using EternalSteam.Railway;
public static class BuildHud06CFixture{
static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
static OpenWorldSandbox Sandbox(){if(!Application.isPlaying||!(Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")??"").Contains("hud-06c"))throw new Exception("Isolated Play required");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();s.Persistence.Automatic=false;s.Clock.Paused=true;return s;}
public static string Main(){
        var s=Sandbox();Check(!s.Persistence.Blocked,s.Persistence.Status);var c=s.Content;var input=s.GetComponent<OpenWorldInput>();input.Cancel();
        Check(c.GroundWorld.Buildings.Count==1,"Fresh fixture required");var overview=s.RailwayHud.GetComponent<RailwayOverview>();overview.Hud.Groups.Select(true);s.RailwayHud.Execute("open");overview.Apply();Check(overview.Empty.gameObject.activeSelf&&overview.Rows.All(b=>!b.gameObject.activeSelf),"Empty overview state");File.WriteAllText("Docs/Measurements/2026-10-02-hud-06c/empty-state.txt","PASS empty route list and authored rows hidden");string main=c.MainBase.Module<IBaseIdentity>().BaseId;
        var subDef=s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.nexus");BuildingInstance sub=null;
        for(int z=20;z>=12&&sub==null;z--)for(int x=-5;x<10&&sub==null;x++)if(c.GroundPlacement.Add(subDef,new Vector2Int(x,z),out _).Success){var result=c.GroundPlacement.Confirm();Check(result.Success,result.Message);sub=c.GroundWorld.Buildings.Last();}
        Check(sub!=null,"Sub placement");string secondary=sub.Module<IBaseIdentity>().BaseId;c.Bases.Refresh();
        var aBank=c.Inventories.Available(main);var bBank=c.Inventories.Available(secondary);aBank.Deposit("iron",500);aBank.Deposit("coal",100);bBank.Deposit("iron",100);
        var station=s.RailwayHud.Station;var track=s.RailwayHud.Track;
        var cells=new[]{new Vector2Int(2,1),new Vector2Int(3,1),new Vector2Int(4,1),new Vector2Int(5,1),new Vector2Int(5,0)};
        var legacyReturn=new List<Vector2Int>();for(int y=1;y<=4;y++)legacyReturn.Add(new Vector2Int(8,y));for(int x=7;x>=-1;x--)legacyReturn.Add(new Vector2Int(x,4));for(int y=3;y>=0;y--)legacyReturn.Add(new Vector2Int(-1,y));
        Vector2Int? origin=null;
        for(int z=12;z<26&&!origin.HasValue;z++)for(int x=-5;x<8&&!origin.HasValue;x++){
            var o=new Vector2Int(x,z);if(!c.HasBuildArea(c.GroundWorld.Grid.Center(o,station.Footprint),station.Footprint)||!c.HasBuildArea(c.GroundWorld.Grid.Center(o+new Vector2Int(6,0),station.Footprint),station.Footprint))continue;if(!c.GroundPlacement.Validate(new PlacementRequest(-1,station,o)).Success||!c.GroundPlacement.Validate(new PlacementRequest(-2,station,o+new Vector2Int(6,0))).Success)continue;
            if(legacyReturn.All(p=>c.GroundPlacement.Validate(new PlacementRequest(-4,track,p+o)).Success)&&cells.All(p=>c.GroundPlacement.Validate(new PlacementRequest(-3,track,p+o)).Success))origin=o;
        }
        Check(origin.HasValue,"Terrain-valid single connection fixture");
        BuildingInstance Install(BuildingDefinition def,Vector2Int cell,string owner){c.Bases.Select(owner);input.BeginEditing();double before=c.Inventories.Available(owner).Amount("iron");Check(input.Edits.AddContent(def,c.GroundWorld.Grid.Center(cell,Vector2Int.one),WorldGridGeometry.Rotation*Vector3.forward,out var why),why);var result=input.Edits.Confirm();Check(result.Success,result.Message);Check(c.Inventories.Available(owner).Amount("iron")==before-(def==station?10:1),"Charge once per actual new building");input.Cancel();return c.GroundWorld.Buildings.Last();}
        var a=Install(station,origin.Value,main);var b=Install(station,origin.Value+new Vector2Int(6,0),secondary);Install(track,origin.Value+new Vector2Int(3,1),main);
        c.Railway.Refresh();Check(s.RailwayHud.OpenFromStation(a.PersistentId),"Open station");
        return "Created actual two-base, two-station fixture with one reusable track for connection UI";
}}
