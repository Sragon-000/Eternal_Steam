using System;
using TMPro;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // All referenced objects are authored in the scene. Only responsive geometry changes here.
    [DefaultExecutionOrder(100)]
    public sealed class CanvasHudLayout:MonoBehaviour
    {
        public Canvas Canvas;
        public RectTransform Root,ClockPanel,ClockBody,ResourcePanel,ResourceBody,ResourceScroll;
        public RectTransform MapPanel,MapBody,SelectionPanel,SelectionScroll,ConstructionBar,InventoryBody,StatusRow,Notifications;
        public RectTransform CategoryStrip;
        public RectTransform[] FloatingPanels=Array.Empty<RectTransform>();
        public bool SingleRowCatalog;
        public float SelectionMaximumHeight=240;
        public UnityEngine.UI.GridLayoutGroup Catalog;
        public TMP_Text[] Texts;
        public float[] FontSizes;
        public float ExpandedHeight=204,CollapsedHeight=44,MinimumCellWidth=160,CellHeight=52,Gap=6;
        public float MinimumCellPixels=152,MinimumCellHeightPixels=44;
        public int Revision {get;private set;}
        Vector2 previousSize;float previousScale=-1;int previousState=-1;
        void LateUpdate(){Apply();}
        public void Apply(bool force=false)
        {
            if(Root==null||Canvas==null||Catalog==null)return;
            var size=Root.rect.size;float scale=Mathf.Max(.01f,Canvas.scaleFactor);
            int state=(ClockBody.gameObject.activeSelf?1:0)|(ResourceBody.gameObject.activeSelf?2:0)|(MapBody.gameObject.activeSelf?4:0)|(SelectionPanel.gameObject.activeSelf?8:0)|(InventoryBody.gameObject.activeSelf?16:0)|((GetComponent<HudModeGroups>()?.ConstructionSelected??true)?32:0);
            if(!force&&size==previousSize&&Mathf.Approximately(scale,previousScale)&&state==previousState)return;
            previousSize=size;previousScale=scale;previousState=state;Revision++;
            float bottom=(state&16)!=0?(SingleRowCatalog?Mathf.Max(ExpandedHeight,132+Mathf.Max(CellHeight,MinimumCellHeightPixels/scale)):ExpandedHeight):CollapsedHeight;
            ConstructionBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,bottom);
            StatusRow.gameObject.SetActive((state&16)!=0);
            if(CategoryStrip!=null)CategoryStrip.gameObject.SetActive((state&16)!=0);
            float clockHeight=(state&1)!=0?156:28;
            ClockPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,clockHeight);
            bool grouped=GetComponent<HudModeGroups>()!=null;
            float resourceTop=grouped?0:84+clockHeight+8;
            ResourcePanel.anchoredPosition=new Vector2(0,-resourceTop);
            float resourceHeight=(state&2)!=0?Mathf.Clamp(size.y-bottom-48-resourceTop,96,280):28;
            ResourcePanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,resourceHeight);
            ResourceBody.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(0,resourceHeight-44));
            // Budget both panels together: a minimum selection height must never push it through the bottom bar.
            float rightAvailable=Mathf.Max(0,size.y-84-bottom-40);
            float mapHeight=(state&4)!=0?Mathf.Min(292,Mathf.Max(64,rightAvailable-228)):28;
            MapPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,mapHeight);
            float mapSide=Mathf.Max(1,Mathf.Min(MapPanel.rect.width-24,mapHeight-44));
            MapBody.sizeDelta=new Vector2(mapSide,mapSide);
            MapBody.anchoredPosition=new Vector2((MapPanel.rect.width-mapSide)*.5f,-36);
            SelectionPanel.anchoredPosition=new Vector2(0,grouped?0:-mapHeight-8);
            float selectionHeight=Mathf.Max(0,Mathf.Min(SelectionMaximumHeight,rightAvailable-mapHeight-8));
            SelectionPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,selectionHeight);
            Notifications.anchoredPosition=new Vector2(Notifications.anchoredPosition.x,grouped&&(state&32)==0?24:bottom+24);
            foreach(var panel in FloatingPanels)if(panel!=null){
                // Top/right anchored authored popups fit above the bottom dock even on shallow windows.
                float fit=Mathf.Min(1,Mathf.Max(1,size.x-640)/Mathf.Max(1,panel.rect.width),Mathf.Max(1,size.y-108-bottom)/Mathf.Max(1,panel.rect.height));
                panel.localScale=Vector3.one*fit;
            }
            float width=((RectTransform)Catalog.transform).rect.width;
            int columns=Mathf.Clamp(Mathf.FloorToInt((width+Gap)/(Mathf.Max(MinimumCellWidth,MinimumCellPixels/scale)+Gap)),1,8);
            Catalog.constraint=SingleRowCatalog?UnityEngine.UI.GridLayoutGroup.Constraint.FixedRowCount:UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            Catalog.constraintCount=SingleRowCatalog?1:columns;Catalog.spacing=new Vector2(Gap,Gap);
            Catalog.cellSize=new Vector2(SingleRowCatalog?Mathf.Max(MinimumCellWidth,MinimumCellPixels/scale):Mathf.Max(1,(width-(columns-1)*Gap)/columns),Mathf.Max(CellHeight,MinimumCellHeightPixels/scale));
            for(int i=0;i<Texts.Length;i++)if(Texts[i]!=null){float minimum=FontSizes[i]<=16?12:14;if(!Texts[i].enableAutoSizing)Texts[i].fontSize=Mathf.Clamp(minimum/scale,FontSizes[i],FontSizes[i]*1.15f);}
        }
    }
}
