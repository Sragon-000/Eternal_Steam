using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class MeasureOpenWorldFrames
{
 const int Warmup=30,Samples=120;
 static OpenWorldSandbox sandbox;static BuildingInstance nexus;static ProfilerRecorder[] recorders;
 static readonly string[] names={"Main Thread","PlayerLoop","GC Allocated In Frame"};
 static readonly long[,] values=new long[3,Samples];
 static int frame,lastFrame,oldVsync,oldRate;static float oldCapture;static bool running;static double started;
 static Vector3 oldFocus;static float oldZoom;
 public static string Main(){
  if(running)throw new Exception("Already measuring");
  sandbox=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
  if(!Application.isPlaying||sandbox==null||sandbox.Enemies.Alive!=0||sandbox.Content.GroundWorld.Buildings.Count!=0||sandbox.Foundations.Platforms.Count!=0)throw new Exception("Empty StartRegion Play session required");
  oldVsync=QualitySettings.vSyncCount;oldRate=Application.targetFrameRate;oldCapture=Time.captureDeltaTime;oldFocus=sandbox.CameraRig.Focus;oldZoom=sandbox.CameraRig.Zoom;running=true;
  try{
   var input=sandbox.GetComponent<OpenWorldInput>();input.Cancel();var d=sandbox.ContentCatalog.Buildings.Single(x=>x.Id=="installation.nexus");
   var origin=sandbox.Ground.transform.position;var size=sandbox.Ground.terrainData.size;bool placed=false;
   for(float z=origin.z+16;z<origin.z+size.z-16&&!placed;z+=4)for(float x=origin.x+16;x<origin.x+size.x-16&&!placed;x+=4){
    var p=new Vector3(x,0,z);if(!sandbox.Content.Resolve(d,p,out var session,out var world,out var cell,out _)||!session.Validate(new PlacementRequest(-1,d,cell)).Success)continue;
    input.BeginEditing();input.SelectContent(d);if(input.ClickWorld(p)&&input.Confirm())placed=true;else input.Cancel();
   }
   if(!placed)throw new Exception("No nexus site");nexus=sandbox.Content.GroundWorld.Buildings.Single();nexus.Module<IHealthScaling>().SetMaximumMultiplier(1000000);
   sandbox.SpawnAir=false;if(!sandbox.Spawn("4000"))throw new Exception(sandbox.Message);
   for(int i=0;i<64&&sandbox.SpawnStream.Pending>0;i++)sandbox.SpawnStream.Pump();
   if(sandbox.Enemies.Alive!=4000)throw new Exception("Only "+sandbox.Enemies.Alive+" valid spaced spawn sites");
   sandbox.CameraRig.Focus=nexus.Position;sandbox.CameraRig.Zoom=70;
   QualitySettings.vSyncCount=0;Application.targetFrameRate=-1;Time.captureDeltaTime=1f/60;
   var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);recorders=new ProfilerRecorder[3];
   for(int i=0;i<3;i++){foreach(var h in handles){var description=ProfilerRecorderHandle.GetDescription(h);if(description.Name==names[i]){recorders[i]=ProfilerRecorder.StartNew(description.Category,description.Name,1);break;}}if(!recorders[i].Valid)throw new Exception("Missing counter "+names[i]);}
   frame=0;lastFrame=Time.frameCount;started=EditorApplication.timeSinceStartup;EditorApplication.update+=Observe;File.WriteAllText("Temp/openworld-frames-status.txt","running");
   return "Measuring 4000 actually spawned ground enemies, real TWC terrain, existing camera/render/UI: 30 warmup + 120 frames. Poll Temp/openworld-frames-status.txt; cleanup automatic.";
  }catch{Cleanup();throw;}
 }
 static void Observe(){try{
  if(!Application.isPlaying||EditorApplication.timeSinceStartup-started>120)throw new Exception("Stopped or timed out");
  int current=Time.frameCount;if(current==lastFrame)return;if(current!=lastFrame+1)throw new Exception("Missed frames");lastFrame=current;
  if(frame>=Warmup)for(int i=0;i<3;i++)values[i,frame-Warmup]=recorders[i].LastValue;
  if(++frame<Warmup+Samples)return;
  if(sandbox.Enemies.Alive!=4000)throw new Exception("Population changed");
  var report="StartRegionSandbox actual Editor frames, 4000 ground enemies spawned with normal spacing/terrain rules; one high-health nexus, no towers; camera zoom 70; 30 warmup + 120 frames; includes rendering and UI, no build. Full-frame GC includes Editor/UI/harness; not equivalent to combat-only GC.\n";
  for(int i=0;i<3;i++){var data=new long[Samples];for(int n=0;n<Samples;n++)data[n]=values[i,n];Array.Sort(data);double scale=i==2?1:1e-6;report+=names[i]+": median "+(data[60]*scale).ToString("F3")+", p95 "+(data[114]*scale).ToString("F3")+(i==2?" bytes":" ms")+"\n";}
  File.WriteAllText("Temp/openworld-frames.txt",report);Cleanup();File.WriteAllText("Temp/openworld-frames-status.txt","completed");
 }catch(Exception e){Cleanup();File.WriteAllText("Temp/openworld-frames-status.txt","failed: "+e);}}
 static void Cleanup(){EditorApplication.update-=Observe;if(recorders!=null)foreach(var r in recorders)r.Dispose();recorders=null;
  if(sandbox!=null){sandbox.ResetEnemies();sandbox.GetComponent<OpenWorldInput>().Cancel();if(nexus!=null)sandbox.Content.GroundWorld.Remove(nexus.Id);sandbox.CameraRig.Focus=oldFocus;sandbox.CameraRig.Zoom=oldZoom;}
  nexus=null;QualitySettings.vSyncCount=oldVsync;Application.targetFrameRate=oldRate;Time.captureDeltaTime=oldCapture;running=false;
 }
}
