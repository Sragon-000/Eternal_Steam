using System;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Demo;
public static class VerifyNexusDestinations
{
 sealed class Factory:IBuildingFactory{public BuildingInstance Stage(int id,PlacementRequest r,Vector3 p)=>new BuildingInstance(id,r.Definition,r.Cell,p,new BuildingServices(null));public void Activate(BuildingInstance b){}public void Remove(BuildingInstance b){}}
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static string Main(){var data=new TerrainData{heightmapResolution=33,size=new Vector3(128,10,128)};var terrainObject=Terrain.CreateTerrainGameObject(data);var prefab=new GameObject("Test definition");prefab.SetActive(false);var definition=ScriptableObject.CreateInstance<BuildingDefinition>();var area=ScriptableObject.CreateInstance<BuildAreaModuleDefinition>();definition.Id="test.nexus";definition.DisplayName="Nexus";definition.ViewPrefab=prefab;definition.Modules.Add(area);
 var world=new BuildingWorld(new BuildGrid(new RectInt(0,0,128,128),1),new Factory());var session=new PlacementSession(world);var targets=new NexusDestinationQuery(world);var enemies=new HordeEnemyWorld(_=>0,false,32,new Rect(0,0,128,128));var stream=new EnemySpawnStream(enemies,targets,terrainObject.GetComponent<Terrain>(),.75f);
 try{
 Check(!stream.Request(1),"No nexus blocks request");Check(session.Add(definition,new Vector2Int(40,40),out _).Success,"Pending nexus");Check(!stream.Request(1),"Pending nexus cannot be target");Check(session.Confirm().Success,"Confirm first");var first=world.Buildings.Single();Check(stream.Request(100),"Nexus alone permits spawning without foundation");Check(stream.Pump()>0,"Spawned");for(int i=0;i<enemies.MaxCount;i++)if(enemies.GetEnemy(i).alive){var e=enemies.GetEnemy(i);Check((new Vector2(e.destination.x,e.destination.z)-new Vector2(first.Position.x,first.Position.z)).sqrMagnitude<.001f,"Spawn points toward nexus");}
 Check(session.Add(definition,new Vector2Int(80,40),out _).Success&&session.Confirm().Success,"Second nexus");stream.Pump();Check(targets.TryNearest(new Vector3(80,0,40),out var nearest)&&nearest.x>70,"Nearest selection");Check(targets.TryNearest(new Vector3(60.5f,0,40.5f),out nearest)&&nearest==first.Position,"Equal distance uses stable id");
 first.Destroy();stream.Pump();var remaining=world.Buildings.Single();for(int i=0;i<enemies.MaxCount;i++)if(enemies.GetEnemy(i).alive)Check(Mathf.Abs(enemies.GetEnemy(i).destination.x-remaining.Position.x)<.001f,"Retarget existing enemies");
 remaining.Destroy();int pending=stream.Pending;Check(stream.Pump()==0&&stream.Pending==pending,"No nexus preserves queued count");var before=enemies.GetEnemy(0).position;int alive=enemies.Alive;enemies.MoveAndIndex(10);Check(enemies.GetEnemy(0).position==before&&enemies.Alive==alive&&enemies.GetEnemy(0).waitingForDestination,"No target holds enemies without arrival removal");
 Check(session.Add(definition,new Vector2Int(60,70),out _).Success&&session.Confirm().Success,"Reinstall");stream.Pump();Check(!enemies.GetEnemy(0).waitingForDestination,"Existing enemies resume");
 var air=new HordeEnemyWorld(_=>2,false,2);Check(air.TrySpawn(Vector3.zero,Vector3.right*20,2,20,true),"Air spawn");air.SetDestination(0,new Vector3(10,0,10));Check(air.GetEnemy(0).destination.y==7,"Retarget preserves air height");
 var legacy=new HordeEnemyWorld();legacy.Spawn(HordeMapKind.Lane);Check(!legacy.GetEnemy(0).waitingForDestination,"Legacy route enabled by default");return "PASS: missing/pending nexus rejected; confirmed nexus without foundation spawns; nearest and ID tie; existing enemy retarget; no nexus hold and pending preservation; reinstall resume; air height and legacy movement defaults.";
 }finally{session.Dispose();world.Dispose();UnityEngine.Object.DestroyImmediate(prefab);UnityEngine.Object.DestroyImmediate(terrainObject);UnityEngine.Object.DestroyImmediate(data);UnityEngine.Object.DestroyImmediate(definition);UnityEngine.Object.DestroyImmediate(area);}
 }
}
