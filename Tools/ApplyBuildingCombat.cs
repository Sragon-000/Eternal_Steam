using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class ApplyBuildingCombat
{
    const string Root="Assets/EternalSteam/Content/Buildings/StartRegion/";
    static T Asset<T>(string path)where T:ScriptableObject{var value=AssetDatabase.LoadAssetAtPath<T>(path);if(value==null){value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);}return value;}
    static void Configure(BuildingDefinition d,string path,BuildingCombatRole role,float maximum){
        var health=Asset<HealthModuleDefinition>(path.Replace(".asset",".health.asset"));health.Maximum=maximum;
        var body=Asset<BuildingCombatDefinition>(path.Replace(".asset",".combat.asset"));body.Role=role;body.BlocksGround=true;
        d.Modules.RemoveAll(m=>m is HealthModuleDefinition||m is BuildingCombatDefinition);d.Modules.Add(health);d.Modules.Add(body);
        if(role==BuildingCombatRole.Nexus&&!d.Provides<IBaseIdentity>())d.Modules.Add(Asset<BaseModuleDefinition>(path.Replace(".asset",".identity.asset")));
        foreach(var value in new UnityEngine.Object[]{d,health,body})EditorUtility.SetDirty(value);
    }
    public static string Main(){
        if(Application.isPlaying)throw new Exception("Stop Play first");var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(current.isDirty)throw new Exception("Preserve unsaved scene edits first");var original=current.path;
        foreach(var guid in AssetDatabase.FindAssets("t:BuildingDefinition",new[]{"Assets/EternalSteam/Content/Buildings/DocumentContent","Assets/EternalSteam/Content/Buildings/StartRegion"})){
            var path=AssetDatabase.GUIDToAssetPath(guid);var d=AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
            var role=d.Id=="installation.nexus"?BuildingCombatRole.Nexus:d.Id=="installation.wall"?BuildingCombatRole.Wall:d.Category==BuildingCategory.Defense?BuildingCombatRole.Defense:BuildingCombatRole.General;
            Configure(d,path,role,role==BuildingCombatRole.Nexus?1000:role==BuildingCombatRole.General?50:100);
        }
        var wall=Asset<BuildingDefinition>(Root+"installation.wall.asset");wall.Id="installation.wall";wall.DisplayName="방벽";wall.Category=BuildingCategory.Installation;wall.Footprint=Vector2Int.one;wall.Recoverable=true;
        var profile=Asset<BuildingPlacementDefinition>(Root+"installation.wall.placement.asset");profile.Surface=BuildingSurface.GroundOrFoundation;profile.RequiresBuildArea=false;profile.RequiresOperationalArea=false;profile.RequiresOwnerBase=false;profile.VerificationSettings=true;wall.Placement=profile;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Wall.prefab");
        if(prefab==null){var root=new GameObject("Wall");var model=GameObject.CreatePrimitive(PrimitiveType.Cube);model.name="Wall block";model.transform.SetParent(root.transform,false);model.transform.localPosition=Vector3.up;model.transform.localScale=new Vector3(2,2,2);model.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/EternalSteam/Content/Buildings/DocumentContent/Model_Steel.mat");prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"Wall.prefab");UnityEngine.Object.DestroyImmediate(root);}
        wall.ViewPrefab=prefab;Configure(wall,Root+"installation.wall.asset",BuildingCombatRole.Wall,100);EditorUtility.SetDirty(profile);
        foreach(var catalogPath in new[]{Root+"StartRegionBuildings.asset","Assets/EternalSteam/Content/Buildings/DocumentContent/DocumentBuildings.asset"}){var catalog=AssetDatabase.LoadAssetAtPath<BuildingCatalog>(catalogPath);if(!catalog.Buildings.Contains(wall))catalog.Buildings.Add(wall);var errors=catalog.Validate();if(errors.Count>0)throw new Exception(string.Join("\n",errors));EditorUtility.SetDirty(catalog);}
        var settings=Asset<EnemyCombatSettings>(Root+"EnemyCombat.asset");settings.AttackInterval=1;settings.AttackReach=1;settings.SearchRadius=10;settings.SearchInterval=.25f;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
        foreach(var scene in new[]{"Assets/EternalSteam/Scene/Tests/StartRegionSandbox.unity","Assets/EternalSteam/Scene/Tests/OpenWorldSandbox.unity"}){
            EditorSceneManager.OpenScene(scene);var sandbox=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();sandbox.EnemyCombat=AssetDatabase.LoadAssetAtPath<EnemyCombatSettings>(Root+"EnemyCombat.asset");
            foreach(var p in sandbox.TowerPrefabs){if(p==null)continue;string path=AssetDatabase.GetAssetPath(p);var root=PrefabUtility.LoadPrefabContents(path);
                try{var tower=root.GetComponent<SceneTower>();string dir=System.IO.Path.GetDirectoryName(path).Replace('\\','/');tower.Health=Asset<HealthModuleDefinition>(dir+"/"+tower.Kind+".health.asset");tower.Health.Maximum=100;
                    tower.CombatBody=Asset<BuildingCombatDefinition>(dir+"/"+tower.Kind+".combat.asset");tower.CombatBody.Role=BuildingCombatRole.Defense;tower.CombatBody.BlocksGround=true;EditorUtility.SetDirty(tower.Health);EditorUtility.SetDirty(tower.CombatBody);PrefabUtility.SaveAsPrefabAsset(root,path);
                }finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            EditorUtility.SetDirty(sandbox);EditorSceneManager.MarkSceneDirty(sandbox.gameObject.scene);EditorSceneManager.SaveScene(sandbox.gameObject.scene);AssetDatabase.SaveAssets();
        }
        if(!string.IsNullOrEmpty(original))EditorSceneManager.OpenScene(original);
        return "Applied wall and explicit health/combat roles to both catalogs, 4 legacy prefabs and both open-world scenes. Damage 1; interval 1s; HP wall/defense 100, general 50, nexus 1000.";
    }
}
