using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TMPro;
using EternalSteam.OpenWorld;

public static class AuthorFirstLoopHud
{
    public static string Apply()
    {
        if(EditorApplication.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Clean Edit Mode scene required");
        var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        foreach(var name in new[]{"OpenWorldSandbox","StartRegionSandbox"})
        {
            var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");
            var h=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CanvasWorldHud>(true)).Single();
            if(h.Texts.Any(x=>x.Id=="first-loop-objective"))continue;
            var panel=new GameObject("FirstLoopObjective",typeof(RectTransform),typeof(UnityEngine.UI.Image));
            var rect=(RectTransform)panel.transform;rect.SetParent(h.Layout.Notifications,false);
            rect.anchorMin=new Vector2(0,1);rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(.5f,0);
            rect.anchoredPosition=new Vector2(0,12);rect.sizeDelta=new Vector2(0,64);
            var background=panel.GetComponent<UnityEngine.UI.Image>();background.color=new Color(.025f,.06f,.07f,.92f);background.raycastTarget=false;
            var label=UnityEngine.Object.Instantiate(h.Texts.Single(x=>x.Id=="stage").View,rect);
            label.name="NextAction";label.gameObject.SetActive(true);label.text="첫 회차 · 생산 → 운송 → 방어 → 오브 제작\n목표 · 수정 → 기지 범위 안에 철·석탄 생성기 → 확정";
            var textRect=label.rectTransform;textRect.anchorMin=Vector2.zero;textRect.anchorMax=Vector2.one;textRect.pivot=new Vector2(.5f,.5f);textRect.anchoredPosition=Vector2.zero;textRect.sizeDelta=new Vector2(-24,-8);
            label.fontSize=18;label.alignment=TextAlignmentOptions.MidlineLeft;label.color=new Color(.95f,.86f,.6f);label.raycastTarget=false;
            h.Texts=h.Texts.Concat(new[]{new CanvasWorldHud.TextBinding{Id="first-loop-objective",View=label}}).ToArray();
            h.Sections=h.Sections.Concat(new[]{new CanvasWorldHud.Section{Id="first-loop",Body=panel}}).ToArray();
            h.Layout.Texts=h.Layout.Texts.Concat(new[]{(TMP_Text)label}).ToArray();h.Layout.FontSizes=h.Layout.FontSizes.Concat(new[]{18f}).ToArray();
            EditorSceneManager.MarkSceneDirty(scene);if(!EditorSceneManager.SaveScene(scene))throw new Exception("Save failed: "+name);
        }
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=original)EditorSceneManager.OpenScene(original);
        return "PASS first-loop objective authored and saved in both Canvas scenes, without runtime construction or pointer interception";
    }
}
