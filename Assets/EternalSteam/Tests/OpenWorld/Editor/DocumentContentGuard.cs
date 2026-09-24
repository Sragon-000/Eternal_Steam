using UnityEditor;
using UnityEngine;
namespace EternalSteam.OpenWorld.Editor
{
    [InitializeOnLoad]
    static class DocumentContentGuard
    {
        static DocumentContentGuard(){EditorApplication.playModeStateChanged+=Validate;}
        static void Validate(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.ExitingEditMode)return;
            foreach(var world in Object.FindObjectsByType<OpenWorldSandbox>(FindObjectsSortMode.None)){
                if(world.ContentCatalog==null)continue;
                var errors=world.ContentCatalog.Validate();if(errors.Count==0)continue;
                Debug.LogError("Content configuration: "+string.Join("\n",errors),world);EditorApplication.isPlaying=false;
            }
        }
    }
}
