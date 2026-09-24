using System.Linq;
using UnityEngine;
using UnityEditor;
using EternalSteam.OpenWorld;
public static class InspectStartLoop{
 public static object Main(){var s=Object.FindFirstObjectByType<OpenWorldSandbox>();var scene=s.gameObject.scene;var tiles=s.Ground.GetComponent<TileWorldGround>();return new{scene=scene.path,dirty=scene.isDirty,play=Application.isPlaying,root=tiles.GridRoot.name,position=tiles.GridRoot.position.ToString(),rotation=tiles.GridRoot.eulerAngles.ToString(),ground=s.Ground.transform.position.ToString(),size=s.Ground.terrainData.size.ToString(),focus=s.CameraRig.Focus.ToString(),roots=scene.GetRootGameObjects().Select(o=>o.name).ToArray(),named=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.ToLower().Contains("main")||t.name.ToLower().Contains("nexus")||t.name.Contains("기지")||t.name.ToLower().Contains("start")).Select(t=>new{name=t.name,position=t.position.ToString(),rotation=t.eulerAngles.ToString(),active=t.gameObject.activeInHierarchy}).ToArray()};}
}
