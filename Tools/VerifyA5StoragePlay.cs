using System;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;

// Run only against a temporary, storage-enabled resource.iron definition and an isolated save root.
// The regular catalog must be restored after the Play check.
public static class VerifyA5StoragePlay
{
    public const string Root="/tmp/eternal-a5-storage-play-20260930";
    const double BaseCapacity=1000000,Extra=250;

    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static OpenWorldSandbox World()
    {
        Check(Application.isPlaying&&Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")==Root,"isolated Play required");
        var world=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        Check(world!=null&&world.Persistence!=null&&!world.Persistence.Blocked,"world or save unavailable");
        world.Persistence.Automatic=false;world.Clock.Paused=true;
        return world;
    }
    static BuildingInstance Install(OpenWorldSandbox world,BuildingDefinition definition,string owner,Vector2Int anchor)
    {
        var content=world.Content;
        Check(content.Bases.Select(owner),"select base");
        for(int radius=0;radius<=20;radius++)
            for(int z=anchor.y-radius;z<=anchor.y+radius;z++)
                for(int x=anchor.x-radius;x<=anchor.x+radius;x++)
                {
                    var cell=new Vector2Int(x,z);
                    if(Math.Max(Math.Abs(x-anchor.x),Math.Abs(z-anchor.y))!=radius)continue;
                    if(!content.Bases.Covers(content.GroundWorld.Grid.Center(cell,definition.Footprint),(Vector2)definition.Footprint,WorldGridGeometry.Rotation,owner))continue;
                    if(!content.GroundPlacement.Validate(new PlacementRequest(-99,definition,cell)).Success)continue;
                    if(!content.GroundPlacement.Add(definition,cell,out _).Success)continue;
                    var installed=content.GroundPlacement.Confirm();
                    if(!installed.Success){content.GroundPlacement.Cancel();continue;}
                    var building=content.GroundWorld.Buildings.Single(b=>b.DefinitionId==definition.Id&&b.Cell==cell);
                    content.Bases.Refresh();
                    Check(building.OwnerBaseId==owner&&building.Operational,"installed building owner/operation");
                    return building;
                }
        throw new Exception("no valid operational cell for "+definition.Id+" near "+anchor);
    }
    static string MainId(OpenWorldSandbox world)=>world.Content.MainBase.Module<IBaseIdentity>().BaseId;
    static BuildingInstance[] StorageBuildings(OpenWorldSandbox world)=>world.Content.GroundWorld.Buildings
        .Where(building=>building.DefinitionId=="resource.iron").ToArray();
    static void Save(OpenWorldSandbox world)
    {
        world.Clock.SetPhase(DayPhase.Day);
        Check(world.Persistence.Save(),world.Persistence.Status);
    }
    public static string Prepare()
    {
        var world=World();var content=world.Content;
        Check(content.GroundWorld.Buildings.Count==1,"fresh isolated main base required");
        var iron=world.ContentCatalog.Buildings.Single(building=>building.Id=="resource.iron");
        Check(iron.Modules.OfType<StorageModuleDefinition>().Count()==1&&
            iron.Modules.OfType<StorageModuleDefinition>().Single().ResourceId=="iron"&&
            iron.Modules.OfType<StorageModuleDefinition>().Single().Capacity==Extra,"temporary storage fixture missing");
        var sub=world.ContentCatalog.Buildings.Single(building=>building.Id=="installation.nexus");
        string main=MainId(world);
        var mainStore=Install(world,iron,main,content.MainBase.Cell);
        BuildingInstance subBase=null;
        for(int z=20;z>=12&&subBase==null;z--)
            for(int x=-5;x<=10&&subBase==null;x++)
                if(content.GroundPlacement.Add(sub,new Vector2Int(x,z),out _).Success)
                {
                    var result=content.GroundPlacement.Confirm();
                    if(result.Success)subBase=content.GroundWorld.Buildings.Last();
                    else content.GroundPlacement.Cancel();
                }
        Check(subBase!=null,"sub base placement");
        string subId=subBase.Module<IBaseIdentity>().BaseId;
        var subStore=Install(world,iron,subId,subBase.Cell);
        var first=content.Inventories.Available(main);var second=content.Inventories.Available(subId);
        Check(first.Capacity("iron")==BaseCapacity+Extra&&second.Capacity("iron")==BaseCapacity+Extra,"independent storage capacity");
        Check(first.Deposit("iron",BaseCapacity+100)==BaseCapacity+100&&second.Deposit("iron",23)==23,"test stock deposit");
        Check(content.Resources.Amount("iron")==0&&StorageBuildings(world).Length==2,"common stock or storage count");
        Save(world);
        world.Persistence.ContinueSaved();
        return "PASS installed 2 storage-enabled buildings, each base +250 capacity, isolated save and reload requested";
    }
    public static string Reloaded()
    {
        var world=World();var content=world.Content;string main=MainId(world);
        var stores=StorageBuildings(world);Check(stores.Length==2,"two storage buildings restored");
        var subId=stores.Single(building=>building.OwnerBaseId!=main).OwnerBaseId;
        var first=content.Inventories.Available(main);var second=content.Inventories.Available(subId);
        Check(first.Capacity("iron")==BaseCapacity+Extra&&first.Amount("iron")==BaseCapacity+100,"main capacity/stock restored");
        Check(second.Capacity("iron")==BaseCapacity+Extra&&second.Amount("iron")==23,"sub capacity/stock restored");
        content.GroundWorld.Remove(stores.Single(building=>building.OwnerBaseId==main).Id);
        Check(first.Capacity("iron")==BaseCapacity&&first.Amount("iron")==BaseCapacity+100,"main storage removal preserves overflow");
        Check(second.Capacity("iron")==BaseCapacity+Extra&&second.Amount("iron")==23,"sub storage remains independent");
        Save(world);
        world.Persistence.ContinueSaved();
        return "PASS reloaded 2 capacities; removed main storage, kept overflow and sub capacity; second reload requested";
    }
    public static string RemovedReloaded()
    {
        var world=World();var content=world.Content;string main=MainId(world);
        var stores=StorageBuildings(world);Check(stores.Length==1,"only sub storage restored");
        var subId=stores[0].OwnerBaseId;
        Check(subId!=main,"remaining storage owner");
        var first=content.Inventories.Available(main);var second=content.Inventories.Available(subId);
        Check(first.Capacity("iron")==BaseCapacity&&first.Amount("iron")==BaseCapacity+100,"removed main storage capacity and overflow");
        Check(second.Capacity("iron")==BaseCapacity+Extra&&second.Amount("iron")==23,"sub storage capacity and stock");
        return "PASS storage installation, first reload, removal, overflow, second reload across two bases";
    }
}
