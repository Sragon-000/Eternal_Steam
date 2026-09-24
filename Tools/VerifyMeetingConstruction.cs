using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Demo;
public static class VerifyMeetingConstruction
{
    sealed class Receiver:IDamageReceiver {public bool Alive=>true;public int Hits;public void ApplyDamage(float n){Hits++;}}
    static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    public static string Main()
    {
        Check(Application.isPlaying,"Play required");
        var sandbox=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        Check(sandbox.MeetingConstructionRules,"Start region scene required");
        var root=new GameObject("Meeting isolated verification");
        var data=new TerrainData{heightmapResolution=33,size=new Vector3(600,10,600)};
        var ground=Terrain.CreateTerrainGameObject(data);ground.transform.position=new Vector3(2000,0,2000);
        var terrain=ground.GetComponent<Terrain>();var targets=new TargetRegistry();var receiver=new Receiver();
        var towers=new List<HordeTower>();
        var content=new OpenWorldContent(terrain,root.transform,sandbox.LineMaterial,targets,8,true);
        var foundations=new FoundationPlacement(terrain,root.transform,towers,sandbox.FoundationMaterial,sandbox.BarrelMaterial,sandbox.LineMaterial,sandbox.ValidMaterial,new Material[4],new Material[4],sandbox.FoundationPrefab,sandbox.TowerPrefabs,content);
        content.Attach(foundations);foundations.AttachGround();
        var edits=new WorldEditSession(foundations,sandbox.LineMaterial,content){legacyGroundTowers=towers};
        var owned=new List<UnityEngine.Object>();
        BuildingInstance Install(BuildingDefinition d,Vector3 p){Check(edits.AddContent(d,p,Vector3.forward,out string reason),reason);var before=content.GroundWorld.Buildings.ToArray();Check(edits.Confirm().Success,"Commit "+d.Id);content.Bases.Refresh();return content.GroundWorld.Buildings.Single(x=>!before.Contains(x));}
        try {
            var nexusDef=sandbox.ContentCatalog.Buildings.Single(d=>d.Id=="installation.nexus");
            var outpostDef=sandbox.ContentCatalog.Buildings.Single(d=>d.Id=="installation.outpost");
            var towerDef=sandbox.ContentCatalog.Buildings.First(d=>d.Id=="defense.tesla");
            var nexus=Install(nexusDef,new Vector3(2260,0,2260));
            var area=nexus.Module<IBuildArea>();Check(area.Shape==BuildAreaShape.Square,"Square shape");
            var boundary=nexus.Position+WorldGridGeometry.ToWorld(new Vector3(39,0,39));
            Check(area.Contains(boundary,Vector2.one,WorldGridGeometry.Rotation),"Inclusive square corner");
            Check(!area.Contains(boundary+WorldGridGeometry.ToWorld(Vector3.right*.01f),Vector2.one,WorldGridGeometry.Rotation),"Footprint crosses boundary");
            for(int i=0;i<4;i++)Check(area.Contains(area.Boundary(i/4f),Vector2.zero,WorldGridGeometry.Rotation),"Boundary renderer matches query");
            var tower=Install(towerDef,nexus.Position+WorldGridGeometry.ToWorld(new Vector3(64,0,0)));
            Check(!tower.Operational&&tower.OperationBlock==OperationBlock.OutsideArea,"Outside installation remains inactive with owner");
            targets.Register(1,tower.Position+Vector3.forward*4,TargetKind.Ground,receiver);content.Tick(.1f);Check(receiver.Hits==0,"Inactive turret silent");
            string owner=tower.OwnerBaseId;
            Check(edits.AddContent(outpostDef,tower.Position+WorldGridGeometry.ToWorld(Vector3.right*8),Vector3.forward,out var why),why);
            content.Bases.Refresh();Check(!tower.Operational,"Pending provider never operates");edits.Cancel();
            var outpost=Install(outpostDef,tower.Position+WorldGridGeometry.ToWorld(Vector3.right*8));Check(tower.Operational,"Outpost wakes tower");
            content.TickProduction(1);Check(receiver.Hits==0,"Preparation skips combat capability");content.Tick(.1f);Check(receiver.Hits>0,"Operational tower attacks");
            var destinations=new NexusDestinationQuery(content.GroundWorld,true);destinations.Refresh();Check(destinations.TryNearest(outpost.Position,out var destination)&&destination==nexus.Position,"Outpost is never enemy destination");
            Check(edits.ToggleGroundRecovery(outpost,out why),why);content.Bases.Refresh();Check(tower.Operational,"Pending recovery keeps coverage");edits.Cancel();Check(tower.Operational,"Recovery cancel preserves coverage");
            Check(edits.ToggleGroundRecovery(outpost,out why)&&edits.Confirm().Success,"Provider recoverable with dependents");content.Bases.Refresh();Check(!tower.Operational&&!tower.Disposed,"Confirmed provider recovery stops, not removes, tower");
            outpost=Install(outpostDef,tower.Position+WorldGridGeometry.ToWorld(Vector3.right*8));Check(tower.Operational,"Coverage restored");
            var other=Install(nexusDef,nexus.Position+WorldGridGeometry.ToWorld(new Vector3(80,0,40)));
            Check(other.Module<IBaseIdentity>().BaseId!=nexus.Module<IBaseIdentity>().BaseId&&tower.OwnerBaseId==owner,"Independent identities; ownership doesn't migrate");
            Check(edits.ToggleGroundRecovery(nexus,out why)&&edits.Confirm().Success,"Base recovery allowed");content.Bases.Refresh();Check(tower.OperationBlock.HasFlag(OperationBlock.BaseLost)&&!outpost.Operational,"Base loss disables dependent outpost and turret");
            // Production and damage use the same operational contract, without disabling lifecycle.
            content.Bases.Select(other.Module<IBaseIdentity>().BaseId);
            var producer=UnityEngine.Object.Instantiate(towerDef);owned.Add(producer);producer.Id="verification.production";producer.Modules.Clear();
            var health=ScriptableObject.CreateInstance<HealthModuleDefinition>();owned.Add(health);producer.Modules.Add(health);
            var recipe=ScriptableObject.CreateInstance<ProductionModuleDefinition>();owned.Add(recipe);recipe.OutputId="test.ore";recipe.OutputAmount=1;recipe.Interval=1;producer.Modules.Add(recipe);content.Resources.AddCapacity("test.ore",100);
            var production=Install(producer,other.Position+WorldGridGeometry.ToWorld(new Vector3(64,0,0)));
            content.TickProduction(3);Check(content.Resources.Amount("test.ore")==0,"Outside producer paused");
            production.Module<IDamageReceiver>().ApplyDamage(10);Check(production.Module<HealthModule>().Current==90,"Inactive building takes damage");
            var supply=Install(outpostDef,production.Position+WorldGridGeometry.ToWorld(Vector3.right*8));content.TickProduction(1);Check(content.Resources.Amount("test.ore")==1,"Producer resumes with coverage");
            // Existing legacy tower adapter shares operational gating.
            var point=other.Position+WorldGridGeometry.ToWorld(new Vector3(-64,0,0));Check(edits.AddTower(point,Vector3.forward,HordeTowerKind.MachineGun,out why)&&edits.Confirm().Success,"Legacy external placement "+why);content.Bases.Refresh();Check(towers.Count==1&&!towers[0].building.Operational,"Legacy external tower inactive");
            var legacy=towers[0];Check(edits.ToggleGroundRecovery(legacy.building,out why)&&edits.Confirm().Success&&towers.Count==0,"Inactive legacy tower recoverable");
            var invalid=UnityEngine.Object.Instantiate(outpostDef);owned.Add(invalid);invalid.Placement=UnityEngine.Object.Instantiate(outpostDef.Placement);owned.Add(invalid.Placement);invalid.Placement.RequiresOperationalArea=true;Check(invalid.Validate().Count>0,"Circular coverage configuration rejected");
            Check(root.GetComponentsInChildren<NexusGridView>().Length==0,"No per-provider overlapping grids");
            Check(content.GroundWorld.Grid.ReservationCount==0,"No leaked reservations");
            return "PASS: square boundary/corners/render data, external placement, pending/cancel/confirmed coverage, attack + production gating, inactive damage, fixed base ownership/loss, outposts excluded from destinations, legacy tower recovery, dependency cycle validation, no per-base grid or reservation leaks.";
        } finally {edits.Dispose();foundations.Dispose();content.Dispose();foundations.GroundPlatform.Factory.Dispose();foreach(var o in owned)UnityEngine.Object.Destroy(o);UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(ground);UnityEngine.Object.Destroy(data);}
    }
}
