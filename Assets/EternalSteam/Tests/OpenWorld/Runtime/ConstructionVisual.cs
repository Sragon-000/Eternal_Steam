using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace EternalSteam.OpenWorld
{
    // Owned temporary materials; never changes a shared prefab/material asset.
    public sealed class ConstructionVisual : IDisposable
    {
        readonly List<(Renderer Renderer,Material[] Original)> originals=new();
        readonly List<Material> materials=new();
        readonly GameObject outline;
        readonly LineRenderer line;
        readonly Material outlineMaterial;
        public ConstructionVisual(GameObject view,Material lineMaterial,float size,Quaternion rotation,Color color)
        {
            foreach(var renderer in view.GetComponentsInChildren<MeshRenderer>()) {
                var old=renderer.sharedMaterials;originals.Add((renderer,old));var copies=new Material[old.Length];
                for(int i=0;i<old.Length;i++) {
                    var m=new Material(old[i]);m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
                    m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=(int)RenderQueue.Transparent;
                    copies[i]=m;materials.Add(m);
                }
                renderer.sharedMaterials=copies;
            }
            outline=new GameObject("Construction outline");outline.transform.SetParent(view.transform,false);
            outlineMaterial=new Material(lineMaterial);
            line=outline.AddComponent<LineRenderer>();line.sharedMaterial=outlineMaterial;line.widthMultiplier=.09f;line.positionCount=5;
            line.useWorldSpace=false;
            float half=size*.5f;var center=view.transform.position+Vector3.up*.3f;
            var corners=new[]{new Vector3(-half,0,-half),new Vector3(-half,0,half),new Vector3(half,0,half),new Vector3(half,0,-half),new Vector3(-half,0,-half)};
            for(int i=0;i<5;i++)line.SetPosition(i,outline.transform.InverseTransformPoint(center+rotation*corners[i]));
            SetColor(color);
        }
        public void SetColor(Color color)
        {
            var tint=color;tint.a=.38f;
            foreach(var m in materials) {m.SetColor("_BaseColor",tint);m.SetColor("_Color",tint);}
            color.a=1;outlineMaterial.SetColor("_BaseColor",color);outlineMaterial.SetColor("_Color",color);line.startColor=line.endColor=Color.white;
        }
        public void Dispose()
        {
            foreach(var item in originals)if(item.Renderer!=null)item.Renderer.sharedMaterials=item.Original;
            foreach(var material in materials)UnityEngine.Object.Destroy(material);
            if(outline!=null)UnityEngine.Object.Destroy(outline);UnityEngine.Object.Destroy(outlineMaterial);
        }
    }
}
