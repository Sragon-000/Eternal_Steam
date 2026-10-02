using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using EternalSteam.OpenWorld;

// Applies only the observed title overflow to existing authored Canvas controls.
public static class PolishPointerQaHud
{
    public static string Apply()
    {
        if(EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new Exception("Clean Edit Mode required");
        var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        foreach(var name in new[]{"OpenWorldSandbox","StartRegionSandbox"}){
            var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");
            var h=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CanvasWorldHud>(true)).Single();
            var title=h.Texts.Single(v=>v.Id=="selection-title").View;
            Undo.RecordObject(title,"Fit selected building title");
            title.enableAutoSizing=true;title.fontSizeMin=14;title.fontSizeMax=21;
            title.margin=Vector4.zero;title.alignment=TextAlignmentOptions.TopLeft;
            EditorUtility.SetDirty(title);
            var objective=h.Texts.Single(v=>v.Id=="first-loop-objective").View;
            objective.text="첫 회차 · 생산 → 운송 → 방어 → 오브 제작\n목표 · 수정 → 파란 가동 영역 안에 철·석탄 생성기 → 확정";
            EditorUtility.SetDirty(objective);
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed: "+name);
        }
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=original)EditorSceneManager.OpenScene(original);
        return "PASS existing title auto-fit and initial guide saved in both scenes";
    }
}
