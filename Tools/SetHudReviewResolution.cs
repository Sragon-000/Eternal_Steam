using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
public static class SetHudReviewResolution
{
 public static string Main(int width=1920,int height=1080)
 {
  var assembly=typeof(EditorWindow).Assembly;var sizesType=assembly.GetType("UnityEditor.GameViewSizes");var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);var sizes=singleton.GetProperty("instance").GetValue(null);
  var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{0});var gt=group.GetType();int count=(int)gt.GetMethod("GetTotalCount").Invoke(group,null),index=-1;
  for(int i=0;i<count;i++){var size=gt.GetMethod("GetGameViewSize").Invoke(group,new object[]{i});var t=size.GetType();if((int)t.GetProperty("width").GetValue(size)==width&&(int)t.GetProperty("height").GetValue(size)==height){index=i;break;}}
  if(index<0){var type=assembly.GetType("UnityEditor.GameViewSize");var kind=assembly.GetType("UnityEditor.GameViewSizeType");var size=Activator.CreateInstance(type,new object[]{Enum.ToObject(kind,1),width,height,"HUD review "+width+"x"+height});gt.GetMethod("AddCustomSize").Invoke(group,new[]{size});index=count;}
  var viewType=assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(viewType);if(!SessionState.GetBool("HudReview.HasOriginal",false)){SessionState.SetInt("HudReview.SizeIndex",(int)viewType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(view));SessionState.SetBool("HudReview.HasOriginal",true);}
  viewType.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,index);view.Show();view.Focus();view.Repaint();return width+"x"+height;
 }
}
