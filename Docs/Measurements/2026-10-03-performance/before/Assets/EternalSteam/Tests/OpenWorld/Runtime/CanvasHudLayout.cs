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
        public bool CircularMap;
        public RectTransform EscapeHint;
        public CompactConstructionDock CompactDock;
        public RectTransform TestShortcuts;
        public RectTransform[] TestButtons=Array.Empty<RectTransform>();
        public RectTransform ConstructionSummary,FirstLoopObjective;
        public RectTransform EditHeader,PowerHeader,EditActions;
        public RectTransform[] FloatingPanels=Array.Empty<RectTransform>();
        public Vector2[] FloatingSizes=Array.Empty<Vector2>();
        public bool SingleRowCatalog;
        public float SelectionMaximumHeight=240;
        public UnityEngine.UI.GridLayoutGroup Catalog;
        public TMP_Text[] Texts;
        public float[] FontSizes;
        public float ExpandedHeight=204,CollapsedHeight=44,MinimumCellWidth=160,CellHeight=52,Gap=6;
        public float MinimumCellPixels=152,MinimumCellHeightPixels=44;
        public int Revision {get;private set;}
        Vector2 previousSize;float previousScale=-1;int previousState=-1;
        void LateUpdate(){Apply();FitObjective();}
        void FitObjective()
        {
            if(CompactDock!=null||FirstLoopObjective==null||!FirstLoopObjective.gameObject.activeInHierarchy)return;
            var label=FirstLoopObjective.GetComponentInChildren<TMP_Text>();
            if(label==null)return;
            float height=label.GetPreferredValues(label.text,Mathf.Max(1,FirstLoopObjective.rect.width-24),Mathf.Infinity).y+16;
            FirstLoopObjective.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(58,height));
        }
        public void Apply(bool force=false)
        {
            if(Root==null||Canvas==null||Catalog==null)return;
            var size=Root.rect.size;float scale=Mathf.Max(.01f,Canvas.scaleFactor);
            int state=(ClockBody.gameObject.activeSelf?1:0)|(ResourceBody.gameObject.activeSelf?2:0)|(MapBody.gameObject.activeSelf?4:0)|(SelectionPanel.gameObject.activeSelf?8:0)|(InventoryBody.gameObject.activeSelf?16:0)|((GetComponent<HudModeGroups>()?.ConstructionSelected??true)?32:0);
            if(!force&&size==previousSize&&Mathf.Approximately(scale,previousScale)&&state==previousState)return;
            previousSize=size;previousScale=scale;previousState=state;Revision++;
            if(EscapeHint!=null){EscapeHint.localScale=Vector3.one/scale;EscapeHint.anchoredPosition=new Vector2(-16/scale,-16/scale);}
            if(TestShortcuts!=null&&TestButtons.Length==3){
                bool narrow=size.x<1120;
                TestShortcuts.anchoredPosition=new Vector2(0,narrow?-68:-126);
                TestShortcuts.sizeDelta=new Vector2(narrow?280:450,narrow?84:44);
                for(int i=0;i<3;i++){
                    TestButtons[i].anchoredPosition=narrow?new Vector2(i==2?142:4,i==0?-4:-44):new Vector2(i==0?4:i==1?226:338,-4);
                    TestButtons[i].sizeDelta=new Vector2(narrow?(i==0?272:134):(i==0?218:108),36);
                }
            }
            float summaryHeight=Mathf.Max(104,76/scale);
            float bottom=(state&16)!=0?(SingleRowCatalog?Mathf.Max(ExpandedHeight,(ConstructionSummary!=null?114+summaryHeight:132)+Mathf.Max(CellHeight,MinimumCellHeightPixels/scale)):ExpandedHeight):CollapsedHeight;
            if(CompactDock!=null)bottom=CompactDock.HeightPixels/scale;
            if(CompactDock==null){
            ConstructionBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,bottom);
            StatusRow.gameObject.SetActive((state&16)!=0);
            if(ConstructionSummary!=null){
                ConstructionSummary.gameObject.SetActive((state&16)!=0);ConstructionSummary.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,summaryHeight);
                var inset=InventoryBody.offsetMin;inset.y=summaryHeight+16;InventoryBody.offsetMin=inset;
                if(EditHeader!=null&&PowerHeader!=null){float buttonWidth=Mathf.Max(128,90/scale);EditHeader.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,buttonWidth);PowerHeader.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,buttonWidth);PowerHeader.anchoredPosition=new Vector2(-16-buttonWidth,-6);StatusRow.offsetMax=new Vector2(-24-2*buttonWidth,-6);}
                if(EditActions!=null){EditActions.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,summaryHeight);float height=(summaryHeight-8)*.5f;for(int j=0;j<EditActions.childCount;j++){var action=EditActions.GetChild(j) as RectTransform;action.anchoredPosition=new Vector2(0,-j*(height+8));action.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);}}
            }
            if(FirstLoopObjective!=null){var pos=FirstLoopObjective.anchoredPosition;pos.y=bottom+108;FirstLoopObjective.anchoredPosition=pos;}
            if(CategoryStrip!=null)CategoryStrip.gameObject.SetActive((state&16)!=0);
            }
            float clockHeight=(state&1)!=0?156:28;
            ClockPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,clockHeight);
            bool grouped=GetComponent<HudModeGroups>()!=null;
            float resourceTop=grouped?84:84+clockHeight+8;
            ResourcePanel.anchoredPosition=new Vector2(0,grouped?0:-resourceTop);
            float resourceHeight=(state&2)!=0?Mathf.Clamp(size.y-bottom-48-resourceTop,96,280):28;
            ResourcePanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,resourceHeight);
            ResourceBody.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(0,resourceHeight-44));
            // Budget both panels together: a minimum selection height must never push it through the bottom bar.
            float rightAvailable=Mathf.Max(0,size.y-84-bottom-40);
            float mapAvailable=grouped&&(state&32)==0?size.y-84-40:rightAvailable-228;
            float mapHeight=(state&4)!=0?Mathf.Min(292,Mathf.Max(64,mapAvailable)):28;
            if(CircularMap){
                float diameter=Mathf.Clamp(size.y*scale*.24f,144,256)/scale;
                float frame=diameter/.78f;
                // Cancel RightStatus inset so the frame meets the top/right edges at every scale.
                MapPanel.anchoredPosition=new Vector2(16,84);
                if(EscapeHint!=null&&(state&32)==0)EscapeHint.anchoredPosition=new Vector2(-16/scale,-frame-8/scale);
                MapPanel.sizeDelta=Vector2.one*frame;
                MapBody.sizeDelta=Vector2.one*diameter;
                MapBody.anchoredPosition=Vector2.zero;
            }else{
                MapPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,mapHeight);
                float mapSide=Mathf.Max(1,Mathf.Min(MapPanel.rect.width-24,mapHeight-44));
                MapBody.sizeDelta=new Vector2(mapSide,mapSide);
                MapBody.anchoredPosition=new Vector2((MapPanel.rect.width-mapSide)*.5f,-36);
            }
            SelectionPanel.anchoredPosition=new Vector2(0,grouped?0:-mapHeight-8);
            float selectionHeight=Mathf.Max(0,Mathf.Min(SelectionMaximumHeight,grouped?rightAvailable:rightAvailable-mapHeight-8));
            SelectionPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,selectionHeight);
            Notifications.anchoredPosition=new Vector2(Notifications.anchoredPosition.x,grouped&&(state&32)==0?24:bottom+24);
            for(int i=0;i<FloatingPanels.Length;i++)if(FloatingPanels[i]!=null&&i<FloatingSizes.Length){
                var panel=FloatingPanels[i];
                // Resize the scroll viewport; keep popup text at a readable physical size.
                panel.localScale=Vector3.one/scale;
                panel.anchoredPosition=new Vector2(-16/scale,-84);
                panel.sizeDelta=new Vector2(Mathf.Min(FloatingSizes[i].x,size.x*scale-32),Mathf.Min(FloatingSizes[i].y,Mathf.Max(120,(size.y-108-bottom)*scale)));
            }
            if(CompactDock==null){
            float width=((RectTransform)Catalog.transform).rect.width;
            int columns=Mathf.Clamp(Mathf.FloorToInt((width+Gap)/(Mathf.Max(MinimumCellWidth,MinimumCellPixels/scale)+Gap)),1,8);
            Catalog.constraint=SingleRowCatalog?UnityEngine.UI.GridLayoutGroup.Constraint.FixedRowCount:UnityEngine.UI.GridLayoutGroup.Constraint.FixedColumnCount;
            Catalog.constraintCount=SingleRowCatalog?1:columns;Catalog.spacing=new Vector2(Gap,Gap);
            Catalog.cellSize=new Vector2(SingleRowCatalog?Mathf.Max(MinimumCellWidth,MinimumCellPixels/scale):Mathf.Max(1,(width-(columns-1)*Gap)/columns),Mathf.Max(CellHeight,MinimumCellHeightPixels/scale));
            }
            for(int i=0;i<Texts.Length;i++)if(Texts[i]!=null){float minimum=FontSizes[i]<=16?12:14;if(!Texts[i].enableAutoSizing)Texts[i].fontSize=(ConstructionSummary!=null&&Texts[i].transform.IsChildOf(ConstructionBar)?Mathf.Max(14/scale,FontSizes[i]):Mathf.Clamp(minimum/scale,FontSizes[i],FontSizes[i]*1.15f));}
            CompactDock?.Apply(0);
            foreach(var panel in FloatingPanels)if(panel!=null)foreach(var text in panel.GetComponentsInChildren<TMP_Text>(true))if(!text.enableAutoSizing)text.fontSize=18;
        }
    }
}
