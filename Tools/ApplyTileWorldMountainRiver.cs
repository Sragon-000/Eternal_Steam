using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using GiantGrey.TileWorldCreator;
using EternalSteam.OpenWorld;

public static class ApplyTileWorldMountainRiver
{
    const string ScenePath = "Assets/EternalSteam/Scene/Tests/OpenWorldSandbox.unity";
    const string BaseFolder = "Assets/EternalSteam/Content/Environments/TileWorldMountainRiver";
    const int Size = 96;
    const float Cell = 2;
    static string folder;
    static void Ensure(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\','/'); Ensure(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    static float River(float z) => 57 + 7 * Mathf.Sin((z - 8) * .077f) + 2 * Mathf.Sin(z * .19f);
    static int Mountain(int x, int z)
    {
        float Peak(float px,float pz,float radius,float height) => Mathf.Max(0, 1 - Vector2.Distance(new Vector2(x,z),new Vector2(px,pz))/radius)*height;
        float h = Mathf.Max(Peak(20,74,22,11),Peak(33,85,19,9),Peak(13,55,15,7),Peak(81,76,18,9),Peak(80,17,17,8));
        if (h <= 0) return 0;
        h += (Mathf.PerlinNoise(x*.15f+9,z*.15f+13)-.5f)*1.5f;
        return Mathf.Clamp(Mathf.FloorToInt(h),0,11);
    }
    static TilesBuildLayer Layer(TileWorldCreatorManager manager,string name,HashSet<Vector2> cells,float height,string preset)
    {
        var bp=manager.AddNewBlueprintLayer(name);bp.defaultLayerHeight=height;bp.AddCells(cells);
        bp.layerColor=name.Contains("River")?new Color(.1f,.5f,.8f):new Color(.3f,.65f,.3f);
        var build=manager.AddNewBuildLayer<TilesBuildLayer>(name);build.SetBlueprintLayer(bp);
        var tile=AssetDatabase.LoadAssetAtPath<TilePreset>("Assets/TileWorldCreator/Tiles URP/"+preset);
        if(tile==null)throw new Exception("Missing preset "+preset);
        build.SetNewTilePreset(tile);build.useDualGrid=true;
        EditorUtility.SetDirty(bp);EditorUtility.SetDirty(build);return build;
    }
    static void Polish(TileWorldCreatorManager manager)
    {
        Material Make(string name,Color color,float smooth=.08f)
        {
            string path=folder+"/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",smooth);mat.enableInstancing=true;
            EditorUtility.SetDirty(mat);return mat;
        }
        var grass=Make("Valley grass",new Color(.27f,.43f,.25f));
        var sand=Make("Riverbank sand",new Color(.52f,.46f,.31f));
        var water=Make("River teal",new Color(.045f,.33f,.40f),.35f);
        var rock=new Material[11];
        for(int i=0;i<11;i++)rock[i]=Make("Mountain tier "+(i+1),Color.Lerp(new Color(.31f,.39f,.29f),new Color(.56f,.59f,.57f),i/10f));
        foreach(var layer in manager.configuration.buildLayerFolders.SelectMany(f=>f.buildLayers))
        {
            var mat=layer.layerName=="Grass plain"?grass:layer.layerName=="River"?water:sand;
            if(layer.layerName.StartsWith("Mountain terrace "))mat=rock[int.Parse(layer.layerName.Substring(17))-1];
            foreach(var r in layer.GetLayerObject(manager.gameObject).GetComponentsInChildren<MeshRenderer>())
                r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();
        }
    }
    public static string PolishCurrent()
    {
        var m=UnityEngine.Object.FindFirstObjectByType<TileWorldCreatorManager>();
        folder=Path.GetDirectoryName(AssetDatabase.GetAssetPath(m.configuration)).Replace('\\','/');
        Polish(m);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(m.gameObject.scene);
        return "Applied valley/rock/river URP palette";
    }
    public static async Task<string> Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play before generation");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!=ScenePath)throw new Exception("OpenWorldSandbox must be the active scene");
        var world=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        if(world==null || world.Ground==null)throw new Exception("Missing sandbox or ground");
        if(world.GetComponentsInChildren<SceneFoundation>().Length>0)throw new Exception("Authored foundations must be relocated before replacing this map");
        EditorSceneManager.SaveScene(scene);
        const string backup="Assets/EternalSteam/Scene/Tests/OpenWorldSandbox_BeforeTileWorld.unity";
        if(!File.Exists(backup) && !AssetDatabase.CopyAsset(ScenePath,backup))throw new Exception("Scene backup failed");
        Ensure(BaseFolder);folder=AssetDatabase.GenerateUniqueAssetPath(BaseFolder+"/Generated");Ensure(folder);
        var oldRoot=GameObject.Find("TWC · Mountain River");if(oldRoot!=null)UnityEngine.Object.DestroyImmediate(oldRoot);
        var root=new GameObject("TWC · Mountain River");
        root.transform.rotation=WorldGridGeometry.Rotation;
        // Align logical cell centers to the existing 2m construction lattice.
        root.transform.position=WorldGridGeometry.ToWorld(new Vector3(-95,0,-95));
        var manager=root.AddComponent<TileWorldCreatorManager>();
        var config=ScriptableObject.CreateInstance<Configuration>();config.name="Mountain River · Seed 170926";
        config.width=Size;config.height=Size;config.cellSize=Cell;config.lastCellSize=Cell;
        config.clusterCellSize=12;config.mergeTiles=true;config.colliderType=Configuration.ColliderType.meshCollider;
        config.useGlobalRandomSeed=true;config.globalRandomSeed=170926;
        AssetDatabase.CreateAsset(config,folder+"/Configuration.asset");manager.configuration=config;config.SetManager(manager);
        var all=new HashSet<Vector2>();var land=new HashSet<Vector2>();var river=new HashSet<Vector2>();
        var mountains=new HashSet<Vector2>[11];for(int i=0;i<11;i++)mountains[i]=new HashSet<Vector2>();
        var playable=new byte[Size*Size];
        for(int z=0;z<Size;z++)for(int x=0;x<Size;x++)
        {
            var p=new Vector2(x,z);all.Add(p);float d=Mathf.Abs(x-River(z));
            int tier=Mountain(x,z);
            if(d<3.8f)river.Add(p);
            if(d>4.8f)land.Add(p);
            if(d>6.3f && x>2 && z>2 && x<Size-3 && z<Size-3 && tier==0)playable[z*Size+x]=1;
            if(d>6)for(int i=0;i<tier;i++)mountains[i].Add(p);
        }
        // Erode playable area around mountain edges to keep the full footprint off cliff geometry.
        var safe=(byte[])playable.Clone();
        for(int z=1;z<Size-1;z++)for(int x=1;x<Size-1;x++)
            for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
                if(playable[(z+dz)*Size+x+dx]==0)safe[z*Size+x]=0;
        Layer(manager,"Riverbed and banks",all,-.9f,"SandBlockTiles/SandBlocksPreset.asset");
        Layer(manager,"Grass plain",land,0,"GrassTiles/GrassTilesPreset.asset");
        Layer(manager,"River",river,-.2f,"RiverTiles/RiverPreset.asset");
        for(int i=0;i<mountains.Length;i++)if(mountains[i].Count>0)
            Layer(manager,"Mountain terrace "+(i+1),mountains[i],(i+1)*2,"CliffTiles/CliffTilesPreset_A.asset");
        manager.GenerateCompleteMap();
        // TWC runs Editor coroutines; its OnMapReady notification precedes mesh generation.
        var deadline=DateTime.UtcNow.AddSeconds(90);
        while(config.buildLayerFolders.SelectMany(f=>f.buildLayers).Any(l=>l.isExecuting))
        {
            if(DateTime.UtcNow>deadline)throw new Exception("TWC generation timed out");
            await Task.Delay(100);
        }
        // PostExecuteLayer merges one layer per Editor update, after all tile generation finishes.
        for(int frame=0;frame<config.buildLayerFolders.Sum(f=>f.buildLayers.Count)+3;frame++)
        {
            var tick=new TaskCompletionSource<bool>();
            EditorApplication.CallbackFunction next=null;
            next=()=>{EditorApplication.update-=next;tick.TrySetResult(true);};
            EditorApplication.update+=next;await tick.Task;
        }
        // Reserve one previously empty physics layer through Unity's serialized settings API.
        var tags=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers=tags.FindProperty("layers");int surfaceLayer=LayerMask.NameToLayer("TWC Ground");
        if(surfaceLayer<0){for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)){surfaceLayer=i;break;}}
        if(surfaceLayer<0)throw new Exception("No free physics layer");
        layers.GetArrayElementAtIndex(surfaceLayer).stringValue="TWC Ground";tags.ApplyModifiedProperties();
        foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=surfaceLayer;
        // Persist generated cluster meshes: scene reload must not depend on transient meshes.
        Ensure(folder+"/Meshes");var persisted=new HashSet<Mesh>();int meshIndex=0;
        foreach(var mesh in root.GetComponentsInChildren<MeshFilter>().Select(f=>f.sharedMesh)
            .Concat(root.GetComponentsInChildren<MeshCollider>().Select(c=>c.sharedMesh)))
            if(mesh!=null && !AssetDatabase.Contains(mesh) && persisted.Add(mesh))AssetDatabase.CreateAsset(mesh,folder+"/Meshes/Cluster_"+(meshIndex++)+".asset");
        Polish(manager);
        Physics.SyncTransforms();
        var renderers=root.GetComponentsInChildren<Renderer>();if(renderers.Length==0)throw new Exception("TWC generated no renderers");
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        var terrain=world.Ground;var collider=terrain.GetComponent<TerrainCollider>();if(collider!=null)collider.enabled=false;
        terrain.drawHeightmap=false;terrain.drawTreesAndFoliage=false;
        var data=new TerrainData();data.name="TWC baked height cache";data.heightmapResolution=513;
        data.size=new Vector3(bounds.size.x,64,bounds.size.z);
        terrain.transform.position=new Vector3(bounds.min.x,-4,bounds.min.z);
        var heights=new float[513,513];int hits=0;
        for(int z=0;z<=512;z++)for(int x=0;x<=512;x++)
        {
            var p=terrain.transform.position+new Vector3(x*data.size.x/512,204,z*data.size.z/512);
            float y=-4;
            if(Physics.Raycast(p,Vector3.down,out var hit,400,1<<surfaceLayer,QueryTriggerInteraction.Ignore)){y=hit.point.y;hits++;}
            heights[z,x]=Mathf.Clamp01((y+4)/64);
        }
        data.SetHeights(0,0,heights);AssetDatabase.CreateAsset(data,folder+"/HeightCache.asset");terrain.terrainData=data;
        if(collider!=null)collider.terrainData=data;
        terrain.name="TWC Height Cache · hidden Terrain";
        var bridge=terrain.GetComponent<TileWorldGround>();if(bridge==null)bridge=terrain.gameObject.AddComponent<TileWorldGround>();
        bridge.GridRoot=root.transform;bridge.Width=Size;bridge.Height=Size;bridge.CellSize=Cell;bridge.SurfaceMask=1<<surfaceLayer;bridge.PlayableCells=safe;
        world.CameraRig.Focus=new Vector3(0,1,0);world.CameraRig.Zoom=62;
        var cam=world.CameraRig.View;cam.orthographicSize=62;cam.farClipPlane=600;
        cam.transform.rotation=Quaternion.Euler(60,0,0);cam.transform.position=new Vector3(0,1,0)-cam.transform.forward*160;
        cam.backgroundColor=new Color(.17f,.24f,.29f);
        var sun=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l=>l.type==LightType.Directional);
        if(sun!=null){sun.transform.rotation=Quaternion.Euler(48,-35,0);sun.intensity=1.35f;}
        RenderSettings.ambientLight=new Color(.65f,.71f,.79f);
        foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(folder+"/Configuration.asset"))EditorUtility.SetDirty(asset);
        EditorUtility.SetDirty(world);EditorUtility.SetDirty(terrain);EditorUtility.SetDirty(bridge);EditorUtility.SetDirty(world.CameraRig);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=root;SceneView.lastActiveSceneView?.Frame(bounds,false);
        return "Applied TWC mountain river: "+Size+"x"+Size+" cells, "+config.buildLayerFolders.Sum(f=>f.buildLayers.Count)+" layers, "+meshIndex+" saved meshes, "+hits+" height samples, "+safe.Count(b=>b!=0)+" playable cells. Assets: "+folder+". Backup: "+backup;
    }
}
