using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Demo;
public static class VerifyLegacyRotation
{
    static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
    public static string Main(){var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        foreach(HordeTowerKind kind in Enum.GetValues(typeof(HordeTowerKind))){
            var definition=UnityEngine.Object.Instantiate(s.Foundations.Definition(kind));definition.Modules=definition.Modules.Where(m=>m is not PowerModuleDefinition).ToList();
            using var building=new BuildingInstance(1,definition,default,Vector3.zero,new BuildingServices(null));building.Activate();
            var root=new GameObject("Legacy rotation integration");var head=new GameObject("Head");head.transform.SetParent(root.transform);
            LineRenderer Line(string n,int count){var g=new GameObject(n);g.transform.SetParent(root.transform);var l=g.AddComponent<LineRenderer>();l.positionCount=count;return l;}
            var t=new HordeTower{building=building,root=root,head=head.transform,kind=kind,range=20,halfAngle=30,tracer=Line("Tracer",2),coverage=Line("Coverage",65),impact=Line("Impact",35)};
            try{var turn=building.Module<ITurretRotation>();Check(turn!=null,"Prefab definition has rotation "+kind);var world=new HordeEnemyWorld(capacity:8);world.TrySpawn(Vector3.right*5,Vector3.forward*25,0,10000);world.MoveAndIndex(0);var combat=new HordeTowerCombat(new List<HordeTower>{t},world,new HordeAttackResolver(world),true,true);
                combat.Update(.01f,true,int.MaxValue);Check(t.shot==0&&Vector3.Angle(Vector3.forward,head.transform.forward)<=turn.DegreesPerSecond*.01f+.01f,"Limited turn without early shot "+kind);
                for(int i=0;i<100;i++)combat.Update(.02f,true,int.MaxValue);Check(t.shot>0&&Vector3.Angle(Vector3.right,head.transform.forward)<=1.01f,"Aligned fire "+kind);
                float speed=turn.DegreesPerSecond;Check(building.Module<IUpgradeControl>().TryUpgrade(out _)&&Mathf.Abs(turn.DegreesPerSecond-speed*1.1f)<.02f,"Upgrade changes actual legacy speed "+kind);
            }finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(definition);}
        }return "PASS all four legacy rotation configurations (power tested separately in VerifyPoweredCombat): independent rotation module, capped turn, aligned fire, 10% upgrade integration.";
    }
}
