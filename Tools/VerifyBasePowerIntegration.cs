using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class VerifyBasePowerIntegration
{
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    public static string Main()
    {
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();Check(Application.isPlaying,"Play required");s.GetComponent<OpenWorldInput>().Cancel();s.Clock.Paused=true;
        // A live Editor may have advanced a partial second before this test starts.
        // Align the empty supply to its settlement boundary before adding test devices.
        double phase=(double)typeof(BasePowerSimulation).GetField("elapsed",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(s.Content.Power);
        if(phase>0)s.Content.Power.Tick(1-phase);
        bool Install(string id,out BuildingInstance installed){installed=null;var def=s.ContentCatalog.Buildings.Single(d=>d.Id==id);for(int z=-30;z<40;z++)for(int x=-30;x<40;x++){
            if(id!="installation.main_base"&&!s.Content.HasBuildArea(s.Content.GroundWorld.Grid.Center(new Vector2Int(x,z),def.Footprint),def.Footprint))continue;
            if(!s.Content.GroundPlacement.Add(def,new Vector2Int(x,z),out _).Success)continue;
            Check(s.Content.GroundPlacement.Confirm().Success,"Commit "+id);installed=s.Content.GroundWorld.Buildings.Last();return true;
        }return false;}
        var main=s.Content.MainBase;if(main==null)Check(Install("installation.main_base",out main),"Main placement");Check(Install("resource.power_generator",out var generator),"Generator placement");
        var definition=s.ContentCatalog.Buildings.First(d=>d.Modules.Any(m=>m is WeaponModuleDefinition)&&d.Placement.RequiredNexusLevel==1);
        Check(Install(definition.Id,out var tower),"Powered tower placement");Check(!CombatPermission.Allows(tower),"Initially unpowered");s.Content.Power.Tick(1);Check(CombatPermission.Allows(tower),"Supply enables weapon");
        var storage=main.Module<PowerModule>();Check(storage.Stored==15&&storage.Production==20&&storage.Consumed==5,"Real storage settlement");
        var recovery=new RecoverySession(s.Content.GroundWorld);Check(recovery.Toggle(generator.Id),"Recovery schedule");s.Content.Power.Tick(1);Check(storage.Stored==30,"Pending recovery still produces");recovery.Cancel();Check(!generator.Disposed,"Recovery cancel preserves");
        Check(recovery.Toggle(generator.Id)&&recovery.Confirm().Success,"Generator recovery commit");s.Content.Power.Tick(7);Check(!CombatPermission.Allows(tower)&&storage.Stored==0&&!tower.Disposed,"Power loss preserves building");
        var validator=new SpawnAreaValidator(s.Ground,s.Content);Check(validator.Check(s.AssaultSettings.BossPosition,3,out _),"Authored whole 3x3 is valid");
        var cell=s.Content.GroundWorld.Grid.WorldToCell(s.AssaultSettings.BossPosition);Check(!s.Content.CheckGround(cell,Vector2Int.one,out _,out _),"Reserved boss area blocks building");
        Check(!validator.Check(main.Position,1,out _),"Existing building blocks spawn footprint");Check(!validator.Check(new Vector3(-999,0,-999),3,out _),"Out-of-map 3x3 rejected");
        Check(s.LegacyPower!=null,"Legacy adapter configured");var ui=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>().GetComponent<UIDocument>().rootVisualElement;Check(ui.Q<Label>("base-power")!=null&&ui.Q<Label>("device-power")!=null,"Authored power UI");
        return "PASS: real generator/tower installation, production, recovery cancel/commit, power shortage without destruction, 3x3 area validation and placement reservation, UI";
    }
}
