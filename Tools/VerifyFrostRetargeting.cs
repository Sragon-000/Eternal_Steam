using System;
using System.Collections.Generic;
using UnityEngine;
using EternalSteam.Demo;
public static class VerifyFrostRetargeting
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static string Main(){
        var root=new GameObject("Frost retarget verification");var head=new GameObject("Head");head.transform.SetParent(root.transform);
        LineRenderer Line(string name,int count){var go=new GameObject(name);go.transform.SetParent(root.transform);var line=go.AddComponent<LineRenderer>();line.positionCount=count;return line;}
        var tower=new HordeTower{root=root,head=head.transform,range=20,halfAngle=30,kind=HordeTowerKind.Frost,tracer=Line("Tracer",2),coverage=Line("Coverage",65),impact=Line("Impact",35),showIdleCoverage=false};
        try {
            var world=new HordeEnemyWorld(capacity:8);var combat=new HordeTowerCombat(new List<HordeTower>{tower},world,new HordeAttackResolver(world),true,true);
            world.TrySpawn(new Vector3(0,0,3),Vector3.right*30,0,100);
            world.TrySpawn(new Vector3(0,0,5),Vector3.right*30,0,100);
            world.TrySpawn(new Vector3(0,0,-6),Vector3.right*30,0,100);world.MoveAndIndex(0);
            combat.Update(.01f,true,int.MaxValue);
            Check(world.GetEnemy(0).slowTime>0&&world.GetEnemy(1).slowTime>0&&world.GetEnemy(2).slowTime==0,"First cone slows both front enemies");
            combat.Update(.01f,true,int.MaxValue);
            Check(tower.targetId==2&&tower.head.forward.z<-.9f&&tower.shot==1,"Skip all slowed enemies, aim next without resetting cooldown");
            combat.Update(.2f,true,int.MaxValue);Check(world.GetEnemy(2).slowTime>0&&tower.shot==2,"Next enemy slowed");
            combat.Update(1,true,int.MaxValue);Check(tower.targetId==-1&&tower.shot==2,"All slowed: hold fire");
            world.MoveAndIndex(3);combat.Update(.01f,true,int.MaxValue);Check(tower.targetId==0&&tower.shot==3,"Expired slow becomes eligible again");
            tower.kind=HordeTowerKind.MachineGun;tower.cooldown=0;combat.Update(.01f,true,int.MaxValue);Check(tower.targetId==0&&world.GetEnemy(0).health==90,"Other tower retains slowed target");
            return "PASS frost skips cone-slowed targets, switches during existing cooldown, pauses when all slowed, reacquires after expiry; machine gun targeting unchanged.";
        }finally{UnityEngine.Object.DestroyImmediate(root);}
    }
}
