using System;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;

public static class VerifyQa02Batch
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static string Main()
    {
        Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")=="/tmp/eternal-railway-a1","Isolated Play required");
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();s.Persistence.Automatic=false;s.Clock.Paused=true;
        var c=s.Content;var input=s.GetComponent<OpenWorldInput>();input.Cancel();
        var main=c.GroundWorld.Buildings.Single(b=>b.Module<IBaseRole>()?.Role==BaseRole.Main);
        var bank=c.Inventories.Available(main.Module<IBaseIdentity>().BaseId);c.Bases.Select(main.Module<IBaseIdentity>().BaseId);
        var track=s.RailwayHud.Track;input.BeginEditing();int found=0;
        for(int z=-25;z<35&&found<2;z++)for(int x=-25;x<35&&found<2;x++)
        {
            var point=c.GroundWorld.Grid.Center(new Vector2Int(x,z),Vector2Int.one);
            if(input.Edits.AddContent(track,point,Vector3.forward,out _))found++;
        }
        Check(found==2&&input.Edits.ContentPending.Count==2,"Two valid track reservations required");
        int built=c.GroundWorld.Buildings.Count,occupied=c.GroundWorld.Grid.OccupiedCount;
        var cell=input.Edits.ContentPending[1].Request.Cell;
        c.GroundWorld.Grid.SetBlocked(cell,true);
        var blocked=input.Edits.Confirm();c.GroundWorld.Grid.SetBlocked(cell,false);
        Check(!blocked.Success&&c.GroundWorld.Buildings.Count==built&&c.GroundWorld.Grid.OccupiedCount==occupied&&input.Edits.ContentPending.Count==2,"Blocked cell must reject entire batch without dropping reservations");
        double original=bank.Amount("iron");if(original>1)bank.Withdraw("iron",original-1);
        var poor=input.Edits.Confirm();Check(!poor.Success&&poor.Code=="resource"&&bank.Amount("iron")==1&&c.GroundWorld.Buildings.Count==built&&input.Edits.ContentPending.Count==2,"Shortage must reject entire batch without charge");
        bank.Deposit("iron",2);var paid=input.Edits.Confirm();Check(paid.Success,paid.Message);input.Cancel();
        Check(c.GroundWorld.Buildings.Count==built+2&&c.GroundWorld.Grid.OccupiedCount==occupied+2&&bank.Amount("iron")==1,"Batch must install both tracks and charge exactly two iron");
        return "PASS two-track occupancy and resource rejection preserve all reservations; funded confirm installs two and charges iron 2";
    }
    public static string CountLimit()
    {
        Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")=="/tmp/eternal-railway-a1","Isolated Play required");
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();s.Persistence.Automatic=false;s.Clock.Paused=true;
        var c=s.Content;var input=s.GetComponent<OpenWorldInput>();input.Cancel();input.BeginEditing();
        var sub=s.ContentCatalog.Buildings.Single(definition=>definition.Id=="installation.nexus");
        int existing=c.GroundWorld.Buildings.Count(b=>b.Module<IBaseRole>()?.Role==BaseRole.Sub),limit=c.SubLimit;
        int added=0;bool rejected=false;
        try
        {
            for(int z=-35;z<55&&!rejected;z++)for(int x=-35;x<55&&!rejected;x++)
            {
                var point=c.GroundWorld.Grid.Center(new Vector2Int(x,z),sub.Footprint);
                if(input.Edits.AddContent(sub,point,Vector3.forward,out var reason))added++;
                else if(reason!=null&&reason.Contains("한도"))rejected=true;
            }
            Check(added==limit-existing&&rejected&&input.Edits.ContentPending.Count==added,"Sub-base reservation count limit did not reject the next valid candidate");
            Check(c.Bases.Bases.Count==1,"Reservations installed a base before confirmation");
            return $"PASS {added} sub-base reservations, next candidate rejected at limit {limit}, no early installation";
        }
        finally{input.Cancel();}
    }
}
