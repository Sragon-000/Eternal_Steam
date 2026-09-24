using System;
using UnityEngine;
using UnityEditor;
using EternalSteam;
using EternalSteam.OpenWorld;
public static class VerifyNexusFootprint
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static string Main(){var d=AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/EternalSteam/Content/Buildings/DocumentContent/installation.nexus.asset");Check(d!=null&&d.Validate().Count==0,"Valid nexus");Check(d.Footprint==new Vector2Int(4,4)&&d.Placement.SnapCells==4,"16 cells with 8m snap");var grid=new BuildGrid(new RectInt(-100,-100,200,200),2,default,45);
 foreach(float x in new[]{-16.1f,-8.01f,-7.99f,-.01f,.01f,7.99f,8.01f,16.1f})foreach(float z in new[]{-8.01f,-.01f,.01f,8.01f}){var p=WorldGridGeometry.ToWorld(new Vector3(x,0,z));var cell=d.Placement.Snap(grid.WorldToCell(p));var center=grid.Center(cell,d.Footprint);var foundation=WorldGridGeometry.Center(FoundationPlacement.Key(p),8);Check((center-foundation).sqrMagnitude<.0001f,"Positive and negative lattice alignment");Check(d.Placement.IsAligned(cell)&&!d.Placement.IsAligned(cell+Vector2Int.one),"Off-grid anchor rejected");
 var neighbour=foundation+WorldGridGeometry.Rotation*Vector3.right*8;var nexusEdge=center+WorldGridGeometry.Rotation*Vector3.right*4;var foundationEdge=neighbour-WorldGridGeometry.Rotation*Vector3.right*4;Check((nexusEdge-foundationEdge).sqrMagnitude<.0001f,"Adjacent edges coincide");}
 var prefab=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(d.ViewPrefab));try{var bounds=prefab.GetComponentsInChildren<Renderer>()[0].bounds;foreach(var r in prefab.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);Check(Mathf.Abs(bounds.size.x-8)<.001f&&Mathf.Abs(bounds.size.z-8)<.001f,"Rendered model fits 8x8m");var c=prefab.GetComponent<BoxCollider>();Check(c.size.x==8&&c.size.z==8,"Selection collider fits footprint");Check(Mathf.Abs(prefab.transform.GetChild(0).localPosition.y+.2f+.05f-.35f)<.001f,"Base top matches flat foundation");}finally{PrefabUtility.UnloadPrefabContents(prefab);}return "PASS: nexus 4x4 cells, 8m snapping on positive/negative coordinates, shared 45-degree foundation grid, matching adjacent edges, 8x8m model/collider, flat-ground base top alignment.";}
}
