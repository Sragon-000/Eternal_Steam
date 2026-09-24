using System;
using System.IO;
using UnityEngine;
using EternalSteam.OpenWorld;
public static class CaptureTileWorldMap
{
    public static string Main()
    {
        var source=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>().CameraRig.View;
        var go=new GameObject("TWC overview capture");var camera=go.AddComponent<Camera>();camera.CopyFrom(source);
        camera.enabled=false;camera.orthographic=true;camera.orthographicSize=115;camera.aspect=1.6f;
        camera.transform.rotation=Quaternion.Euler(53,0,0);camera.transform.position=new Vector3(0,7,0)-camera.transform.forward*260;
        var rt=new RenderTexture(1600,1000,24){antiAliasing=4};var previous=RenderTexture.active;Texture2D image=null;
        try
        {
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();
            string path=Path.GetFullPath("Docs/Validation/twc-mountain-river-overview.png");File.WriteAllBytes(path,image.EncodeToPNG());return path;
        }
        finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);if(image!=null)UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);}
    }
}
