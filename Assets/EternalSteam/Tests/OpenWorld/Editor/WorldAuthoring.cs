using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using EternalSteam.Demo;
namespace EternalSteam.OpenWorld.Editor
{
    // All edits create actual prefab instances in the scene and participate in Undo.
    public static class WorldAuthoring
    {
        public static bool Foundation(OpenWorldSandbox world,Vector3 point,out string reason)
        {
            if(Application.isPlaying){reason="Play를 종료하고 편집하세요.";return false;}
            var key=FoundationPlacement.Key(point);
            foreach(var p in world.GetComponentsInChildren<SceneFoundation>())if(p.Key==key){reason="이미 토대가 있습니다.";return false;}
            Vector3 center=new((key.x+.5f)*8,0,(key.y+.5f)*8);var ground=world.Ground;var origin=ground.transform.position;var size=ground.terrainData.size;
            if(center.x-4<origin.x || center.z-4<origin.z || center.x+4>origin.x+size.x || center.z+4>origin.z+size.z){reason="지형 밖입니다.";return false;}
            float low=float.MaxValue,high=float.MinValue;
            for(int z=0;z<=4;z++)for(int x=0;x<=4;x++) {float h=ground.SampleHeight(center+new Vector3(x*2-4,0,z*2-4))+origin.y;low=Mathf.Min(low,h);high=Mathf.Max(high,h);}
            if(high-low>1.2f){reason="경사가 너무 큽니다.";return false;}
            var go=(GameObject)PrefabUtility.InstantiatePrefab(world.FoundationPrefab,world.transform);
            go.transform.position=new Vector3(center.x,high+.15f,center.z);go.name="Foundation "+key;
            Undo.RegisterCreatedObjectUndo(go,"Place foundation");EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            reason="토대를 씬에 배치했습니다. Ctrl/Cmd+S로 저장하세요.";return true;
        }
        public static bool Tower(OpenWorldSandbox world,Vector3 point,Vector3 direction,HordeTowerKind kind,out string reason)
        {
            if(Application.isPlaying){reason="Play를 종료하고 편집하세요.";return false;}
            SceneFoundation foundation=null;var key=FoundationPlacement.Key(point);
            foreach(var f in world.GetComponentsInChildren<SceneFoundation>())if(f.Key==key)foundation=f;
            if(foundation==null){reason="토대 위를 선택하세요.";return false;}
            var cell=new Vector2Int(Mathf.FloorToInt(point.x/2),Mathf.FloorToInt(point.z/2));
            foreach(var t in world.GetComponentsInChildren<SceneTower>())if(new Vector2Int(Mathf.FloorToInt(t.transform.position.x/2),Mathf.FloorToInt(t.transform.position.z/2))==cell){reason="이미 포탑이 있습니다.";return false;}
            direction.y=0;if(!float.IsFinite(direction.sqrMagnitude)||direction.sqrMagnitude<.01f){reason="방향을 지정하세요.";return false;}
            int index=(int)kind;if(index<0 || index>=world.TowerPrefabs.Length){reason="포탑 프리팹이 없습니다.";return false;}
            var go=(GameObject)PrefabUtility.InstantiatePrefab(world.TowerPrefabs[index],world.transform);
            go.transform.position=new Vector3(cell.x*2+1,foundation.Top,cell.y*2+1);go.transform.rotation=Quaternion.LookRotation(direction);
            Undo.RegisterCreatedObjectUndo(go,"Place tower");EditorSceneManager.MarkSceneDirty(world.gameObject.scene);reason="포탑을 씬에 배치했습니다.";return true;
        }
        public static bool Recover(OpenWorldSandbox world,Vector3 point,out string reason)
        {
            if(Application.isPlaying){reason="Play를 종료하고 편집하세요.";return false;}
            var cell=new Vector2Int(Mathf.FloorToInt(point.x/2),Mathf.FloorToInt(point.z/2));
            foreach(var t in world.GetComponentsInChildren<SceneTower>())if(new Vector2Int(Mathf.FloorToInt(t.transform.position.x/2),Mathf.FloorToInt(t.transform.position.z/2))==cell) {
                Undo.DestroyObjectImmediate(t.gameObject);EditorSceneManager.MarkSceneDirty(world.gameObject.scene);reason="회수했습니다. Undo로 복구할 수 있습니다.";return true;
            }
            reason="포탑이 있는 칸을 선택하세요.";return false;
        }
    }
}
