using System;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;

public static class VerifyQa04Overlap
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}

    public static string Run()
    {
        Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")=="/tmp/eternal-qa04-overlap","Isolated Play required");
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        Check(s!=null&&!s.Persistence.Blocked,"World unavailable");
        s.Persistence.Automatic=false;s.Clock.Paused=true;
        var c=s.Content;
        var main=c.GroundWorld.Buildings.Single(b=>b.Module<IBaseRole>()?.Role==BaseRole.Main);
        var sub=c.GroundWorld.Buildings.Single(b=>b.Module<IBaseRole>()?.Role==BaseRole.Sub);
        var mainId=main.Module<IBaseIdentity>().BaseId;
        var subId=sub.Module<IBaseIdentity>().BaseId;
        var station=c.GroundWorld.Buildings.Single(b=>b.OwnerBaseId==subId&&b.Module<RailFacility>()?.Kind==RailFacilityKind.Station);
        var mainArea=main.Module<IBuildArea>();
        bool covered=mainArea.Contains(station.Position,(Vector2)station.Footprint*.5f,WorldGridGeometry.Rotation);
        Check(covered,"Sub-owned station is not covered by main base");
        var stock=c.Inventories.Available(subId);
        Check(stock!=null&&c.Railway.Network.StationActive(station.PersistentId),"Station must be active before loss");
        stock.Deposit("iron",17);
        var expected=stock.Amount("iron");
        sub.Module<HealthModule>().ApplyDamage(100000);
        c.Bases.Refresh();c.Railway.Refresh();
        Check(sub.Disposed&&c.Bases.Bases.ContainsKey(mainId)&&!c.Bases.Bases.ContainsKey(subId),"Sub-base loss was not applied");
        Check(station.OwnerBaseId==subId&&station.Operational&&station.OperationBlock==OperationBlock.None,"Covered station should retain ownership and area operation");
        Check(c.Inventories.Available(subId)==null&&c.Inventories.Ensure(subId).Amount("iron")==expected,"Old ledger should be inaccessible but retained");
        Check(!c.Railway.Network.StationActive(station.PersistentId),"Rail logistics must stop without its owner ledger");
        var input=UnityEngine.Object.FindFirstObjectByType<OpenWorldInput>();
        Check(input!=null&&input.ClickWorld(station.Position)&&input.SelectedContent==station,"Could not select surviving station");
        var hud=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();
        Check(hud!=null,"Authored Canvas HUD missing");hud.Refresh();
        var stats=hud.Texts.Single(binding=>binding.Id=="selection-stats").View.text;
        Check(stats.Contains("영역 내 가동 · 소속 기지 상실")&&stats.Contains("원소속 재고 이용 불가 · 생산/역 운송 중단")&&stats.Contains("소속 기지 상실"),"Canvas status does not explain the split state: "+stats);
        return $"PASS overlap: owner retained, area operational={station.Operational}, railway active={c.Railway.Network.StationActive(station.PersistentId)}, old iron={expected}; Canvas={stats.Replace('\n','|')}";
    }
}
