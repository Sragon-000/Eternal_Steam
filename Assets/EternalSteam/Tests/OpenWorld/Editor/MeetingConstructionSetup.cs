using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class MeetingConstructionSetup
{
    const string Root="Assets/EternalSteam/Content/Buildings/StartRegion/";
    static T Asset<T>(string name)where T:ScriptableObject {
        var path=Root+name+".asset";var value=AssetDatabase.LoadAssetAtPath<T>(path);
        if(value==null){value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);}return value;
    }
    [MenuItem("Eternal Steam/Open World/Apply Meeting Construction")]
    public static void Apply() => Debug.Log(Main());
    public static string Main() {
        if(Application.isPlaying)throw new Exception("Stop Play before authoring.");
        var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(current.isDirty)throw new Exception("Preserve unsaved scene before applying.");
        if(!AssetDatabase.IsValidFolder(Root.TrimEnd('/')))AssetDatabase.CreateFolder("Assets/EternalSteam/Content/Buildings","StartRegion");
        var source=AssetDatabase.LoadAssetAtPath<BuildingCatalog>("Assets/EternalSteam/Content/Buildings/DocumentContent/DocumentBuildings.asset");
        var catalog=Asset<BuildingCatalog>("StartRegionBuildings");catalog.Buildings.Clear();
        foreach(var original in source.Buildings) {
            // Existing resource content stays in its own verification catalog until its economic role is decided.
            if(original.Category==BuildingCategory.Resource)continue;
            var copy=Asset<BuildingDefinition>(original.Id);EditorUtility.CopySerialized(original,copy);
            var profile=Asset<BuildingPlacementDefinition>(original.Id+".placement");EditorUtility.CopySerialized(original.Placement,profile);
            profile.RequiresBuildArea=false;profile.RequiresOperationalArea=original.Category==BuildingCategory.Defense;
            profile.RequiresOwnerBase=profile.RequiresOperationalArea;profile.VerificationSettings=true;copy.Placement=profile;
            if(copy.Id=="installation.nexus") {
                var area=Asset<BuildAreaModuleDefinition>("nexus.area");area.Shape=BuildAreaShape.Square;area.Radius=40;area.Yaw=45;
                copy.Modules.RemoveAll(m=>m is BuildAreaModuleDefinition||m is BaseModuleDefinition);
                copy.Modules.Add(area);copy.Modules.Add(Asset<BaseModuleDefinition>("nexus.identity"));EditorUtility.SetDirty(area);
            }
            EditorUtility.SetDirty(copy);EditorUtility.SetDirty(profile);catalog.Buildings.Add(copy);
        }
        var outpost=Asset<BuildingDefinition>("installation.outpost");outpost.Id="installation.outpost";outpost.DisplayName="외부 전초";outpost.Category=BuildingCategory.Installation;outpost.Footprint=new Vector2Int(2,2);outpost.Recoverable=true;
        var placement=Asset<BuildingPlacementDefinition>("outpost.placement");placement.Surface=BuildingSurface.Ground;placement.RequiresOwnerBase=true;placement.RequiresOperationalArea=false;placement.RequiresBuildArea=false;placement.VerificationSettings=true;outpost.Placement=placement;
        var coverage=Asset<BuildAreaModuleDefinition>("outpost.area");coverage.Shape=BuildAreaShape.Square;coverage.Radius=20;coverage.Yaw=45;
        outpost.Modules.RemoveAll(m=>m is BuildAreaModuleDefinition);outpost.Modules.Add(coverage);
        var health=Asset<HealthModuleDefinition>("installation.outpost.health");health.Maximum=50;
        var body=Asset<BuildingCombatDefinition>("installation.outpost.combat");body.Role=BuildingCombatRole.General;body.BlocksGround=true;
        outpost.Modules.RemoveAll(m=>m is HealthModuleDefinition||m is BuildingCombatDefinition);outpost.Modules.Add(health);outpost.Modules.Add(body);EditorUtility.SetDirty(health);EditorUtility.SetDirty(body);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Outpost.prefab");
        if(prefab==null){var model=new GameObject("External outpost");
            var steel=AssetDatabase.LoadAssetAtPath<Material>("Assets/EternalSteam/Content/Buildings/DocumentContent/Model_Steel.mat");
            void Part(string name,Vector3 position,Vector3 scale){var p=GameObject.CreatePrimitive(PrimitiveType.Cube);p.name=name;p.transform.SetParent(model.transform,false);p.transform.localPosition=position;p.transform.localScale=scale;p.GetComponent<Renderer>().sharedMaterial=steel;UnityEngine.Object.DestroyImmediate(p.GetComponent<Collider>());}
            Part("Platform",new Vector3(0,.2f,0),new Vector3(4,.4f,4));Part("Signal mast",new Vector3(0,2,0),new Vector3(.5f,4,.5f));Part("Signal bar",new Vector3(0,3.3f,0),new Vector3(3,.3f,.3f));
            model.AddComponent<BoxCollider>().size=new Vector3(4,4,4);model.GetComponent<BoxCollider>().center=Vector3.up*2;
            prefab=PrefabUtility.SaveAsPrefabAsset(model,Root+"Outpost.prefab");UnityEngine.Object.DestroyImmediate(model);
        }outpost.ViewPrefab=prefab;catalog.Buildings.Add(outpost);
        const string scene="Assets/EternalSteam/Scene/Tests/StartRegionSandbox.unity";
        if(!AssetDatabase.LoadAssetAtPath<SceneAsset>(scene)&&!AssetDatabase.CopyAsset("Assets/EternalSteam/Scene/Tests/OpenWorldSandbox.unity",scene))throw new Exception("Scene copy failed.");
        foreach(var value in new UnityEngine.Object[]{outpost,placement,coverage,catalog})EditorUtility.SetDirty(value);
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(scene);
        catalog=AssetDatabase.LoadAssetAtPath<BuildingCatalog>(Root+"StartRegionBuildings.asset");
        var sandbox=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        sandbox.MeetingConstructionRules=true;sandbox.ContentCatalog=catalog;
        var origin=sandbox.Ground.transform.position;var size=sandbox.Ground.terrainData.size;
        var a=Asset<RegionDefinition>("region.start");a.Id="region.start";a.DisplayName="스타트 지역 A";a.ResourceId="verification.iron";a.Bounds=new Rect(origin.x,origin.z,size.x*.5f,size.z);
        var b=Asset<RegionDefinition>("region.adjacent");b.Id="region.adjacent";b.DisplayName="인접 지역 B";b.ResourceId="verification.copper";b.Bounds=new Rect(origin.x+size.x*.5f,origin.z,size.x*.5f,size.z);
        sandbox.Regions=new[]{a,b};
        foreach(var value in new UnityEngine.Object[]{catalog,a,b,sandbox})EditorUtility.SetDirty(value);
        var errors=catalog.Validate();if(errors.Count>0)throw new Exception(string.Join("\n",errors));
        EditorSceneManager.MarkSceneDirty(sandbox.gameObject.scene);EditorSceneManager.SaveScene(sandbox.gameObject.scene);AssetDatabase.SaveAssets();
        return "StartRegionSandbox: square base 80m / outpost 40m / external construction / fixed base ownership / camera-local grid / two region definitions. Original OpenWorldSandbox and resource catalog preserved.";
    }
}
