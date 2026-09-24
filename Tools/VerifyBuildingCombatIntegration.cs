using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Demo;
public static class VerifyBuildingCombatIntegration
{
    static void Check(bool value,string reason){if(!value)throw new Exception(reason);}
    public static string Main()
    {
        Check(Application.isPlaying,"Play required");
        var sandbox=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        Check(sandbox.MeetingConstructionRules,"Start region scene required");
        var root=new GameObject("Meeting isolated verification");
        var data=new TerrainData{heightmapResolution=33,size=new Vector3(600,10,600)};
        var ground=Terrain.CreateTerrainGameObject(data);ground.transform.position=new Vector3(2000,0,2000);
        var terrain=ground.GetComponent<Terrain>();var enemies=new HordeEnemyWorld(null,false,16){ArrivalPolicy=EnemyArrivalPolicy.Remain};var targets=new HordeTargetAdapter(enemies);
        var towers=new List<HordeTower>();
        var content=new OpenWorldContent(terrain,root.transform,sandbox.LineMaterial,targets,8,true);
        var foundations=new FoundationPlacement(terrain,root.transform,towers,sandbox.FoundationMaterial,sandbox.BarrelMaterial,sandbox.LineMaterial,sandbox.ValidMaterial,new Material[4],new Material[4],sandbox.FoundationPrefab,sandbox.TowerPrefabs,content);
        content.Attach(foundations);foundations.AttachGround();
        var edits=new WorldEditSession(foundations,sandbox.LineMaterial,content){legacyGroundTowers=towers};
        BuildingInstance Install(BuildingDefinition d,Vector3 p){Check(edits.AddContent(d,p,Vector3.forward,out string reason),reason);var before=content.GroundWorld.Buildings.ToArray();Check(edits.Confirm().Success,"Commit "+d.Id);content.Bases.Refresh();return content.GroundWorld.Buildings.Single(x=>!before.Contains(x));}
        try {
            var nexusDef=sandbox.ContentCatalog.Buildings.Single(d=>d.Id=="installation.nexus");
            var wallDef=sandbox.ContentCatalog.Buildings.Single(d=>d.Id=="installation.wall");
            var towerDef=sandbox.ContentCatalog.Buildings.First(d=>d.Id=="defense.tesla");
            var nexus=Install(nexusDef,new Vector3(2260,0,2260));
            int registered=content.BuildingTargets.Count;
            var outside=nexus.Position+WorldGridGeometry.ToWorld(new Vector3(64,0,0));
            Check(edits.AddContent(wallDef,outside,Vector3.forward,out var reason),reason);
            Check(content.BuildingTargets.Count==registered,"Pending wall excluded from target registry");edits.Cancel();
            var wall=Install(wallDef,outside);Check(content.BuildingTargets.Count==registered+1&&wall.Module<HealthModule>().Maximum==100,"Outside wall registered with HP100");
            Check(edits.ToggleGroundRecovery(wall,out reason),reason);Check(content.BuildingTargets.FirstBlocker(wall.Position,wall.Position,out _,out _),"Pending recovery still blocks");edits.Cancel();Check(!wall.Disposed,"Recovery cancel retains building");
            Check(edits.ToggleGroundRecovery(wall,out reason)&&edits.Confirm().Success,"Recovery confirm");Check(wall.Disposed&&!content.BuildingTargets.FirstBlocker(wall.Position,wall.Position,out _,out _),"Recovery unregisters blocking body");
            var tower=Install(towerDef,outside);Check(!tower.Operational,"Outside defense inactive");tower.Module<IDamageReceiver>().ApplyDamage(1);Check(tower.Module<HealthModule>().Current==99,"Inactive defense still damageable");
            var hitView=content.Views[tower].GetComponent<BuildingHitView>();Check(hitView!=null&&content.Views[tower].GetComponentsInChildren<LineRenderer>().Any(x=>x.name=="Damage flash"&&x.enabled),"Damage flash attached and triggered");
            var foundationPoint=nexus.Position+WorldGridGeometry.ToWorld(new Vector3(16,0,0));Check(edits.AddFoundation(foundationPoint,out reason)&&edits.Confirm().Success,"Foundation install "+reason);
            var platform=foundations.Platforms.Single();var point=platform.World.Grid.Center(Vector2Int.zero,Vector2Int.one);
            Check(edits.AddContent(wallDef,point,Vector3.forward,out reason)&&edits.Confirm().Success,"Wall on foundation "+reason);
            var onTop=platform.World.Buildings.Single();registered=content.BuildingTargets.Count;onTop.Module<IDamageReceiver>().ApplyDamage(100);Check(onTop.Disposed&&platform.World.Buildings.Count==0&&foundations.Platforms.Count==1&&content.BuildingTargets.Count==registered-1,"Destroy upper wall preserves floor and unregisters target");
            Check(edits.AddContent(wallDef,point,Vector3.forward,out reason)&&edits.Confirm().Success,"Destroyed footprint can be reused "+reason);
            // Active enemies share the common health lifecycle; no nexus means queued spawns remain queued.
            var stream=new EnemySpawnStream(enemies,new NexusDestinationQuery(content.GroundWorld,true),terrain,.75f,10,false,content.BuildingTargets);Check(stream.Request(100),"Queue request");
            var other=Install(nexusDef,nexus.Position+WorldGridGeometry.ToWorld(new Vector3(-80,0,0)));
            nexus.Module<IDamageReceiver>().ApplyDamage(1000);content.Bases.Refresh();Check(tower.OperationBlock.HasFlag(OperationBlock.BaseLost)&&other.Operational,"Only destroyed base loses its territory");
            other.Module<IDamageReceiver>().ApplyDamage(1000);content.Bases.Refresh();Check(stream.Pump()==0&&stream.Pending==100&&!stream.HasDestination,"All nexuses gone: pending spawn paused");
            Check(sandbox.EnemyCombat!=null&&sandbox.EnemyCombat.Valid,"Scene has verification combat settings");
            return "PASS actual factory integration: pending exclusion, outside wall, recovery cancel/confirm, inactive health/flash, foundation survival/occupancy release, per-base nexus loss, pending spawn hold, scene settings.";
        } finally {edits.Dispose();foundations.Dispose();content.Dispose();foundations.GroundPlatform?.Factory?.Dispose();targets.Dispose();UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(ground);UnityEngine.Object.Destroy(data);}
    }
}
