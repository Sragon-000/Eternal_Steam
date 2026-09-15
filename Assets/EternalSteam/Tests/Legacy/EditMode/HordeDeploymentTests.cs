using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace EternalSteam.Demo.Tests
{
    public sealed class HordeDeploymentTests
    {
        [Test] public void TwoClicksCommitSnappedPositionAndPointerDirection()
        {
            int calls=0; Vector3 placed=default,aimed=default;float distance=0;
            var c=new HordePlacementController(_=>true,(p,d,r)=>{ calls++;placed=p;aimed=d;distance=r;return true; });
            c.Tick(true,false,HordeMapKind.Lane,new HordePlacementInput(true,new Vector3(3.2f,0,-8.1f),confirmPressed:true));
            Assert.That(c.ChoosingDirection,Is.True);Assert.That(calls,Is.Zero);Assert.That(c.LockedPosition,Is.EqualTo(new Vector3(4,0,-8)));
            c.Tick(true,false,HordeMapKind.Lane,new HordePlacementInput(true,new Vector3(14,0,-8),confirmPressed:true));
            Assert.That(calls,Is.EqualTo(1));Assert.That(placed,Is.EqualTo(new Vector3(4,0,-8)));Assert.That(aimed,Is.EqualTo(Vector3.right));Assert.That(distance,Is.EqualTo(10));Assert.That(c.ChoosingDirection,Is.False);
        }
        [Test] public void FailedPlacementKeepsEditAndCancellationNeedsNoPointer()
        {
            var c=new HordePlacementController(_=>true,(p,d,r)=>false);
            Assert.That(c.Begin(Vector3.zero,HordeMapKind.Lane),Is.True);
            Assert.That(c.Confirm(Vector3.forward,10),Is.False);Assert.That(c.ChoosingDirection,Is.True);
            c.Tick(false,true,HordeMapKind.Lane,new HordePlacementInput(false,Vector3.zero,cancelPressed:true));
            Assert.That(c.ChoosingDirection,Is.False);Assert.That(c.Visible,Is.False);
        }
        [TestCase(false,false)] [TestCase(true,true)] public void CombatAndUiBlockingCannotCommit(bool canBuild,bool blocked)
        {
            int calls=0;var c=new HordePlacementController(_=>true,(p,d,r)=>{calls++;return true;});c.Begin(Vector3.zero,HordeMapKind.Lane);
            c.Tick(canBuild,blocked,HordeMapKind.Lane,new HordePlacementInput(true,Vector3.forward*10,confirmPressed:true));
            Assert.That(calls,Is.Zero);Assert.That(c.Visible,Is.False);
        }
        [Test] public void AimingBeyondMapClampsRangeWhileIdleOutsideMapIsHidden()
        {
            var c=new HordePlacementController(_=>true,(p,d,r)=>true);
            c.Tick(true,false,HordeMapKind.Lane,new HordePlacementInput(true,Vector3.right*100));Assert.That(c.Visible,Is.False);
            c.Begin(Vector3.zero,HordeMapKind.Lane);c.Tick(true,false,HordeMapKind.Lane,new HordePlacementInput(true,Vector3.right*100));
            Assert.That(c.Visible,Is.True);Assert.That(c.Range,Is.EqualTo(26));
            c.Tick(true,false,HordeMapKind.Lane,new HordePlacementInput(true,Vector3.zero));Assert.That(c.Range,Is.EqualTo(6));Assert.That(c.Direction,Is.EqualTo(Vector3.right));
        }
        [Test] public void InvalidGridCannotBeginAndConfirmationRevalidatesThroughCommand()
        {
            bool allowed=false;int calls=0;var c=new HordePlacementController(_=>allowed,(p,d,r)=>{calls++;return allowed;});
            Assert.That(c.Begin(Vector3.zero,HordeMapKind.Lane),Is.False);allowed=true;c.Begin(Vector3.zero,HordeMapKind.Lane);allowed=false;
            Assert.That(c.Confirm(Vector3.forward,22),Is.False);Assert.That(calls,Is.EqualTo(1));Assert.That(c.ChoosingDirection,Is.True);
        }
        [Test] public void CameraFramingRejectsNonfiniteViewportAndFitsBothMapSizes()
        {
            var go=new GameObject("Camera fixture");
            try
            {
                var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.transform.rotation=Quaternion.Euler(65,0,0);
                var framing=new HordeCameraFraming();framing.SetViewport(camera,HordeMapKind.Lane,0.3f);
                Assert.That(camera.rect.x,Is.EqualTo(0.3f));float size=camera.orthographicSize;
                framing.SetViewport(camera,HordeMapKind.Lane,float.NaN);Assert.That(camera.rect.x,Is.EqualTo(0.3f));
                framing.Fit(camera,HordeMapKind.WideFront);Assert.That(camera.orthographicSize,Is.GreaterThan(size));
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void DeploymentHasNoSimulationOrInputSystemDependency()
        {
            var refs=typeof(HordePlacementController).Assembly.GetReferencedAssemblies().Select(x=>x.Name).ToArray();
            Assert.That(refs,Does.Not.Contain("EternalSteam.LegacyDemo"));Assert.That(refs,Does.Not.Contain("Unity.InputSystem"));
            Assert.That(refs.Where(x=>x.StartsWith("EternalSteam.")),Is.EquivalentTo(new[]{"EternalSteam.LegacyDefinitions"}));
            var rendering=typeof(HordePlacementView).Assembly.GetReferencedAssemblies().Select(x=>x.Name).ToArray();Assert.That(rendering,Does.Not.Contain("EternalSteam.LegacyDemo"));
        }
    }
}
