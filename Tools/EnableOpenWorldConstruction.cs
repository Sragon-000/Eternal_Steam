using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam.OpenWorld;
public static class EnableOpenWorldConstruction
{
    public static string Main()
    {
        if(Application.isPlaying)throw new Exception("Edit mode required");
        var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/OpenWorldSandbox.unity");
        var world=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        if(world.FoundationPrefab==null || world.TowerPrefabs.Length!=4)throw new Exception("Prepared prefabs required");
        world.GetComponent<OpenWorldInput>().enabled=true;
        var document=UnityEngine.Object.FindFirstObjectByType<OpenWorldHud>().GetComponent<UIDocument>();
        document.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/EternalSteam/Shared/UI/OpenWorld/OpenWorldHud.uxml");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "In-game foundation/tower/recovery UI enabled; existing terrain and prefab references preserved.";
    }
}
