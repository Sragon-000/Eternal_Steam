using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class VerifySharedBasePower
{
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    public static string Main()
    {
        var campaign=new CampaignProgression();var objects=new List<UnityEngine.Object>();var buildings=new List<BuildingInstance>();
        T Asset<T>()where T:ScriptableObject{var v=ScriptableObject.CreateInstance<T>();objects.Add(v);return v;}
        BuildingInstance Build(int id,PowerRole role,Vector3 p,bool externalAttack=true){var def=Asset<BuildingDefinition>();def.Id="verify."+id;def.DisplayName=def.Id;
            if(role==PowerRole.Storage){var identity=Asset<BaseModuleDefinition>();identity.Role=BaseRole.Main;def.Modules.Add(identity);var area=Asset<BuildAreaModuleDefinition>();area.Shape=BuildAreaShape.Square;area.Radius=11;area.Yaw=45;def.Modules.Add(area);def.Modules.Add(Asset<MainBaseUpgradeDefinition>());}
            var power=Asset<PowerModuleDefinition>();power.Role=role;power.ExternalAttackAdapter=role==PowerRole.Consumer&&externalAttack;power.Rate=role==PowerRole.Producer?20:5;power.Capacity=200;power.CapacityPerLevel=20;def.Modules.Add(power);
            var b=new BuildingInstance(id,def,Vector2Int.zero,p,new BuildingServices(null,campaign:campaign));b.Activate();buildings.Add(b);return b;}
        try{
            var a=Build(1,PowerRole.Storage,Vector3.zero);var b=Build(2,PowerRole.Storage,new Vector3(100,0,0));
            var producer=Build(3,PowerRole.Producer,new Vector3(3,0,0));var consumer=Build(4,PowerRole.Consumer,new Vector3(5,0,0));var otherConsumer=Build(5,PowerRole.Consumer,new Vector3(103,0,0));
            var registry=new BaseRegistry(2,Quaternion.Euler(0,45,0)){AnyNormalBaseCoverage=true};foreach(var building in buildings)registry.Register(building);
            var simulation=new BasePowerSimulation(registry,2,Quaternion.Euler(0,45,0));simulation.Tick(.5);Check(!CombatPermission.Allows(consumer),"No power before first settlement");simulation.Tick(.5);
            var pa=a.Module<PowerModule>();var pb=b.Module<PowerModule>();Check(pa.Stored==15&&pb.Stored==0,"Independent stores and exact production/consumption");Check(CombatPermission.Allows(consumer)&&!CombatPermission.Allows(otherConsumer),"Both combat gates");
            Check(a.Module<IUpgradeControl>().TryUpgrade(out _),"Shared upgrade");Check(b.Module<IUpgradeControl>().Level==2&&pa.Capacity==220&&pb.Capacity==220&&pa.Stored==15,"Share level not balances");
            var newMapMain=Build(6,PowerRole.Storage,new Vector3(200,0,0));Check(newMapMain.Module<IUpgradeControl>().Level==2,"New map starts shared level");Check(!campaign.TryUpgrade(1),"Reject stale expected level");
            registry.Remove(producer);producer.Dispose();simulation.Tick(4);Check(pa.Stored==0&&!CombatPermission.Allows(consumer),"All-or-nothing shortage");
            registry.Remove(a);a.Dispose();simulation.Refresh();Check(consumer.Module<PowerModule>().Supply==null&&!CombatPermission.Allows(consumer),"Disconnected no supply");
            var overlap=Build(7,PowerRole.Storage,new Vector3(6,0,0));registry.Register(overlap);simulation.Refresh();Check(consumer.Module<PowerModule>().Supply==overlap.Module<PowerModule>(),"Reconnect after destroyed base");
            campaign.MergeMainLevel(7);Check(newMapMain.Module<IUpgradeControl>().Level==7,"Maximum merge");bool invalid=false;try{campaign.MergeMainLevel(11);}catch(ArgumentOutOfRangeException){invalid=true;}Check(invalid,"Invalid legacy level rejected");
            var isolated=new BaseRegistry(2,Quaternion.Euler(0,45,0)){AnyNormalBaseCoverage=true};var isolatedStore=Build(30,PowerRole.Storage,new Vector3(300,0,0));isolated.Register(isolatedStore);var partial=new BasePowerSimulation(isolated,2,Quaternion.Euler(0,45,0));partial.Tick(.5);var lateProducer=Build(31,PowerRole.Producer,new Vector3(302,0,0));isolated.Register(lateProducer);partial.Tick(.5);Check(Math.Abs(isolatedStore.Module<PowerModule>().Stored-10)<.00001,"No production before generator existed");
            var decoration=Build(32,PowerRole.Consumer,new Vector3(304,0,0),false);isolated.Register(decoration);partial.Tick(1);Check(isolatedStore.Module<PowerModule>().Requested==0,"Removed attack module removes demand");double stock=isolatedStore.Module<PowerModule>().Stored;partial.Tick(0);Check(isolatedStore.Module<PowerModule>().Stored==stock,"Paused settlement unchanged");
            return "PASS: attack-free composition; fractional installation timing; shared levels across contexts/new main, stale upgrade, independent power, split dt, shortage, removal/reconnection, capacity without refill, migration bounds";
        }finally{foreach(var b in buildings)b.Dispose();foreach(var o in objects)UnityEngine.Object.DestroyImmediate(o);}
    }
}
