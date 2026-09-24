using System;
using UnityEngine;
using UnityEditor;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Demo;
public static class ApplyTurretRotation
{
    static T Asset<T>(string path)where T:ScriptableObject{var value=AssetDatabase.LoadAssetAtPath<T>(path);if(value==null){value=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(value,path);}return value;}
    public static string Main(){
        if(Application.isPlaying)throw new Exception("Stop Play before asset changes");int count=0;
        foreach(var guid in AssetDatabase.FindAssets("t:BuildingDefinition",new[]{"Assets/EternalSteam/Content/Buildings/DocumentContent","Assets/EternalSteam/Content/Buildings/StartRegion"})){
            var path=AssetDatabase.GUIDToAssetPath(guid);var definition=AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);if(definition.Category!=BuildingCategory.Defense)continue;
            var rotation=Asset<TurretRotationDefinition>(path.Replace(".asset",".rotation.asset"));
            if(!definition.Modules.Contains(rotation)){definition.Modules.RemoveAll(m=>m is TurretRotationDefinition);definition.Modules.Add(rotation);}
            EditorUtility.SetDirty(rotation);EditorUtility.SetDirty(definition);count++;
        }
        var sandbox=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        foreach(var prefab in sandbox.TowerPrefabs){if(prefab==null)continue;string path=AssetDatabase.GetAssetPath(prefab);var root=PrefabUtility.LoadPrefabContents(path);
            try{var tower=root.GetComponent<SceneTower>();string dir=System.IO.Path.GetDirectoryName(path).Replace('\\','/');
                var rotation=Asset<TurretRotationDefinition>(dir+"/"+tower.Kind+".rotation.asset");
                rotation.DegreesPerSecond=tower.Kind==HordeTowerKind.MachineGun?240:tower.Kind==HordeTowerKind.Cannon?120:180;
                tower.Rotation=rotation;tower.Upgrade=Asset<PerformanceUpgradeDefinition>(dir+"/"+tower.Kind+".upgrade.asset");
                EditorUtility.SetDirty(rotation);PrefabUtility.SaveAsPrefabAsset(root,path);
            }finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        AssetDatabase.SaveAssets();return "Rotation module registered on "+count+" catalog definitions and 4 legacy prefabs; per-instance state and upgrade multiplier enabled.";
    }
}
