using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam;
using EternalSteam.Demo;
using EternalSteam.OpenWorld;

// Isolated paid-policy fixture; these are editing/HUD commands, not OS pointer evidence.
public static class VerifyConstructionCosts
{
    const string Root="/tmp/eternal-construction-20261001";
    static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    static OpenWorldSandbox World(){Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")==Root,"Isolated Play required");var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();s.Persistence.Automatic=false;s.Clock.Paused=true;return s;}
    static ResourceCost Cost(string id,double value)=>new(){ResourceId=id,Amount=value};
    static void Entry(ConstructionCostTable t,string id,params ResourceCost[] costs)=>t.Entries.Add(new(){BuildingId=id,Costs=new(costs)});
    static void Stock(ResourceBank b,string id,double amount){b.Withdraw(id,b.Amount(id));if(b.Capacity(id)<amount)b.AddCapacity(id,amount);b.Deposit(id,amount);}
    static WorldEditSession.PendingContent Reserve(OpenWorldSandbox s,BuildingDefinition d)
    {
        var edit=s.GetComponent<OpenWorldInput>().Edits;
        for(int y=5;y<35;y++)for(int x=-25;x<25;x++){
            var point=s.Content.GroundWorld.Grid.Center(new Vector2Int(x,y),d.Footprint);
            if(edit.AddContent(d,point,Vector3.forward,out _))return edit.ContentPending.Last();
        }
        throw new Exception("No valid cell for "+d.Id+"; "+edit.Quote(d).Reason);
    }
    public static string Main()
    {
        var s=World();var c=s.Content;var input=s.GetComponent<OpenWorldInput>();input.Cancel();
        var main=c.MainBase;var mainId=main.Module<IBaseIdentity>().BaseId;var bank=c.Inventories.Available(mainId);
        while(main.Module<IUpgradeControl>().Level<3)Check(s.Persistence.Upgrades.TryUpgrade(main,out var why),why);
        var generator=s.ContentCatalog.Buildings.Single(d=>d.Id=="resource.iron");
        var subDefinition=s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.nexus");
        input.BeginEditing();Reserve(s,subDefinition);Check(input.Confirm(),s.Message);
        var sub=c.GroundWorld.Buildings.Single(b=>b.Module<IBaseRole>()?.Role==BaseRole.Sub);var subId=sub.Module<IBaseIdentity>().BaseId;var subBank=c.Inventories.Available(subId);c.Bases.Select(mainId);
        Check(c.ConstructionCosts is VerificationFreeConstruction,"Default free policy missing");
        input.BeginEditing();double freeBefore=bank.Amount("iron");Reserve(s,generator);Check(input.Edits.Quote().VerificationFreeItems==1,"Free quote must be explicit");Check(input.Confirm(),s.Message);Check(bank.Amount("iron")==freeBefore,"Default general construction charged");
        var freeConfiguration=SaveConfiguration.Compute(s);
        var table=ScriptableObject.CreateInstance<ConstructionCostTable>();Entry(table,generator.Id,Cost("iron",4),Cost("iron",3),Cost("copper",2));
        Entry(table,ConstructionCostTable.FoundationId,Cost("copper",3));Entry(table,s.Foundations.Definition(HordeTowerKind.MachineGun).Id,Cost("iron",2),Cost("copper",1));
        s.ConstructionCosts=table;Check(SaveConfiguration.Compute(s)!=freeConfiguration,"Paid table did not change configuration fingerprint");s.ConstructionCosts=null;
        c.ConstructionCosts=table;Stock(bank,"iron",30);Stock(bank,"copper",10);Stock(subBank,"iron",80);Stock(subBank,"copper",80);
        input.BeginEditing();var pending=Reserve(s,generator);var second=Reserve(s,generator);var track=Reserve(s,s.RailwayHud.Track);
        var quote=input.Edits.Quote();Check(quote.Affordable&&quote.Iron==15&&quote.Lines.Single(v=>v.Resource=="copper").Required==4,"Mixed quote/deduplication failed");
        int built=c.GroundWorld.Buildings.Count;bank.Withdraw("copper",7);
        Check(!input.Confirm()&&input.Edits.ContentPending.Count==3&&bank.Amount("iron")==30&&bank.Amount("copper")==3&&c.GroundWorld.Buildings.Count==built,"Shortage changed stocks or reservations");
        bank.Deposit("copper",7);c.Bases.Select(subId);Check(!input.Confirm()&&bank.Amount("iron")==30&&subBank.Amount("iron")==80&&input.Edits.Count==3,"Selection change redirected payer");c.Bases.Select(mainId);
        var cell=second.Request.Cell;c.GroundWorld.Grid.SetBlocked(cell,true);Check(!input.Confirm()&&bank.Amount("iron")==30&&bank.Amount("copper")==10&&input.Edits.Count==3,"Placement failure charged stock");c.GroundWorld.Grid.SetBlocked(cell,false);
        c.PrepareRestoredBuilding=_=>throw new InvalidOperationException("Injected factory failure");Check(!input.Confirm()&&bank.Amount("iron")==30&&bank.Amount("copper")==10&&c.GroundWorld.Buildings.Count==built&&input.Edits.Count==3,"Factory exception did not roll back");c.PrepareRestoredBuilding=null;
        Check(input.Confirm(),s.Message);Check(bank.Amount("iron")==15&&bank.Amount("copper")==6&&subBank.Amount("iron")==80&&subBank.Amount("copper")==80&&c.GroundWorld.Buildings.Count==built+3,"Exact mixed payment failed");
        Check(c.GroundWorld.Buildings.Where(b=>b.DefinitionId==generator.Id).All(b=>b.OwnerBaseId==mainId),"Installed owner diverged from payer");
        input.BeginEditing();Reserve(s,generator);Stock(bank,"iron",0);Stock(bank,"copper",0);c.InfiniteResources=true;Check(input.Edits.Quote().Affordable,"Infinite quote failed");c.InfiniteResources=false;Check(!input.Confirm()&&input.Edits.Count==1,"Infinite OFF did not recheck");
        c.InfiniteResources=true;Check(input.Confirm(),s.Message);Check(bank.Amount("iron")==0&&bank.Amount("copper")==0,"Infinite construction changed real stocks");c.InfiniteResources=false;
        Stock(bank,"iron",20);Stock(bank,"copper",10);input.BeginEditing();Reserve(s,generator);input.Cancel();Check(bank.Amount("iron")==20&&bank.Amount("copper")==10,"Cancel charged stocks");
        input.BeginEditing();bool slab=false;
        for(int y=-50;y<60&&!slab;y+=4)for(int x=-50;x<60&&!slab;x+=4)slab=input.Edits.AddFoundation(WorldGridGeometry.Center(new Vector2Int(x,y),2),out _);
        Check(slab&&input.Edits.Quote().Lines.Single().Required==3,"Foundation cost path failed");Check(input.Confirm(),s.Message);Check(bank.Amount("copper")==7,"Foundation not charged exactly");
        var platform=s.Foundations.Platforms.Last();var point=platform.World.Grid.Center(new Vector2Int(1,1),Vector2Int.one);input.BeginEditing();
        Check(input.Edits.AddTower(point,Vector3.forward,HordeTowerKind.MachineGun,out var reason),reason);Check(input.Confirm(),s.Message);Check(bank.Amount("iron")==18&&bank.Amount("copper")==6,"Legacy tower not charged exactly");
        // Pending payment retains its original owner when that base disappears.
        c.Bases.Select(subId);input.BeginEditing();Reserve(s,generator);sub.Module<HealthModule>().ApplyDamage(100000);Check(!input.Confirm()&&input.Edits.Count==1&&subBank.Amount("iron")==80,"Lost payer silently redirected");input.Cancel();c.Bases.Select(mainId);
        c.ConstructionCosts=new VerificationFreeConstruction();
        Check(SaveConfiguration.Compute(s)==freeConfiguration,"Unassigned-policy save compatibility changed");Check(s.Persistence.Save(),s.Persistence.Status);
        File.WriteAllText(Root+"/expected.json",JsonUtility.ToJson(s.Persistence.Capture()));
        File.WriteAllText(Root+"/checks.json",JsonUtility.ToJson(new Evidence{passed=true,mixedIron=15,mixedCopper=4,mainAfterMixedIron=15,mainAfterMixedCopper=6,subIron=80,subCopper=80,foundationCopper=3,legacyTowerIron=2,legacyTowerCopper=1}));
        s.Persistence.ContinueSaved();
        return "PASS default free, paid multi-resource/mixed rail quote, shortage/selection/terrain/factory rejection, exact payment/owner, infinite ON-OFF, cancel, foundation/legacy tower, lost payer; saved and reloading";
    }
    [Serializable] public sealed class Evidence{public bool passed;public double mixedIron,mixedCopper,mainAfterMixedIron,mainAfterMixedCopper,subIron,subCopper,foundationCopper,legacyTowerIron,legacyTowerCopper;}
    public static string Reloaded()
    {
        var s=World();var expected=JsonUtility.FromJson<SingleMapSnapshot>(File.ReadAllText(Root+"/expected.json"));var actual=s.Persistence.Capture();expected.savedUtc=actual.savedUtc;
        expected.buildings=expected.buildings.OrderBy(b=>b.id).ToList();actual.buildings=actual.buildings.OrderBy(b=>b.id).ToList();
        Check(JsonUtility.ToJson(expected)==JsonUtility.ToJson(actual),"Paid construction snapshot mismatch");
        return "PASS paid buildings, legacy tower/foundation, base inventories and retired payer exact save/reload; no repeated charge";
    }
}
