using System;using System.Linq;using UnityEngine;
public static class CaptureIntegratedHud{
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
            var path="Docs/Measurements/2026-10-02-train-hud-integration/hud-live-"+texture.width+".png";
            System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());
            return path+" (actual GameView render texture "+texture.width+"x"+texture.height+")";
        } finally {RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(pixels);}
    }
}
