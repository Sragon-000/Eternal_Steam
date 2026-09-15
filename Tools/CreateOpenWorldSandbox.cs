using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using EternalSteam.OpenWorld;

public static class CreateOpenWorldSandbox
{
    const string Root="Assets/EternalSteam";
    const string Folder=Root+"/Content/Environments/TestGround";
    static void Ensure(string path) {if(AssetDatabase.IsValidFolder(path))return;Ensure(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
    static T Load<T>(string path) where T:UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(Root+"/"+path) ?? throw new Exception("Missing "+path);
    static Material Material(string name,string shader,Color color)
    {
        var path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;
    }
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Stop Play first");
        Ensure(Folder);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        string path=Folder+"/TestTerrain.asset";var data=AssetDatabase.LoadAssetAtPath<TerrainData>(path);
        if(data==null){data=new TerrainData();AssetDatabase.CreateAsset(data,path);}
        data.heightmapResolution=257;data.size=new Vector3(256,32,256);
        var heights=new float[257,257];
        for(int z=0;z<=256;z++)for(int x=0;x<=256;x++) {
            float distance=new Vector2(x-128,z-128).magnitude;
            float rim=Mathf.SmoothStep(0,1,Mathf.InverseLerp(42,115,distance));
            heights[z,x]=.05f+rim*(.11f+.48f*Mathf.PerlinNoise(x*.019f+12,z*.019f+7));
        }
        data.SetHeights(0,0,heights);
        var texturePath=Folder+"/GrassTint.asset";var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if(texture==null){texture=new Texture2D(16,16,TextureFormat.RGB24,false);AssetDatabase.CreateAsset(texture,texturePath);}
        for(int y=0;y<16;y++)for(int x=0;x<16;x++)texture.SetPixel(x,y,Color.Lerp(new Color(.25f,.34f,.23f),new Color(.37f,.44f,.29f),Mathf.PerlinNoise(x*.4f,y*.4f)));
        texture.Apply();EditorUtility.SetDirty(texture);
        var layerPath=Folder+"/Grass.terrainlayer";var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        if(layer==null){layer=new TerrainLayer();AssetDatabase.CreateAsset(layer,layerPath);}layer.diffuseTexture=texture;layer.tileSize=new Vector2(12,12);data.terrainLayers=new[]{layer};EditorUtility.SetDirty(layer);EditorUtility.SetDirty(data);
        var terrain=Terrain.CreateTerrainGameObject(data).GetComponent<Terrain>();terrain.name="Test Terrain · 256m";terrain.transform.position=new Vector3(-128,0,-128);
        terrain.materialTemplate=Material("Terrain","Universal Render Pipeline/Terrain/Lit",Color.white);terrain.drawInstanced=true;
        var camera=new GameObject("Top View Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.tag="MainCamera";
        camera.orthographic=true;camera.orthographicSize=26;camera.nearClipPlane=.1f;camera.farClipPlane=450;
        camera.backgroundColor=new Color(.39f,.49f,.53f);camera.clearFlags=CameraClearFlags.SolidColor;
        camera.transform.rotation=Quaternion.Euler(60,0,0);camera.transform.position=new Vector3(0,1.6f,0)-camera.transform.forward*110;
        var rig=camera.gameObject.AddComponent<FreeCameraRig>();rig.Ground=terrain;rig.View=camera;
        var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(50,-30,0);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.7f,.76f);
        var world=new GameObject("Open World Test Space").AddComponent<OpenWorldSandbox>();world.Ground=terrain;world.CameraRig=rig;
        world.EnemyMesh=Load<Mesh>("Content/Enemies/HordeEnemy/Art/EnemyCapsule.asset");world.EnemyMaterial=Load<Material>("Content/Enemies/HordeEnemy/Art/Materials/Enemy.mat");
        world.TowerMaterial=Load<Material>("Content/Buildings/HordeTowers/Art/Materials/Tower.mat");world.BarrelMaterial=Load<Material>("Content/Buildings/HordeTowers/Art/Materials/Barrel.mat");
        world.LineMaterial=Load<Material>("Shared/Materials/Demo/Tracer.mat");world.ValidMaterial=Load<Material>("Shared/Materials/Demo/ValidPlacement.mat");world.InvalidMaterial=Load<Material>("Shared/Materials/Demo/InvalidPlacement.mat");
        world.FoundationMaterial=Material("Foundation","Universal Render Pipeline/Lit",new Color(.24f,.31f,.35f));
        var input=world.gameObject.AddComponent<OpenWorldInput>();input.Sandbox=world;
        var go=new GameObject("Test Space HUD");var doc=go.AddComponent<UIDocument>();doc.panelSettings=Load<PanelSettings>("Settings/UI/SamplePanel.asset");doc.visualTreeAsset=Load<VisualTreeAsset>("Shared/UI/OpenWorld/OpenWorldHud.uxml");
        var hud=go.AddComponent<OpenWorldHud>();hud.Sandbox=world;hud.Input=input;
        EditorSceneManager.SaveScene(scene,Root+"/Scene/Tests/OpenWorldSandbox.unity");AssetDatabase.SaveAssets();
        return "Created OpenWorldSandbox: Terrain, free camera, foundation/tower services, spawn UI. Build settings unchanged.";
    }
}
