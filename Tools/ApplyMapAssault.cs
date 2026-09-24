using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam.OpenWorld;
public static class ApplyMapAssault
{
    public static void Main()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before authoring");
        var sandbox=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        if(sandbox==null||sandbox.gameObject.scene.name!="StartRegionSandbox")throw new Exception("Expected original StartRegionSandbox");
        const string path="Assets/EternalSteam/Content/Buildings/BasePlanning/StartRegionAssault.asset";
        var settings=AssetDatabase.LoadAssetAtPath<MapAssaultSettings>(path);
        if(settings==null){settings=ScriptableObject.CreateInstance<MapAssaultSettings>();
            var tile=sandbox.Ground.GetComponent<TileWorldGround>();var points=new List<Vector3>();
            for(int z=0;z<tile.Height;z++)for(int x=0;x<tile.Width;x++){var p=tile.GridRoot.TransformPoint(new Vector3(x*tile.CellSize,0,z*tile.CellSize));if(tile.IsPlayable(p))points.Add(p);}
            if(points.Count<8)throw new Exception("Insufficient ground");
            // Authored verification positions across the available map; editable in Inspector.
            for(int i=0;i<8;i++)settings.BossWavePoints[i]=points[(i*points.Count)/8];
            settings.BossPosition=points[points.Count-1];AssetDatabase.CreateAsset(settings,path);
        }
        Undo.RecordObject(sandbox,"Configure map assault");sandbox.AssaultSettings=settings;EditorUtility.SetDirty(sandbox);
        EditorSceneManager.MarkSceneDirty(sandbox.gameObject.scene);EditorSceneManager.SaveScene(sandbox.gameObject.scene);AssetDatabase.SaveAssets();
        Debug.Log("Map assault settings assigned to original StartRegionSandbox");
    }
}
