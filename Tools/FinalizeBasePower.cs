using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class FinalizeBasePower
{
    public static string Main()
    {
        if(Application.isPlaying)throw new System.Exception("Edit mode required");
        var s=Object.FindFirstObjectByType<OpenWorldSandbox>();if(s.gameObject.scene.name!="StartRegionSandbox")throw new System.Exception("Original scene required");
        const string path="Assets/EternalSteam/Content/Buildings/BasePower/LegacyDefenseConsumer.asset";
        var legacy=AssetDatabase.LoadAssetAtPath<PowerModuleDefinition>(path);
        if(legacy==null){legacy=Object.Instantiate(s.LegacyPower);legacy.ExternalAttackAdapter=true;AssetDatabase.CreateAsset(legacy,path);}s.LegacyPower=legacy;
        EditorUtility.SetDirty(s.AssaultSettings);EditorUtility.SetDirty(s);Object.FindFirstObjectByType<SpawnAreaView>().SendMessage("LateUpdate");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(s.gameObject.scene);EditorSceneManager.SaveScene(s.gameObject.scene);return "Saved legacy power capability, footprint settings and authored scene markers";
    }
}
