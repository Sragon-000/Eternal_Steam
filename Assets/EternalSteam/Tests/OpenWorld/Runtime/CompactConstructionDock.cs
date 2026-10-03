using System;using System.Linq;using TMPro;using UnityEngine;
namespace EternalSteam.OpenWorld
{
    // Geometry and visibility only. All controls, masks and callbacks are authored in the scenes.
    [DefaultExecutionOrder(300)]
    public sealed class CompactConstructionDock:MonoBehaviour
    {
        public CanvasWorldHud Hud;
        public CanvasGroup Browse,Placement,HeaderInput;
        public RectTransform Header,CategoryScroll,CostPanel,GoalPanel,GoalStrip,PowerSummary;
        public TMP_Text PlacementTitle,CostBrief,BrowseHint,GoalBrief;
        public UnityEngine.UI.Button ListButton,CostButton,ResumeButton;
        public UnityEngine.UI.ScrollRect CatalogScroll;
        public bool BrowsingRequested {get;private set;}
        public bool Transitioning {get;private set;}
        public bool Placing=>Hud.Input.IsEditing&&!BrowsingRequested&&!Hud.Sandbox.RailwayHud.Panel.activeSelf;
        public float HeightPixels=>Hud.Sandbox.RailwayHud.Panel.activeSelf||NarrowSelection?52:Placing?80:156;
        public bool NarrowSelection=>Hud.Layout.Root.rect.width*Hud.Layout.Canvas.scaleFactor<1000&&Hud.Layout.SelectionPanel.gameObject.activeSelf&&!Hud.Input.IsEditing;
        int state=-1;float progress=1,browseVisibility=1,placementVisibility,fromHeight=156;bool wasEditing;string placementProblem;
        public CanvasGroup DockInput;
        readonly Vector3[] powerCorners=new Vector3[4];
        float CurrentScale=>Mathf.Max(.01f,Hud.Layout.Canvas.scaleFactor);
        public void PlacementSelected(){BrowsingRequested=false;placementProblem=null;CloseDetails();}
        public void BrowseRequested(){BrowsingRequested=true;CloseDetails();}
        public void Execute(string command)
        {
            if(Hud.Sandbox.PauseMenu?.BlocksInput==true||Transitioning||Hud.Groups.Transitioning)return;
            switch(command){
                case "list":BrowseRequested();break;
                case "resume":BrowsingRequested=false;break;
                case "cost":GoalPanel.gameObject.SetActive(false);CostPanel.gameObject.SetActive(!CostPanel.gameObject.activeSelf);break;
                case "goal":CostPanel.gameObject.SetActive(false);GoalPanel.gameObject.SetActive(!GoalPanel.gameObject.activeSelf);break;
                case "close-cost":CostPanel.gameObject.SetActive(false);break;
                case "close-goal":GoalPanel.gameObject.SetActive(false);break;
            }
        }
        public void CloseDetails(){CostPanel.gameObject.SetActive(false);GoalPanel.gameObject.SetActive(false);}
        void LateUpdate(){Apply(Time.unscaledDeltaTime);}
        public void Apply(float delta=0)
        {
            if(Hud==null)return;
            bool editing=Hud.Input.IsEditing,rail=Hud.Sandbox.RailwayHud.Panel.activeSelf;
            if(wasEditing&&!editing){BrowsingRequested=false;placementProblem=null;CloseDetails();}wasEditing=editing;
            bool placing=Placing,browsing=!rail&&!placing&&!NarrowSelection;
            int next=rail?2:placing?1:NarrowSelection?3:0;
            if(next!=state){fromHeight=Hud.Layout.ConstructionBar.rect.height;state=next;progress=0;Transitioning=true;}
            progress=Mathf.Min(1,progress+Mathf.Max(0,delta)/.24f);Transitioning=progress<1;
            float ease=progress*progress*(3-2*progress);
            float scale=CurrentScale;var pixels=Hud.Layout.Root.rect.size*scale;var bar=Hud.Layout.ConstructionBar;
            bar.localScale=Vector3.one/scale;bar.anchoredPosition=new Vector2(0,16/scale);bar.sizeDelta=new Vector2(Mathf.Min(pixels.x-32,1280),Mathf.Lerp(fromHeight,HeightPixels,ease));
            DockInput.interactable=!Transitioning;
            Header.anchoredPosition=new Vector2(0,0);
            browseVisibility=Mathf.MoveTowards(browseVisibility,browsing?1:0,delta/.24f);placementVisibility=Mathf.MoveTowards(placementVisibility,placing?1:0,delta/.24f);
            SetGroup(Browse,browsing,browseVisibility);SetGroup(Placement,placing,placementVisibility);
            float headerAmount=1-placementVisibility,headerEase=headerAmount*headerAmount*(3-2*headerAmount);Header.gameObject.SetActive(headerAmount>0);HeaderInput.alpha=headerEase;HeaderInput.interactable=HeaderInput.blocksRaycasts=!placing&&!Transitioning;float shift=(1-headerEase)*180;Header.offsetMin=new Vector2(8,-44-shift);Header.offsetMax=new Vector2(-8,-8-shift);
            Hud.Layout.InventoryBody.gameObject.SetActive(browsing||browseVisibility>0);CategoryScroll.gameObject.SetActive(browsing||browseVisibility>0);Hud.Layout.CategoryStrip.gameObject.SetActive(browsing||browseVisibility>0);
            Hud.Layout.EditActions.gameObject.SetActive(placing||placementVisibility>0);Hud.Layout.StatusRow.gameObject.SetActive(false);
            ResumeButton.gameObject.SetActive(browsing&&editing);
            Hud.Layout.EditHeader.gameObject.SetActive(!rail&&!editing);Hud.Layout.PowerHeader.gameObject.SetActive(!rail&&!editing);
            var width=bar.rect.width;
            CategoryScroll.offsetMin=new Vector2(208,-36);CategoryScroll.offsetMax=new Vector2(-208,0);
            Hud.Layout.Catalog.cellSize=new Vector2(190,60);Hud.Layout.Catalog.spacing=new Vector2(6,0);
            if(rail)CloseDetails();
            foreach(var p in new[]{CostPanel,GoalPanel}){p.localScale=Vector3.one/scale;p.sizeDelta=new Vector2(Mathf.Min(560,pixels.x-32),Mathf.Min(320,pixels.y-HeightPixels-140));p.anchoredPosition=new Vector2(-16/scale,(HeightPixels+24)/scale);}
            GoalStrip.localScale=Vector3.one/scale;GoalStrip.sizeDelta=new Vector2(Mathf.Min(560,pixels.x*.4f),36);var test=Hud.Layout.TestShortcuts;float top=(-test.anchoredPosition.y+test.rect.height)*scale+8;GoalStrip.anchoredPosition=new Vector2(0,-top/scale);GoalStrip.gameObject.SetActive(Hud.Sandbox.Assault!=null);
            bool construction=Hud.Groups.ConstructionSelected;
            var notifications=Hud.Layout.Notifications.GetComponent<CanvasGroup>();notifications.alpha=construction?0:1;notifications.blocksRaycasts=false;
            PowerSummary.GetWorldCorners(powerCorners);
            float resourceTop=Mathf.Max(84,(Hud.Layout.Root.rect.yMax-Hud.Layout.Root.InverseTransformPoint(powerCorners[0]).y)*scale+8);
            var bank=Hud.Layout.ResourcePanel;bank.localScale=Vector3.one/scale;bank.anchoredPosition=new Vector2(16/scale-16,84-resourceTop/scale);bank.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Min(272,pixels.x*.3f-24));bank.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Min(280,pixels.y-resourceTop-HeightPixels-40));Hud.Layout.ResourceBody.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,bank.rect.height-44);
            if(Hud.Layout.FirstLoopObjective!=null){var text=Hud.Layout.FirstLoopObjective.GetComponentInChildren<TMP_Text>(true);if(text!=null)Hud.Layout.FirstLoopObjective.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(100,text.GetPreferredValues(text.text,Mathf.Max(1,GoalPanel.rect.width-48),Mathf.Infinity).y+32));}
            UpdateLabels();
        }
        void SetGroup(CanvasGroup group,bool visible,float ease)
        {
            float amount=ease*ease*(3-2*ease);group.gameObject.SetActive(ease>0||visible);group.alpha=amount;group.blocksRaycasts=group.interactable=visible&&!Transitioning;
            var r=(RectTransform)group.transform;r.anchoredPosition=new Vector2(0,(1-amount)*-180);
        }
        static string ResourceName(string id)=>id switch {"iron"=>"철","coal"=>"석탄","copper"=>"구리","nanometal"=>"나노메탈","titanium"=>"티타늄","uranium"=>"우라늄","tungsten"=>"텅스텐","plasma_ore"=>"플라즈마",_=>id};
        void UpdateLabels()
        {
            var input=Hud.Input;var edits=input.Edits;if(edits==null)return;
            string name=input.SelectedDefinition?.DisplayName??(input.Tool==WorldTool.Foundation?"토대":input.Tool==WorldTool.Tower?input.Kind.ToString():"회수 선택");name=name.Split(" · ")[0];
            string payer=Hud.Sandbox.Content.BaseRules?Hud.Sandbox.Content.Bases.Bases.TryGetValue(Hud.Sandbox.Content.Bases.SelectedBaseId??"",out var b)&&b.Active?b.Nexus.DisplayName:"기지 없음":"공용 재고";
            PlacementTitle.text=$"{name} · 대기 {edits.Count}개 · {payer}";
            var candidate=input.SelectedDefinition??(input.Tool==WorldTool.Tower?Hud.Sandbox.Foundations.Definition(input.Kind):null);
            var quote=edits.Count==0?edits.Quote(candidate,foundation:input.Tool==WorldTool.Foundation):edits.Quote();var first=quote.Lines.FirstOrDefault();string cost=edits.RecoveryCount>0?$"회수 {edits.RecoveryCount}개":quote.Lines.Count==0?"비용 없음":$"{ResourceName(first.Resource)} {first.Required:N0} / {first.Available:N0}"+(quote.Lines.Count>1?$" 외 {quote.Lines.Count-1}종":"");
            if(input.PlacementHint.StartsWith("설치 불가"))placementProblem=input.PlacementHint.Split(" · ")[0];else if(input.PlacementHint.StartsWith("설치 가능"))placementProblem=null;
            string problem=quote.Reason??placementProblem;
            if(problem!=null)foreach(var id in new[]{"iron","coal","copper","nanometal","plasma_ore","titanium","tungsten","uranium"})problem=problem.Replace(id,ResourceName(id));
            CostBrief.text="<line-height=16>"+cost+"\n"+(problem??(Hud.Sandbox.Content.InfiniteResources?"자원 무한 적용":"비용 상세에서 전체 확인"));CostBrief.color=problem==null?Color.white:new Color(1,.65f,.4f);
            BrowseHint.text=Hud.Sandbox.Message;
            var a=Hud.Sandbox.Assault;GoalBrief.text=a==null?"":$"목표 · 에너지 {100d*a.Energy.Earned/a.Energy.Target:0.#}%";
        }
    }
}
