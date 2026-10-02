using System;
using NUnit.Framework;
using UnityEngine;
using EternalSteam.OpenWorld;

namespace EternalSteam.Tests
{
    public sealed class TerrainNavigationTests
    {
        static byte[] Cells(int width,int height){var cells=new byte[width*height];Array.Fill(cells,(byte)1);return cells;}
        static TerrainFlowNavigation Navigation(int width,int height,byte[] cells,int cache=16)
            =>new(new TileWorldGround.TraversalSnapshot(Matrix4x4.identity,width,height,1,cells),cache);
        [Test] public void DetoursThroughGapWithoutCrossingTerrainOrExceedingSpeed()
        {
            var cells=Cells(9,9);for(int z=0;z<8;z++)cells[z*9+4]=0;
            var nav=Navigation(9,9,cells);var from=new Vector3(1,0,1);var goal=new Vector3(7,0,1);
            Assert.That(nav.Grid.HasClearRoute(from,goal),Is.False);Assert.That(nav.HasRoute(from,goal),Is.True);
            bool usedGap=false;
            for(int i=0;i<400&&(from-goal).sqrMagnitude>.01f;i++){
                Assert.That(nav.TryStep(from,goal,.15f,out var step),Is.True);
                Assert.That(nav.Grid.HasClearRoute(from,step),Is.True);Assert.That(Vector3.Distance(from,step),Is.LessThanOrEqualTo(.15001f));
                usedGap|=step.z>7.5f;from=step;
            }
            Assert.That(Vector3.Distance(from,goal),Is.LessThan(.11f));Assert.That(usedGap,Is.True);Assert.That(nav.FieldsBuilt,Is.EqualTo(1));
        }
        [Test] public void DisconnectedAndDiagonalOnlyCellsAreNotReachable()
        {
            var nav=Navigation(2,2,new byte[]{1,0,0,1});
            Assert.That(nav.HasRoute(Vector3.zero,new Vector3(1,0,1)),Is.False);
            Assert.That(nav.TryStep(Vector3.zero,new Vector3(1,0,1),1,out var next),Is.False);Assert.That(next,Is.EqualTo(Vector3.zero));
            Assert.That(nav.HasRoute(Vector3.left,Vector3.zero),Is.False);Assert.That(nav.HasRoute(Vector3.zero,Vector3.right),Is.False);
        }
        [Test] public void ShortDiagonalStepCannotSlipBetweenBlockedCornerCells()
        {
            var from=new Vector3(.49f,0,.49f);var to=new Vector3(.51f,0,.51f);
            Assert.That(Navigation(2,2,new byte[]{1,0,0,1}).Grid.HasClearRoute(from,to),Is.False);
            var clear=Navigation(2,2,Cells(2,2));
            Assert.That(clear.Grid.HasClearRoute(from,to),Is.True);Assert.That(clear.Grid.HasClearRoute(to,from),Is.True);
        }
        [Test] public void ManyActorsReuseOneFieldAndNewGoalsRespectStepBudget()
        {
            var nav=Navigation(20,20,Cells(20,20),2);var goal=new Vector3(19,0,19);nav.BeginStep(1);
            for(int i=0;i<1000;i++)Assert.That(nav.TryStep(new Vector3(i%10,0,0),goal,.1f,out _),Is.True);
            Assert.That(nav.FieldsBuilt,Is.EqualTo(1));
            Assert.That(nav.TryStep(Vector3.zero,new Vector3(15,0,15),.1f,out _),Is.False);
            nav.BeginStep(1);Assert.That(nav.TryStep(Vector3.zero,new Vector3(15,0,15),.1f,out _),Is.True);
            nav.BeginStep(1);Assert.That(nav.TryStep(Vector3.zero,new Vector3(12,0,12),.1f,out _),Is.True);
            Assert.That(nav.CachedFields,Is.EqualTo(2));
        }
        [Test] public void RotatedGridAndReplacementMaskInvalidateDerivedRoutes()
        {
            var transform=Matrix4x4.TRS(new Vector3(100,0,-30),Quaternion.Euler(0,45,0),Vector3.one);
            var cells=Cells(4,4);var nav=new TerrainFlowNavigation(new TileWorldGround.TraversalSnapshot(transform.inverse,4,4,2,cells));
            var from=transform.MultiplyPoint3x4(Vector3.zero);var goal=transform.MultiplyPoint3x4(new Vector3(6,0,6));
            Assert.That(nav.TryStep(from,goal,.2f,out var next),Is.True);Assert.That(nav.Grid.HasClearRoute(from,next),Is.True);
            var replacement=Cells(4,4);for(int z=0;z<4;z++)replacement[z*4+1]=0;
            nav.Update(new TileWorldGround.TraversalSnapshot(transform.inverse,4,4,2,replacement));
            Assert.That(nav.CachedFields,Is.Zero);Assert.That(nav.HasRoute(from,goal),Is.False);
        }
    }
}
