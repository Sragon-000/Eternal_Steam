using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using EternalSteam;
public static class ApplySimpleBuildingModels
{
 const string Root="Assets/EternalSteam/Content/Buildings/DocumentContent";
 static Material metal,dark,accent; static Transform parent;
 static Material Mat(string name,Color color){string p=Root+"/Model_"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,p);}m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",.45f);m.SetFloat("_Smoothness",.35f);EditorUtility.SetDirty(m);return m;}
 static GameObject Part(string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat,Vector3 angles=default){var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.transform.localEulerAngles=angles;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=mat;return g;}
 static void Box(string n,float x,float y,float z,float sx,float sy,float sz,Material m){Part(n,PrimitiveType.Cube,new Vector3(x,y,z),new Vector3(sx,sy,sz),m);}
 static void Cyl(string n,float x,float y,float z,float radius,float height,Material m){Part(n,PrimitiveType.Cylinder,new Vector3(x,y,z),new Vector3(radius*2,height*.5f,radius*2),m);}
 static void Ball(string n,float x,float y,float z,float size,Material m){Part(n,PrimitiveType.Sphere,new Vector3(x,y,z),Vector3.one*size,m);}
 static void Barrel(float x,float y,float length,float width,Material m){Part("Forward barrel",PrimitiveType.Cylinder,new Vector3(x,y,length*.36f),new Vector3(width,length*.5f,width),m,new Vector3(90,0,0));Box("Muzzle",x,y,length*.86f,width*1.6f,width*1.6f,.12f,dark);}
 static void Model(string id,int size,int index,bool resource)
 {
  var path=Root+"/"+id+".prefab";var g=PrefabUtility.LoadPrefabContents(path);
  try{
   for(int i=g.transform.childCount-1;i>=0;i--)UnityEngine.Object.DestroyImmediate(g.transform.GetChild(i).gameObject);
   foreach(var c in g.GetComponents<Collider>())UnityEngine.Object.DestroyImmediate(c);
   var visual=new GameObject("Simple model");visual.transform.SetParent(g.transform,false);parent=visual.transform;
   accent=AssetDatabase.LoadAssetAtPath<Material>(Root+"/"+id+".mat");
   Color[] colors={new Color(.6f,.72f,.78f),new Color(.88f,.43f,.19f),new Color(.36f,.7f,.88f),new Color(.57f,.5f,.4f),new Color(.48f,.86f,.17f),new Color(.7f,.27f,1),new Color(.15f,.9f,.78f)};
   if(resource){accent.SetColor("_BaseColor",colors[index]);EditorUtility.SetDirty(accent);}
   Box("Armored plinth",0,.12f,0,1.65f,.24f,1.65f,dark);Box("Deck",0,.27f,0,1.46f,.12f,1.46f,metal);
   if(resource){
    if(index==0){Box("Drill gantry",0,1,0,1.35f,.2f,.35f,metal);for(int s=-1;s<=1;s+=2)Box("Gantry leg",s*.57f,.65f,0,.18f,.9f,.3f,accent);Cyl("Drill",0,.65f,0,.22f,.65f,dark);for(int j=0;j<3;j++)Cyl("Drill collar",0,.43f+j*.18f,0,.29f,.08f,accent);}
    if(index==1){Cyl("Smelter",0,.75f,0,.48f,.85f,accent);Cyl("Rim",0,1.2f,0,.52f,.12f,dark);Cyl("Chimney",.55f,1.05f,-.45f,.14f,1.4f,metal);Box("Output tray",0,.4f,.6f,.7f,.14f,.5f,metal);}
    if(index==2){for(int s=-1;s<=1;s+=2){Cyl("Separator tank",s*.36f,.8f,0,.25f,.9f,metal);Cyl("Tank band",s*.36f,.85f,0,.28f,.22f,accent);}Box("Pipe bridge",0,1.32f,0,1,.12f,.18f,accent);}
    if(index==3){Box("Press frame",0,.9f,-.1f,1.1f,1.1f,.65f,dark);Box("Press plate",0,.55f,.4f,1,.16f,.6f,metal);Cyl("Piston",0,1,.35f,.17f,.6f,accent);Box("Upper press",0,1.35f,.15f,1.1f,.2f,1,metal);}
    if(index==4){Cyl("Reactor",0,.86f,0,.46f,1.05f,metal);for(int j=0;j<3;j++)Cyl("Containment band",0,.5f+j*.35f,0,.51f,.1f,accent);for(int s=-1;s<=1;s+=2)Box("Shield",s*.62f,.72f,0,.16f,.8f,.95f,dark);}
    if(index==5){Ball("Plasma core",0,.95f,0,.73f,accent);for(int s=-1;s<=1;s+=2)Box("Containment mast",s*.55f,.95f,0,.16f,1.25f,.3f,metal);Box("Containment bridge",0,1.6f,0,1.25f,.16f,.3f,dark);}
    if(index==6){Box("Fabricator",0,.75f,0,1.2f,.8f,1.1f,metal);Box("Core window",0,.8f,.56f,.8f,.4f,.04f,accent);for(int j=0;j<3;j++)Box("Cooling fin",-.4f+j*.4f,1.26f,0,.14f,.3f,.85f,dark);}
   }else{
    Cyl("Turret bearing",0,.47f,0,.55f,.32f,metal);
    if(index==0){Cyl("Coil mast",0,1,0,.18f,1.1f,metal);for(int j=0;j<4;j++)Cyl("Tesla coil",0,.7f+j*.23f,0,.37f,.1f,accent);Ball("Terminal",0,1.7f,0,.48f,metal);}
    if(index==1){Box("Laser housing",0,.85f,0,.7f,.55f,.8f,accent);Barrel(0,.88f,.85f,.3f,metal);Box("Lens",0,.88f,.8f,.25f,.25f,.08f,accent);}
    if(index==2){Box("AA receiver",0,.83f,-.14f,.85f,.5f,.65f,dark);for(int s=-1;s<=1;s+=2)for(int j=0;j<2;j++)Barrel(s*.2f,.8f+j*.19f,.82f,.1f,metal);Box("Ammunition",-.57f,.75f,-.15f,.25f,.5f,.5f,accent);}
    if(index==3||index==5){Box("Rail breech",0,.85f,-.22f,.65f,.5f,.6f,accent);for(int s=-1;s<=1;s+=2){Box("Parallel rail",s*.23f,.9f,.3f,.14f,.2f,1.05f,metal);Box("Rail energy",s*.23f,1.02f,.3f,.09f,.04f,.8f,accent);}if(index==3)Cyl("Tracking mast",-.6f,1.1f,-.45f,.07f,.75f,metal);}
    if(index==4||index==6){Box("Energy receiver",0,.83f,-.1f,.85f,.55f,.7f,metal);Barrel(0,.9f,.7f,.5f,dark);Ball("Emitter",0,.9f,.65f,.4f,accent);for(int s=-1;s<=1;s+=2)Cyl("Capacitor",s*.55f,.78f,-.1f,.13f,.6f,accent);if(index==6)Box("AA sensor",0,1.35f,-.3f,.85f,.12f,.22f,accent);}
    if(index==7){Box("Launcher rack",0,.8f,0,1.35f,.55f,1.1f,metal);for(int i=0;i<3;i++){float x=(i-1)*.4f;Box("Launch tube",x,1.08f,.05f,.3f,.3f,1.2f,dark);Ball("Missile nose",x,1.08f,.68f,.24f,accent);}}
    if(index==8){Cyl("Pulse column",0,.9f,0,.28f,.8f,metal);for(int j=0;j<3;j++)Cyl("Pulse disk",0,.67f+j*.33f,0,.62f-j*.12f,.1f,accent);Ball("Pulse crown",0,1.5f,0,.3f,accent);}
    Box("Forward marker",0,.35f,.72f,.35f,.035f,.14f,accent);
   }
   // Models stay inside the footprint; vertical proportions remain readable in top view.
   parent.localScale=new Vector3(size,Mathf.Sqrt(size),size);
   var bounds=new Bounds(new Vector3(0,.1f,0),Vector3.zero);foreach(var r in g.GetComponentsInChildren<Renderer>())bounds.Encapsulate(r.bounds);
   if(bounds.size.x>size*2 || bounds.size.z>size*2)throw new Exception(id+" exceeds footprint");
   var collider=g.AddComponent<BoxCollider>();collider.center=bounds.center;collider.size=bounds.size;
   PrefabUtility.SaveAsPrefabAsset(g,path);
  }finally{PrefabUtility.UnloadPrefabContents(g);}
 }
 public static string Main(){if(Application.isPlaying)throw new Exception("Exit Play before editing prefabs");metal=Mat("Steel",new Color(.38f,.47f,.51f));dark=Mat("Charcoal",new Color(.095f,.14f,.17f));string[] r={"iron","copper","titanium","tungsten","uranium","plasma_ore","nanometal"};string[] d={"tesla","plasma_laser","vulcan_aa","rail_aa","arc","railgun","sky_plasma","smart_missile","emp"};int[] sizes={1,1,1,1,2,2,2,3,3};for(int i=0;i<r.Length;i++)Model("resource."+r[i],1,i,true);for(int i=0;i<d.Length;i++)Model("defense."+d[i],sizes[i],i,false);AssetDatabase.SaveAssets();return "Updated 16 prefab models; footprint bounds checked; one root selection collider each. Gameplay definitions unchanged.";}
 public static string Preview(){var catalog=AssetDatabase.LoadAssetAtPath<BuildingCatalog>(Root+"/DocumentBuildings.asset");var sheet=new Texture2D(1024,1024,TextureFormat.RGB24,false);int i=0;foreach(var def in catalog.Buildings){if(def.Id=="installation.nexus")continue;var p=new PreviewRenderUtility();try{p.camera.backgroundColor=new Color(.065f,.085f,.11f);p.camera.clearFlags=CameraClearFlags.SolidColor;p.camera.fieldOfView=30;p.camera.nearClipPlane=.1f;p.camera.farClipPlane=100;p.lights[0].intensity=1.4f;p.lights[0].transform.rotation=Quaternion.Euler(45,35,0);p.lights[1].intensity=.7f;var g=UnityEngine.Object.Instantiate(def.ViewPrefab);p.AddSingleGO(g);float s=def.Footprint.x;var center=new Vector3(0,.75f*Mathf.Sqrt(s),0);p.camera.transform.position=center+new Vector3(3,3.2f,4)*s;p.camera.transform.LookAt(center);p.BeginStaticPreview(new Rect(0,0,256,256));p.Render(true);var t=p.EndStaticPreview();sheet.SetPixels((i%4)*256,(3-i/4)*256,256,256,t.GetPixels());UnityEngine.Object.DestroyImmediate(t);i++;}finally{p.Cleanup();}}sheet.Apply();Directory.CreateDirectory("Docs/Validation");File.WriteAllBytes("Docs/Validation/simple-building-models.png",sheet.EncodeToPNG());UnityEngine.Object.DestroyImmediate(sheet);return "Saved 4x4 model sheet: resources 7 then defenses 9, catalog order.";}
}
