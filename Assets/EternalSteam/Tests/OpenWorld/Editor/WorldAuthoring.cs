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
            if(!WorldGridGeometry.TerrainPlacement(world.Ground,point,out var center,out reason))return false;
            foreach(var f in world.GetComponentsInChildren<SceneFoundation>())if(WorldGridGeometry.Overlaps(center,f.transform)){reason="다른 토대와 겹칩니다.";return false;}
            var go=(GameObject)PrefabUtility.InstantiatePrefab(world.FoundationPrefab,world.transform);
            go.transform.SetPositionAndRotation(center-Vector3.up*.2f,WorldGridGeometry.Rotation);go.name="Foundation "+key;
            Undo.RegisterCreatedObjectUndo(go,"Place foundation");EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            reason="토대를 씬에 배치했습니다. Ctrl/Cmd+S로 저장하세요.";return true;
        }
        public static bool Tower(OpenWorldSandbox world,Vector3 point,Vector3 direction,HordeTowerKind kind,out string reason)
        {
            if(Application.isPlaying){reason="Play를 종료하고 편집하세요.";return false;}
            SceneFoundation foundation=null;BuildGrid grid=null;Vector2Int cell=default;
            foreach(var f in world.GetComponentsInChildren<SceneFoundation>()) {
                var candidate=WorldGridGeometry.Grid(f.transform,f.Top);var c=candidate.WorldToCell(point);
                if(candidate.Bounds.Contains(c)){foundation=f;grid=candidate;cell=c;break;}
            }
            if(foundation==null){reason="토대 위를 선택하세요.";return false;}
            foreach(var t in world.GetComponentsInChildren<SceneTower>())if(grid.WorldToCell(t.transform.position)==cell){reason="이미 포탑이 있습니다.";return false;}
            direction.y=0;if(!float.IsFinite(direction.sqrMagnitude)||direction.sqrMagnitude<.01f){reason="방향을 지정하세요.";return false;}
            int index=(int)kind;if(index<0 || index>=world.TowerPrefabs.Length){reason="포탑 프리팹이 없습니다.";return false;}
            var go=(GameObject)PrefabUtility.InstantiatePrefab(world.TowerPrefabs[index],world.transform);
            go.transform.position=grid.Center(cell,Vector2Int.one)+Vector3.up*.01f;go.transform.rotation=Quaternion.LookRotation(direction);
            Undo.RegisterCreatedObjectUndo(go,"Place tower");EditorSceneManager.MarkSceneDirty(world.gameObject.scene);reason="포탑을 씬에 배치했습니다.";return true;
        }
        public static bool Recover(OpenWorldSandbox world,Vector3 point,out string reason)
        {
            if(Application.isPlaying){reason="Play를 종료하고 편집하세요.";return false;}
            foreach(var f in world.GetComponentsInChildren<SceneFoundation>()) {
            var grid=WorldGridGeometry.Grid(f.transform,f.Top);var cell=grid.WorldToCell(point);if(!grid.Bounds.Contains(cell))continue;
            foreach(var t in world.GetComponentsInChildren<SceneTower>())if(grid.WorldToCell(t.transform.position)==cell) {
                Undo.DestroyObjectImmediate(t.gameObject);EditorSceneManager.MarkSceneDirty(world.gameObject.scene);reason="회수했습니다. Undo로 복구할 수 있습니다.";return true;
            }
            }
            reason="포탑이 있는 칸을 선택하세요.";return false;
        }
    }
}
