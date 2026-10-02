using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EternalSteam.OpenWorld;
using GiantGrey.TileWorldCreator;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EternalSteam.OpenWorld.Editor
{
    // One-shot Editor authoring. The map meshes, colliders and height cache are saved in
    // the scene/assets; nothing is generated when a player enters Play mode.
    [InitializeOnLoad]
    public static class StartRegion500MapAuthoring
    {
        const string ScenePath="Assets/EternalSteam/Scene/Tests/StartRegionSandbox.unity";
        const string SourcePath="Assets/Map/TileWorldCreatorConfiguration.asset";
        const string Output="Assets/EternalSteam/Content/Environments/StartRegion500";
        const string Request="Temp/ApplyStartRegion500.request";
        const string Status="Temp/ApplyStartRegion500.status";
        static bool running;
        static double nextPoll;

        static StartRegion500MapAuthoring()
        {
            EditorApplication.update+=Poll;
        }

        static void Poll()
        {
            if(running||EditorApplication.timeSinceStartup<nextPoll)return;
            nextPoll=EditorApplication.timeSinceStartup+2;
            if(File.Exists(Request))Run();
        }

        [MenuItem("Eternal Steam/Open World/Apply 500x500 Start Region Map")]
        public static void Run(){if(!running)Apply();}

        static async void Apply()
        {
            running=true;
            try
            {
                if(File.Exists(Request))File.Delete(Request);
                File.WriteAllText(Status,"running");
                string result=await ApplyScene();
                File.WriteAllText(Status,"complete\n"+result);
                Debug.Log(result);
            }
            catch(Exception e)
            {
                File.WriteAllText(Status,"failed\n"+e);
                Debug.LogException(e);
            }
            finally{running=false;EditorUtility.ClearProgressBar();}
        }

        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            string parent=Path.GetDirectoryName(path)?.Replace('\\','/');
            if(string.IsNullOrEmpty(parent))throw new InvalidOperationException("Invalid asset folder: "+path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }

        static async Task<string> ApplyScene()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before replacing the map.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save the open scene first; unsaved Editor changes were preserved.");
            int surfaceLayer=LayerMask.NameToLayer("TWC Ground");
            if(surfaceLayer<0)throw new InvalidOperationException("Missing TWC Ground physics layer.");
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var sandbox=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
            if(sandbox==null||sandbox.Ground==null||sandbox.StartingBase==null)
                throw new InvalidOperationException("Start scene is missing the sandbox, height cache or fixed main base.");
            if(sandbox.GetComponentsInChildren<SceneFoundation>(true).Length!=0||sandbox.GetComponentsInChildren<SceneTower>(true).Length!=0)
                throw new InvalidOperationException("Relocate scene-authored buildings before changing terrain.");
            var bridge=sandbox.Ground.GetComponent<TileWorldGround>();
            if(bridge==null||bridge.GridRoot==null)throw new InvalidOperationException("Missing current map bridge.");
            var oldRoot=bridge.GridRoot.gameObject;
            if(oldRoot.name=="TWC · Start Region 500"&&bridge.Width==500&&bridge.Height==500)
                return "StartRegionSandbox already uses the 500x500 TWC map.";
            // A prior failed bake can leave tool-owned mesh assets, but the active
            // scene still references only the old map at this point.
            if(AssetDatabase.IsValidFolder(Output+"/Meshes")&&oldRoot.name!="TWC · Start Region 500")
                AssetDatabase.DeleteAsset(Output+"/Meshes");
            if(oldRoot.name!="TWC · Start Region 500"&&AssetDatabase.LoadAssetAtPath<TerrainData>(Output+"/HeightCache.asset")!=null)
                AssetDatabase.DeleteAsset(Output+"/HeightCache.asset");
            string backup=AssetDatabase.GenerateUniqueAssetPath("Assets/EternalSteam/Scene/Tests/StartRegionSandbox_Before500Map.unity");
            if(!AssetDatabase.CopyAsset(ScenePath,backup))throw new IOException("Could not back up the start scene.");
            EnsureFolder(Output);
            EnsureFolder(Output+"/Meshes");
            // AssetDatabase operations can unload the very large TWC configuration.
            // Resolve it only after the backup and output folders have been imported.
            var config=AssetDatabase.LoadAssetAtPath<Configuration>(SourcePath);
            if(config==null||config.width!=500||config.height!=500||Mathf.Abs(config.cellSize-1)>0.0001f)
                throw new InvalidOperationException("The supplied TWC map must be 500x500 with 1m cells.");
            var root=new GameObject("TWC · Start Region 500");
            root.transform.rotation=WorldGridGeometry.Rotation;
            var main=sandbox.StartingBase;
            // (248,257) is a clear 25x25 patch in the supplied Floor1 blueprint.
            // Keeping the fixed main's world XZ also preserves the 2m construction lattice.
            root.transform.position=new Vector3(main.transform.position.x,0,main.transform.position.z)
                -root.transform.rotation*new Vector3(248,0,257);
            var manager=root.AddComponent<TileWorldCreatorManager>();
            manager.configuration=config;config.SetManager(manager);
            bool oldActive=oldRoot.activeSelf;
            try
            {
                // The supplied 5-cell clusters would create roughly ten thousand roots.
                // This changes only bake granularity, not painted cells or tile presets.
                if(config==null)config=AssetDatabase.LoadAssetAtPath<Configuration>(SourcePath);
                if(config==null)throw new InvalidOperationException("TWC configuration was unloaded during scene setup.");
                manager.configuration=config;
                config.clusterCellSize=25;
                EditorUtility.DisplayProgressBar("500x500 시작 맵","TWC 메시 생성",0.05f);
                manager.ExecuteBuildLayers(ExecutionMode.FromScratch);
                var layers=config.buildLayerFolders.SelectMany(f=>f.buildLayers).ToArray();
                var deadline=DateTime.UtcNow.AddMinutes(15);
                while(layers.Any(l=>l.isExecuting))
                {
                    if(DateTime.UtcNow>deadline)throw new TimeoutException("TWC bake exceeded 15 minutes.");
                    await Task.Delay(100);
                }
                for(int i=0;i<layers.Length+4;i++)await NextEditorUpdate();
                var filters=root.GetComponentsInChildren<MeshFilter>(true)
                    .Where(f=>f.sharedMesh!=null&&f.gameObject.activeInHierarchy&&f.TryGetComponent<MeshRenderer>(out var r)&&r.enabled).ToArray();
                if(filters.Length==0)throw new InvalidOperationException("TWC generated no visible meshes.");
                foreach(var filter in filters)
                {
                    var mesh=filter.sharedMesh;
                    filter.gameObject.layer=surfaceLayer;
                    var collider=filter.GetComponent<MeshCollider>();
                    if(collider==null)collider=filter.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh=mesh;
                }
                foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=surfaceLayer;
                oldRoot.SetActive(false);Physics.SyncTransforms();
                var bounds=filters[0].GetComponent<Renderer>().bounds;
                foreach(var f in filters)bounds.Encapsulate(f.GetComponent<Renderer>().bounds);
                float rayTop=bounds.max.y+10,rayLength=bounds.size.y+22;
                bool Sample(Vector3 p,out RaycastHit hit)=>Physics.Raycast(new Vector3(p.x,rayTop,p.z),Vector3.down,out hit,rayLength,1<<surfaceLayer,QueryTriggerInteraction.Ignore);

                EditorUtility.DisplayProgressBar("500x500 시작 맵","통행 셀과 지형 높이 캐시",0.5f);
                var bad=new bool[500*500];
                foreach(var folder in config.blueprintLayerFolders)
                foreach(var layer in folder.blueprintLayers)
                {
                    if(layer==null||layer.layerName=="Floor1")continue;
                    foreach(var cell in layer.allPositions)
                    {
                        int x=Mathf.RoundToInt(cell.x),z=Mathf.RoundToInt(cell.y);
                        if(x>=0&&x<500&&z>=0&&z<500)bad[z*500+x]=true;
                    }
                }
                var playable=new byte[500*500];int count=0;
                for(int z=2;z<498;z++)for(int x=2;x<498;x++)
                {
                    bool clear=true;
                    for(int dz=-1;dz<=1&&clear;dz++)for(int dx=-1;dx<=1;dx++)
                        if(bad[(z+dz)*500+x+dx]){clear=false;break;}
                    if(clear){playable[z*500+x]=1;count++;}
                }
                bridge.GridRoot=root.transform;bridge.Width=500;bridge.Height=500;
                bridge.CellSize=1;bridge.SurfaceMask=1<<surfaceLayer;bridge.PlayableCells=playable;
                var basePoint=root.transform.TransformPoint(new Vector3(248,0,257));
                if(!bridge.IsPlayable(basePoint)||!Sample(basePoint,out var baseHit))
                    throw new InvalidOperationException("Fixed main base has no flat surface in the new map.");
                main.transform.position=new Vector3(main.transform.position.x,baseHit.point.y+.05f,main.transform.position.z);

                var data=new TerrainData{name="Start region 500 height cache",heightmapResolution=513};
                float bottom=bounds.min.y-2;
                data.size=new Vector3(bounds.size.x,Mathf.Max(8,bounds.size.y+4),bounds.size.z);
                var origin=new Vector3(bounds.min.x,bottom,bounds.min.z);
                var heights=new float[513,513];
                for(int z=0;z<513;z++)for(int x=0;x<513;x++)
                {
                    var p=origin+new Vector3(x*data.size.x/512,0,z*data.size.z/512);
                    heights[z,x]=Sample(p,out var hit)?Mathf.Clamp01((hit.point.y-bottom)/data.size.y):0;
                }
                data.SetHeights(0,0,heights);
                sandbox.Ground.terrainData=data;sandbox.Ground.transform.position=origin;
                sandbox.Ground.drawHeightmap=false;sandbox.Ground.drawTreesAndFoliage=false;
                var terrainCollider=sandbox.Ground.GetComponent<TerrainCollider>();
                if(terrainCollider!=null){terrainCollider.enabled=false;terrainCollider.terrainData=data;}

                var assault=sandbox.AssaultSettings;
                if(assault==null)throw new InvalidOperationException("Missing start region assault settings.");
                var candidates=new List<(Vector3 point,int angle,int radius)>();
                for(int angle=0;angle<360;angle+=5)
                {
                    Vector3 furthest=default;int distance=0;
                    float radians=angle*Mathf.Deg2Rad;
                    for(int radius=8;radius<=180;radius+=2)
                    {
                        var p=root.transform.TransformPoint(new Vector3(248+Mathf.Cos(radians)*radius,0,257+Mathf.Sin(radians)*radius));
                        if(!bridge.IsPlayable(p)||!bridge.HasClearRoute(p,basePoint))break;
                        if(Sample(p,out var hit)){furthest=new Vector3(p.x,hit.point.y+.5f,p.z);distance=radius;}
                    }
                    if(distance>=8)candidates.Add((furthest,angle,distance));
                }
                var selected=new List<(Vector3 point,int angle,int radius)>();
                foreach(var candidate in candidates.OrderByDescending(c=>c.radius))
                {
                    if(selected.Count==8)break;
                    if(selected.Any(c=>Mathf.Abs(Mathf.DeltaAngle(c.angle,candidate.angle))<25||
                        new Vector2(c.point.x-candidate.point.x,c.point.z-candidate.point.z).magnitude<10))continue;
                    selected.Add(candidate);
                }
                if(selected.Count<8)foreach(var candidate in candidates.OrderByDescending(c=>c.radius))
                {
                    if(selected.Count==8)break;
                    if(selected.Any(c=>new Vector2(c.point.x-candidate.point.x,c.point.z-candidate.point.z).magnitude<8))continue;
                    selected.Add(candidate);
                }
                if(selected.Count<8)throw new InvalidOperationException($"Only {selected.Count} separate connected spawn points were found.");
                assault.BossWavePoints=selected.Select(c=>c.point).ToArray();
                assault.BossPosition=assault.BossWavePoints[0];
                AssetDatabase.CreateAsset(data,AssetDatabase.GenerateUniqueAssetPath(Output+"/HeightCache.asset"));
                var persisted=new HashSet<Mesh>();int meshCount=0;
                foreach(var filter in filters)
                {
                    var mesh=filter.sharedMesh;
                    if(!AssetDatabase.Contains(mesh)&&persisted.Add(mesh))
                        AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Output+"/Meshes/Cluster_"+(meshCount++)+".asset"));
                }
                EditorUtility.SetDirty(assault);
                if(sandbox.Regions!=null&&sandbox.Regions.Length>=2)
                {
                    var a=sandbox.Regions[0];var b=sandbox.Regions[1];
                    a.Bounds=new Rect(origin.x,origin.z,data.size.x*.5f,data.size.z);
                    b.Bounds=new Rect(origin.x+data.size.x*.5f,origin.z,data.size.x*.5f,data.size.z);
                    EditorUtility.SetDirty(a);EditorUtility.SetDirty(b);
                }
                sandbox.CameraRig.Focus=main.transform.position;
                sandbox.CameraRig.Zoom=56;
                sandbox.CameraRig.View.orthographicSize=56;
                sandbox.CameraRig.View.transform.rotation=Quaternion.Euler(60,0,0);
                sandbox.CameraRig.View.transform.position=main.transform.position-sandbox.CameraRig.View.transform.forward*110;
                EditorUtility.SetDirty(sandbox.CameraRig);EditorUtility.SetDirty(main);EditorUtility.SetDirty(bridge);
                UnityEngine.Object.DestroyImmediate(oldRoot);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                Selection.activeGameObject=root;
                SceneView.lastActiveSceneView?.Frame(bounds,false);
                return $"StartRegionSandbox now uses the 500x500 TWC map; {filters.Length} visible mesh filters, {meshCount} new persistent meshes, {count} playable cells. Backup: {backup}";
            }
            catch
            {
                if(oldRoot!=null)oldRoot.SetActive(oldActive);
                if(root!=null)UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        static Task NextEditorUpdate()
        {
            var task=new TaskCompletionSource<bool>();
            EditorApplication.CallbackFunction callback=null;
            callback=()=>{EditorApplication.update-=callback;task.TrySetResult(true);};
            EditorApplication.update+=callback;
            return task.Task;
        }
    }
}
