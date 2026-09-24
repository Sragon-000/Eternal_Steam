using System;
using UnityEngine;
using EternalSteam.OpenWorld;
public static class VerifyTraversalSnapshot
{
 public static string Main(){
  var go=new GameObject("Traversal snapshot test");try{
   var terrain=go.AddComponent<TileWorldGround>();terrain.GridRoot=go.transform;terrain.Width=32;terrain.Height=32;terrain.CellSize=2;terrain.PlayableCells=new byte[1024];var random=new System.Random(731);
   for(int i=0;i<1024;i++)terrain.PlayableCells[i]=(byte)(random.NextDouble()>.2?1:0);
   go.transform.SetPositionAndRotation(new Vector3(13,4,-17),Quaternion.Euler(0,45,0));go.transform.localScale=new Vector3(1.3f,1,1.3f);
   var snapshot=terrain.CaptureTraversal();for(int i=0;i<10000;i++){
    var a=go.transform.TransformPoint(new Vector3((float)random.NextDouble()*68-2,0,(float)random.NextDouble()*68-2));var b=a+new Vector3((float)random.NextDouble()*8-4,0,(float)random.NextDouble()*8-4);
    if(snapshot.HasClearRoute(a,b)!=terrain.HasClearRoute(a,b))throw new Exception("Snapshot route differs at sample "+i);
   }
   terrain.PlayableCells=null;if(terrain.CaptureTraversal().HasClearRoute(Vector3.zero,Vector3.one))throw new Exception("Missing cells must block");
   return "PASS 10,000 fixed-seed route comparisons, rotated/scaled grid, blocked boundaries, missing terrain data.";
  }finally{UnityEngine.Object.DestroyImmediate(go);}
 }
}
