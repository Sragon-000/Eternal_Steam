using System;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.Demo;
using EternalSteam.OpenWorld;
public static class VerifySandboxEnemyHealth
{
    sealed class Factory:IBuildingFactory {
        public BuildingInstance Stage(int id,PlacementRequest r,Vector3 position)=>new BuildingInstance(id,r.Definition,r.Cell,position,new BuildingServices(null));
        public void Activate(BuildingInstance b){}public void Remove(BuildingInstance b){}
    }
    public static string Main(){
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        if(s.VerificationEnemyHealth!=10)throw new Exception("Scene default must be 10");
        var origin=s.Ground.transform.position+s.Ground.terrainData.size*.5f;origin.y=0;
        using var buildings=new BuildingWorld(new BuildGrid(new RectInt(-10,-10,20,20),2,origin),new Factory());
        using var placement=new PlacementSession(buildings);
        var nexus=s.ContentCatalog.Buildings.Single(d=>d.Id=="installation.nexus");
        if(!placement.Add(nexus,Vector2Int.zero,out _).Success||!placement.Confirm().Success)throw new Exception("Test nexus installation");
        var enemies=new HordeEnemyWorld(null,false,32);
        void Case(int health,int hits){
            enemies.Reset();var stream=new EnemySpawnStream(enemies,new NexusDestinationQuery(buildings,true),s.Ground,.75f,health);
            if(!stream.Request(1,true)||stream.Pump()!=1)throw new Exception("Actual stream spawn");
            int id=Enumerable.Range(0,enemies.MaxCount).Single(i=>enemies.GetEnemy(i).alive);
            if(enemies.GetEnemy(id).health!=health)throw new Exception("Spawn health snapshot");
            for(int i=0;i<hits;i++){enemies.ApplyDamage(id,HordeTowerStats.Damage(HordeTowerKind.MachineGun));if(i<hits-1&&!enemies.GetEnemy(id).alive)throw new Exception("Premature death");}
            if(enemies.Alive!=0||enemies.Killed!=1)throw new Exception("Lethal damage did not remove enemy");
        }
        Case(s.VerificationEnemyHealth,1);Case(20,2);
        return "PASS actual spawn stream: default HP10 dies to one machine-gun hit; configured HP20 needs two hits; death removes active slot and increments kill count.";
    }
}
