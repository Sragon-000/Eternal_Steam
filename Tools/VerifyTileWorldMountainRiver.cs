using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using GiantGrey.TileWorldCreator;
using EternalSteam.OpenWorld;

public static class VerifyTileWorldMountainRiver
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static string EditMode()
    {
        Check(!Application.isPlaying,"Edit mode required");
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        var m=UnityEngine.Object.FindFirstObjectByType<TileWorldCreatorManager>();
        var b=s.Ground.GetComponent<TileWorldGround>();
        Check(m!=null&&b!=null,"TWC and adapter connected");
        Check(m.configuration.width==96&&m.configuration.height==96&&m.configuration.cellSize==2,"96x96 / 2m configuration");
        Check(Mathf.Abs(Mathf.DeltaAngle(m.transform.eulerAngles.y,45))<.01f,"45 degree grid");
        Check(!s.Ground.drawHeightmap&&!s.Ground.GetComponent<TerrainCollider>().enabled,"Legacy terrain does not render or collide");
        var meshes=m.GetComponentsInChildren<MeshFilter>();Check(meshes.Length>100,"Merged terrain clusters present");
        Check(meshes.All(f=>f.sharedMesh!=null&&AssetDatabase.Contains(f.sharedMesh)),"All meshes persist as assets");
        Check(m.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(a=>a!=null&&a.shader.isSupported)),"Supported materials");
        Physics.SyncTransforms();
        int safe=0,blocked=0;float highest=-100,maxCacheError=0;
        for(int z=0;z<96;z+=2)for(int x=0;x<96;x+=2)
        {
            var p=m.transform.TransformPoint(new Vector3(x*2,0,z*2));
            if(b.TrySurface(p,out float h))highest=Mathf.Max(highest,h);
            if(b.IsPlayable(p))
            {
                safe++;Check(b.TrySurface(p,out h),"Playable ground has collider");
                float cached=s.Ground.SampleHeight(p)+s.Ground.transform.position.y;
                maxCacheError=Mathf.Max(maxCacheError,Mathf.Abs(cached-h));
            }
            else blocked++;
        }
        Check(safe>200&&blocked>200,"Playable and excluded terrain present");
        Check(highest>=19,"Mountain peak at least 19m");Check(maxCacheError<.05f,"Height cache agrees with playable flat ground");
        foreach(var cell in m.GetBlueprintLayer("River").allPositions.Take(50))
        {
            var p=m.transform.TransformPoint(new Vector3(cell.x*2,0,cell.y*2));
            Check(!b.IsPlayable(p),"River excluded");Check(!b.CheckFoundation(p,out _,out _),"River foundation rejected");
        }
        var mountain=m.GetBlueprintLayer("Mountain terrace 8").allPositions.First();
        Check(!b.CheckFoundation(m.transform.TransformPoint(new Vector3(mountain.x*2,0,mountain.y*2)),out _,out _),"Mountain foundation rejected");
        Check(!b.IsPlayable(m.transform.TransformPoint(new Vector3(-4,0,0))),"Map exterior rejected");
        var left=m.transform.TransformPoint(new Vector3(20*2,0,30*2));
        var right=m.transform.TransformPoint(new Vector3(85*2,0,30*2));
        Check(!b.HasClearRoute(left,right),"Cross-river straight route rejected");
        return $"PASS EditMode: {meshes.Length} persisted clusters, 45-degree/2m grid, {safe} safe samples, {blocked} excluded samples, peak {highest:F2}m, height cache max error {maxCacheError:F4}m, river/mountain/exterior rejection, cross-river route rejection.";
    }
    public static string PlayMode()
    {
        Check(Application.isPlaying,"Play mode required");
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();var b=s.Ground.GetComponent<TileWorldGround>();
        s.GetComponent<OpenWorldInput>().Cancel();s.ResetEnemies();
        FoundationPlacement.Platform added=null;
        try
        {
            bool found=false;
            for(int z=-5;z<=5&&!found;z++)for(int x=-5;x<=5&&!found;x++)
            {
                var p=WorldGridGeometry.Center(new Vector2Int(x,z),8);
                if(!s.Foundations.CheckFoundation(p,out _,out _))continue;
                Check(s.Foundations.AddFoundation(p,out _),"Foundation on TWC plain");
                Check(s.Foundations.FindCell(p,out added,out _,out _),"Foundation lookup");found=true;
            }
            Check(found,"Buildable plain exists");
            Check(s.Spawn("512"),"Spawn request accepted");s.Running=false;
            for(int i=0;i<20&&s.SpawnStream.Pending>0;i++)s.SpawnStream.Pump();
            Check(s.Enemies.Spawned==512,"512 enemies spawned on eligible routes");
            for(int step=0;step<100;step++)s.Enemies.MoveAndIndex(.1f);
            int alive=0;
            for(int i=0;i<s.Enemies.MaxCount;i++)
            {
                var e=s.Enemies.GetEnemy(i);if(!e.alive)continue;alive++;
                Check(b.IsPlayable(e.position),"Enemy remains on playable plain");
                Check(b.HasClearRoute(e.position,e.destination),"Remaining route stays clear");
                Check(Mathf.Abs(e.position.y-1.55f)<.06f,"Enemy height follows TWC flat surface");
            }
            Check(alive>0,"Simulation progressed with live enemies");
            return $"PASS PlayMode: foundation placement/lookup, 512 enemies spawned, 10 simulated seconds, {alive} live enemies remain on valid ground/routes at correct height. Cleanup performed.";
        }
        finally{s.ResetEnemies();if(added!=null)s.Foundations.RemoveFoundation(added);}
    }
}
