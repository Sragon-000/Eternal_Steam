using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using EternalSteam.OpenWorld;
using TMPro;
using Newtonsoft.Json.Linq;
public static class AuthorRailwaySprites
{
 const string Root="Assets/EternalSteam/Shared/UI/Railway/";
 static Sprite Import(string name,bool frame){
  string path=Root+name+".png";AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
  var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Multiple;importer.spritePixelsPerUnit=600;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=4096;importer.filterMode=FilterMode.Bilinear;importer.SaveAndReimport();
  var factories=new SpriteDataProviderFactories();factories.Init();var provider=factories.GetSpriteEditorDataProviderFromObject(importer);if(provider==null)throw new Exception("No sprite provider");provider.InitSpriteEditorDataProvider();
  var edit=provider.GetDataProvider<ISpriteFrameEditCapability>();if(edit==null)throw new Exception("No sprite capability provider");var caps=edit.GetEditCapability();
  foreach(var cap in new[]{EEditCapability.CreateAndDeleteSprite,EEditCapability.EditSpriteRect,EEditCapability.EditSpriteName,EEditCapability.EditBorder,EEditCapability.EditPivot})if(!caps.HasCapability(cap))throw new Exception("Unsupported sprite operation "+cap);
  var source=new Texture2D(2,2,TextureFormat.RGBA32,false);try{
   source.LoadImage(File.ReadAllBytes(path));var pixels=source.GetPixels32();int minX=source.width,minY=source.height,maxX=0,maxY=0;
   for(int y=0;y<source.height;y++)for(int x=0;x<source.width;x++)if(pixels[y*source.width+x].a>=200){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
   if(minX>=maxX||minY>=maxY)throw new Exception("Empty alpha");
   var old=provider.GetSpriteRects().FirstOrDefault();var rect=new SpriteRect{name=name,spriteID=old?.spriteID??GUID.Generate(),rect=new Rect(minX,minY,maxX-minX+1,maxY-minY+1),alignment=SpriteAlignment.Center,pivot=new Vector2(.5f,.5f),border=frame?new Vector4(name=="RailwayPanel"?65:40,name=="RailwayPanel"?65:40,name=="RailwayPanel"?65:40,name=="RailwayPanel"?65:40):Vector4.zero};
   var names=provider.GetDataProvider<ISpriteNameFileIdDataProvider>();if(names==null)throw new Exception("Missing sprite identity provider");
   provider.SetSpriteRects(new[]{rect});names.SetNameFileIdPairs(new[]{new SpriteNameFileIdPair(rect.name,rect.spriteID)});provider.Apply();importer.SaveAndReimport();
  }finally{UnityEngine.Object.DestroyImmediate(source);}
  return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
 }
 static void Frame(UnityEngine.UI.Image image,Sprite sprite){image.sprite=sprite;image.type=UnityEngine.UI.Image.Type.Sliced;image.color=Color.white;image.pixelsPerUnitMultiplier=1;}
 public static string Main(){
  if(EditorApplication.isPlaying)throw new Exception("Edit Mode required");var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(current.isDirty)throw new Exception("Unsaved scene");string original=current.path;
  var panel=Import("RailwayPanel",true);var button=Import("RailwayButton",true);var icon=Import("RailwayIcon",false);var report=new JArray();
  try{foreach(string name in new[]{"StartRegionSandbox","OpenWorldSandbox"}){
   var scene=EditorSceneManager.OpenScene("Assets/EternalSteam/Scene/Tests/"+name+".unity");var h=UnityEngine.Object.FindFirstObjectByType<RailwayHud>();
   foreach(var body in new[]{h.Panel,h.StationContextPanel,h.DraftStopsPanel})Frame(body.GetComponent<UnityEngine.UI.Image>(),panel);
   foreach(var binding in h.Commands){var b=binding.Button;Frame(b.GetComponent<UnityEngine.UI.Image>(),button);b.transition=UnityEngine.UI.Selectable.Transition.ColorTint;b.targetGraphic=b.GetComponent<UnityEngine.UI.Image>();
    b.colors=new UnityEngine.UI.ColorBlock{normalColor=Color.white,highlightedColor=new Color(1.25f,1.22f,1.06f),pressedColor=new Color(.74f,.84f,.84f),selectedColor=new Color(1.1f,1.12f,.98f),disabledColor=new Color(.48f,.52f,.52f,.72f),colorMultiplier=1,fadeDuration=.09f};
    var label=b.GetComponentInChildren<TMP_Text>();label.color=new Color(.85f,.9f,.89f);label.fontSize=14;label.enableAutoSizing=true;label.fontSizeMin=12;label.fontSizeMax=14;
    if(binding.Command=="open")label.text="철도 관리";
   }
   h.Title.color=new Color(.91f,.75f,.4f);h.Title.rectTransform.anchoredPosition=new Vector2(48,-8);h.Title.rectTransform.sizeDelta=new Vector2(382,28);
   var oldIcon=h.Panel.transform.Find("RailwayTitleIcon");var image=oldIcon==null?new GameObject("RailwayTitleIcon",typeof(RectTransform),typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>():oldIcon.GetComponent<UnityEngine.UI.Image>();image.transform.SetParent(h.Panel.transform,false);image.sprite=icon;image.color=Color.white;image.raycastTarget=false;image.preserveAspect=true;var rt=image.rectTransform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(14,-7);rt.sizeDelta=new Vector2(26,30);
   foreach(var text in new[]{h.Summary,h.Feedback,h.DraftSelection,h.StationContextHeader,h.SelectedSegment})if(text!=null)text.color=new Color(.8f,.86f,.85f);
   var hud=UnityEngine.Object.FindFirstObjectByType<CanvasWorldHud>();hud.ShowDevelopmentControls=false;foreach(var section in hud.Sections)if(section.Id is "test-shortcuts" or "developer")section.Body.SetActive(false);
   h.ConnectionPanel.SetActive(false);h.Panel.SetActive(false);EditorUtility.SetDirty(h);EditorUtility.SetDirty(hud);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   report.Add(new JObject{{"scene",name},{"spriteButtons",h.Commands.Length},{"debugControlsVisible",false}});
  }}finally{EditorSceneManager.OpenScene(original);}
  File.WriteAllText("Docs/Measurements/2026-10-02-railway-connections/sprite-authoring.json",report.ToString());return report.ToString();
 }
}
