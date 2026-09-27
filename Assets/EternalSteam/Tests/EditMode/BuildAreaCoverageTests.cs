using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using EternalSteam.OpenWorld;
using Object=UnityEngine.Object;
namespace EternalSteam.Tests
{
    public sealed class BuildAreaCoverageTests
    {
        readonly List<Object> assets=new();readonly List<BuildingInstance> buildings=new();
        T Asset<T>() where T:ScriptableObject {var a=ScriptableObject.CreateInstance<T>();assets.Add(a);return a;}
        BuildingInstance Provider(BaseRegistry registry,Vector3 local,BaseRole? role=BaseRole.Main,bool register=true,BuildAreaShape shape=BuildAreaShape.Square)
        {
            var d=Asset<BuildingDefinition>();d.Id="coverage.fixture";d.Footprint=new Vector2Int(4,4);
            if(role.HasValue){var identity=Asset<BaseModuleDefinition>();identity.Role=role.Value;d.Modules.Add(identity);}
            var area=Asset<BuildAreaModuleDefinition>();area.Radius=11;area.Shape=shape;area.Yaw=45;area.FromFrontEdge=true;area.AlignForwardCells=true;area.RadiusPerLevel=2;d.Modules.Add(area);
            var upgrade=Asset<UpgradeModuleDefinition>();upgrade.RequireNexus=false;d.Modules.Add(upgrade);
            var b=new BuildingInstance(buildings.Count+1,d,Vector2Int.zero,WorldGridGeometry.ToWorld(local),new BuildingServices(null));buildings.Add(b);b.Activate();if(register)registry.Register(b);return b;
        }
        static int Cells(BaseRegistry registry)
        {
            registry.RefreshCoverage();int count=0;
            for(int z=-32;z<64;z++)for(int x=-32;x<64;x++)if(registry.Covers(WorldGridGeometry.Center(new Vector2Int(x,z),2),Vector2.one,WorldGridGeometry.Rotation,registry.SelectedBaseId))count++;
            return count;
        }
        [TearDown]public void Cleanup(){foreach(var b in buildings)b.Dispose();buildings.Clear();foreach(var a in assets)Object.DestroyImmediate(a);assets.Clear();}
        [Test]public void ForwardSquareCoversExactly121WholeCellsAndNoRearCells()
        {
            var r=new BaseRegistry(2,WorldGridGeometry.Rotation){AnyNormalBaseCoverage=true};Provider(r,new Vector3(4,0,4));Assert.That(Cells(r),Is.EqualTo(121));
            for(int z=-5;z<4;z++)for(int x=-10;x<10;x++)Assert.That(r.Covers(WorldGridGeometry.Center(new Vector2Int(x,z),2),Vector2.one,WorldGridGeometry.Rotation,r.SelectedBaseId),Is.False);
            Assert.That(r.Covers(WorldGridGeometry.Center(new Vector2Int(-3,4),2),Vector2.one,WorldGridGeometry.Rotation,r.SelectedBaseId),Is.True);
            Assert.That(r.Covers(WorldGridGeometry.Center(new Vector2Int(-3,4),2),Vector2.one*2,WorldGridGeometry.Rotation,r.SelectedBaseId),Is.False,"Whole footprint, not center-only inclusion");
        }
        [Test]public void UnionDoesNotDuplicateOverlappingProvidersAndPendingProviderDoesNotExpand()
        {
            var r=new BaseRegistry(2,WorldGridGeometry.Rotation){AnyNormalBaseCoverage=true};var first=Provider(r,new Vector3(4,0,4));
            Provider(r,new Vector3(4,0,4),BaseRole.Sub);var pending=Provider(r,new Vector3(64,0,4),BaseRole.Sub,false);
            Assert.That(Cells(r),Is.EqualTo(121));r.Register(pending);Assert.That(Cells(r),Is.EqualTo(242));
            // Recovery reservation deliberately leaves committed provider registered until commit.
            Assert.That(Cells(r),Is.EqualTo(242));r.Remove(pending);Assert.That(Cells(r),Is.EqualTo(121));first.Dispose();Assert.That(Cells(r),Is.EqualTo(121));
        }
        [Test]public void GrowthDestructionAndOwnerSelectionInvalidateButStationaryQueriesDoNot()
        {
            var r=new BaseRegistry(2,WorldGridGeometry.Rotation){AnyNormalBaseCoverage=true};var b=Provider(r,new Vector3(4,0,4));Assert.That(Cells(r),Is.EqualTo(121));int revision=r.CoverageRevision;
            for(int i=0;i<100;i++)r.RefreshCoverage();Assert.That(r.CoverageRevision,Is.EqualTo(revision));
            Assert.That(b.Module<IUpgradeControl>().TryUpgrade(out _),Is.True);Assert.That(Cells(r),Is.EqualTo(169));Assert.That(r.CoverageRevision,Is.GreaterThan(revision));
            revision=r.CoverageRevision;b.Dispose();Assert.That(Cells(r),Is.Zero);Assert.That(r.CoverageRevision,Is.GreaterThan(revision));
        }
        [Test]public void LegacyOutpostSharesOwnersCoverageButNormalModeFiltersIt()
        {
            var r=new BaseRegistry(2,WorldGridGeometry.Rotation);var owner=Provider(r,new Vector3(4,0,4),BaseRole.Legacy);
            var outpost=Provider(r,new Vector3(44,0,4),null);Assert.That(Cells(r),Is.EqualTo(242));
            int revision=r.CoverageRevision;Assert.That(outpost.Module<IUpgradeControl>().TryUpgrade(out _),Is.True);Assert.That(Cells(r),Is.EqualTo(290));Assert.That(r.CoverageRevision,Is.GreaterThan(revision));
            r.AnyNormalBaseCoverage=true;Assert.That(Cells(r),Is.Zero);r.AnyNormalBaseCoverage=false;owner.Dispose();Assert.That(Cells(r),Is.Zero);
        }
        [Test]public void SelectedLegacyOwnerChangesDisplayUnionWithoutReassigningBuildings()
        {
            var r=new BaseRegistry(2,WorldGridGeometry.Rotation);var first=Provider(r,new Vector3(4,0,4),BaseRole.Legacy);var second=Provider(r,new Vector3(64,0,4),BaseRole.Legacy);
            r.RefreshCoverage();int revision=r.CoverageRevision;var point=WorldGridGeometry.Center(new Vector2Int(0,5),2);
            Assert.That(r.Covers(point,Vector2.one,WorldGridGeometry.Rotation,r.SelectedBaseId),Is.False);
            Assert.That(r.Select(first.OwnerBaseId),Is.True);r.RefreshCoverage();Assert.That(r.CoverageRevision,Is.GreaterThan(revision));
            Assert.That(r.Covers(point,Vector2.one,WorldGridGeometry.Rotation,r.SelectedBaseId),Is.True);
            Assert.That(second.OwnerBaseId,Is.Not.EqualTo(first.OwnerBaseId));
        }
        [Test]public void CircularProviderUsesContainsForEveryWholeCell()
        {
            var r=new BaseRegistry(2,WorldGridGeometry.Rotation){AnyNormalBaseCoverage=true};var b=Provider(r,new Vector3(4,0,4),BaseRole.Main,true,BuildAreaShape.Circle);var area=b.Module<IBuildArea>();
            r.RefreshCoverage();for(int z=-12;z<32;z++)for(int x=-12;x<32;x++) {
                var point=WorldGridGeometry.Center(new Vector2Int(x,z),2);
                Assert.That(r.Covers(point,Vector2.one,WorldGridGeometry.Rotation,r.SelectedBaseId),Is.EqualTo(area.Contains(point,Vector2.one,WorldGridGeometry.Rotation)));
            }
        }
    }
}
