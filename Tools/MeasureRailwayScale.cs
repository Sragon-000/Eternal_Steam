using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.Railway;
public static class MeasureRailwayScale
{
 sealed class Inventory:IRailInventory{public bool Available(string id)=>true;public double Load(string id,string resource,double maximum)=>resource=="coal"?maximum:0;public double Unload(string id,string resource,double maximum)=>maximum;}
 [Serializable] public sealed class Result{public int routes,stations,tracks,updates;public double topologyMs,commitMs,tickMs;}
 [Serializable] public sealed class Report{public string scope="Unity Editor, core model only; 1000 updates of 0.1s, no threshold/FPS claim";public List<Result> samples=new();}
 public static string Main(){
  if(Application.isPlaying)throw new Exception("Edit mode required");var report=new Report();
  foreach(var config in new[]{(1,2,6),(10,2,6),(100,2,6),(10,4,6),(10,4,30),(10,4,100)}){
   var buildings=new List<BuildingInstance>();var definitions=new List<UnityEngine.Object>();var station=ScriptableObject.CreateInstance<BuildingDefinition>();var track=ScriptableObject.CreateInstance<BuildingDefinition>();var stationModule=ScriptableObject.CreateInstance<RailFacilityDefinition>();stationModule.Kind=RailFacilityKind.Station;var trackModule=ScriptableObject.CreateInstance<RailFacilityDefinition>();trackModule.Kind=RailFacilityKind.Track;station.Id="station";station.Footprint=new Vector2Int(2,2);station.Modules.Add(stationModule);track.Id="track";track.Modules.Add(trackModule);definitions.AddRange(new UnityEngine.Object[]{station,track,stationModule,trackModule});
   try{
    string owner=Guid.NewGuid().ToString("N");var drafts=new List<List<RailStop>>();var watch=Stopwatch.StartNew();
    BuildingInstance Place(BuildingDefinition def,Vector2Int cell){var b=new BuildingInstance(buildings.Count+1,def,cell,new Vector3(cell.x,0,cell.y),new BuildingServices(null));b.RestoreIdentity(b.PersistentId,owner);b.ConfirmDirection(Quaternion.Euler(0,45,0)*Vector3.forward);b.Activate();buildings.Add(b);return b;}
    for(int route=0;route<config.Item1;route++){
     int z=route*12,gap=config.Item3,count=config.Item2;var draft=new List<RailStop>();for(int i=0;i<count;i++)draft.Add(new RailStop{stationId=Place(station,new Vector2Int(i*gap,z)).PersistentId});drafts.Add(draft);
     void Track(int x,int y)=>Place(track,new Vector2Int(x,z+y));
     for(int i=0;i<count-1;i++){for(int x=i*gap+2;x<=(i+1)*gap-1;x++)Track(x,1);Track((i+1)*gap-1,0);}int end=(count-1)*gap+2;for(int y=1;y<=4;y++)Track(end,y);for(int x=end-1;x>=-1;x--)Track(x,4);for(int y=3;y>=0;y--)Track(-1,y);
    }
    var network=new RailwayNetwork(new Inventory(),"coal",1);network.Refresh(buildings);watch.Stop();var result=new Result{routes=config.Item1,stations=config.Item1*config.Item2,tracks=buildings.Count-config.Item1*config.Item2,updates=1000,topologyMs=watch.Elapsed.TotalMilliseconds};watch.Restart();
    foreach(var draft in drafts){if(!network.Commit(Guid.NewGuid().ToString("N"),draft,out var route,out var error))throw new Exception(error);network.Configure(route,"iron",0,0,0,out _);network.Refuel(route,route.stops.Count*50,out _);if(!network.Start(route,out error))throw new Exception(error);}
    watch.Stop();result.commitMs=watch.Elapsed.TotalMilliseconds;watch.Restart();for(int i=0;i<1000;i++)network.Tick(.1);watch.Stop();result.tickMs=watch.Elapsed.TotalMilliseconds;report.samples.Add(result);
   }finally{foreach(var b in buildings)b.Dispose();foreach(var d in definitions)UnityEngine.Object.DestroyImmediate(d);}
  }
  string Number(double n)=>n.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
  string json="{\"scope\":\"Unity Editor core only, 1000 updates at 0.1s; no FPS guarantee\",\"samples\":["+string.Join(",",report.samples.Select(r=>"{\"routes\":"+r.routes+",\"stations\":"+r.stations+",\"tracks\":"+r.tracks+",\"updates\":"+r.updates+",\"topologyMs\":"+Number(r.topologyMs)+",\"commitMs\":"+Number(r.commitMs)+",\"tickMs\":"+Number(r.tickMs)+"}"))+"]}";System.IO.File.WriteAllText("/Users/limseth/Projects/Eternal_Steam/Eternal_Steam/Docs/Validation/2026-09-28-railway-scale.json",json);return json;
 }
}
