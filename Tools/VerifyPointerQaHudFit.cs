using System;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;

// Separate HUD rendering fixture. This is command verification, not OS pointer evidence.
public static class VerifyPointerQaHudFit
{
    public static string Prepare()
    {
        if(!Application.isPlaying||Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")!="/tmp/eternal-qa06-hud-20261001")throw new Exception("Isolated HUD fixture required");
        var s=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        var main=s.Content.MainBase;
        while(main.Module<IUpgradeControl>().Level<3)if(!s.Persistence.Upgrades.TryUpgrade(main,out var reason))throw new Exception(reason);
        var input=s.GetComponent<OpenWorldInput>();input.BeginEditing();
        var definition=s.ContentCatalog.Buildings.Single(v=>v.Id=="defense.plasma_laser");
        var point=s.Content.GroundWorld.Grid.Center(new Vector2Int(1,20),Vector2Int.one);
        point.y=s.Ground.SampleHeight(point)+s.Ground.transform.position.y;
        if(!input.Edits.AddContent(definition,point,Vector3.forward,out var why)||!input.Confirm())throw new Exception(why??s.Message);
        input.ClickWorld(s.Content.GroundWorld.Buildings.Last().Position);
        return "HUD title fixture prepared using normal edit/upgrade commands";
    }
    public static string Check()
    {
        var h=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();
        var t=h.Texts.Single(v=>v.Id=="selection-title").View;
        h.Layout.Apply(true);t.ForceMeshUpdate();
        if(!t.gameObject.activeInHierarchy||!t.text.Contains("플라즈마 레이저 포탑")||!t.text.Contains("Lv.1 / 10")||!t.enableAutoSizing||t.isTextOverflowing)throw new Exception("Title hidden, clipped or incomplete");
        var bounds=t.textBounds;var rect=t.rectTransform.rect;
        if(bounds.size.y>rect.height+.1f||bounds.size.x>rect.width+.1f)throw new Exception("Title outside authored bounds");
        // Screen dimensions in Editor eval can describe the Editor window; use the game camera.
        var camera=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>().CameraRig.View;
        return "PASS "+camera.pixelWidth+"x"+camera.pixelHeight+" full selected title and level, font="+t.fontSize+", lines="+t.textInfo.lineCount+", glyphBounds="+bounds.size+", rect="+rect.size;
    }
    public static string Capture()
    {
        var type=typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var view=UnityEngine.Resources.FindObjectsOfTypeAll(type).Single();
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var texture=(RenderTexture)type.GetField("m_RenderTexture",flags).GetValue(view);
        var previous=RenderTexture.active;
        var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
        try {
            RenderTexture.active=texture;
            pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);pixels.Apply();
            // Metal's Editor render target is vertically inverted relative to PNG rows.
            if(SystemInfo.graphicsUVStartsAtTop) {
                var source=pixels.GetPixels();var upright=new Color[source.Length];
                for(int y=0;y<texture.height;y++)Array.Copy(source,y*texture.width,upright,(texture.height-1-y)*texture.width,texture.width);
                pixels.SetPixels(upright);pixels.Apply();
            }
            var path="Docs/Validation/2026-10-01-qa06-title-"+texture.width+".png";
            System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());
            return path+" (actual GameView render texture "+texture.width+"x"+texture.height+")";
        } finally {RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(pixels);}
    }
}
