using System;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using EternalSteam.Railway;
namespace EternalSteam.OpenWorld
{
    // Authored overview. All state-changing commands remain in RailwayHud/Network.
    [DefaultExecutionOrder(250)]
    public sealed class RailwayOverview:MonoBehaviour
    {
        [Serializable] public struct Action {public string Command;public Button Button;}
        public RailwayHud Railway;public CanvasWorldHud Hud;
        public RectTransform Root,List,Detail,DetailScroll;
        public CanvasGroup Legacy;
        public CanvasGroup[] QuietPanels=Array.Empty<CanvasGroup>();
        public CanvasGroup ResourcePanel;
        public TMP_Text Heading,Page,Empty,DetailText;
        public Button[] Rows;
        public Button Previous,Next,Back,Close,LegacyBack;
        public Action[] Actions;
        public bool Narrow {get;private set;}
        public RailwayServicePanel Services;
        public RailwayEditorPanel Editor;
        public bool ShowingCargo {get;private set;}
        public bool IsOverview=>Railway.Panel.activeSelf&&!Railway.HasDraft;
        public void SelectRow(int row){if(!IsOverview||row<0||row>=Rows.Length)return;var routes=Railway.OverviewRoutes;if(Railway.RoutePage*Rows.Length+row>=routes.Count)return;Railway.Execute("list");Railway.Execute("row:"+row);Apply();}
        public void Execute(string command){
            if(command=="overview"){if(Railway.HasDraft)return;if(Railway.ShowingArmament)Railway.Execute("weapon-back");ShowingCargo=false;}
            else if(command=="cargo"){if(Railway.SelectedRoute!=null){ShowingCargo=true;}}
            else {if(command is "list" or "close" or "defense" or "new" or "connect" or "edit")ShowingCargo=false;if(command is "new" or "connect")Railway.Execute("list");Railway.Execute(command);}
            Apply();
        }
        void LateUpdate(){Apply();}
        void OnDisable(){
            foreach(var panel in QuietPanels)if(panel!=null){panel.alpha=1;panel.blocksRaycasts=panel.interactable=true;}
            if(ResourcePanel!=null){ResourcePanel.alpha=1;ResourcePanel.blocksRaycasts=ResourcePanel.interactable=true;}
            if(Root!=null)Root.gameObject.SetActive(false);
        }
        static void Box(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
        public void Apply()
        {
            if(Railway?.Sandbox?.Content==null)return;
            if(!Railway.Panel.activeSelf){ShowingCargo=false;}
            bool show=Railway.Panel.activeSelf;Root.gameObject.SetActive(show);Legacy.alpha=show?0:1;Legacy.interactable=Legacy.blocksRaycasts=!show;
            LegacyBack.gameObject.SetActive(Railway.Panel.activeSelf&&!show&&!Railway.HasDraft);
            foreach(var panel in QuietPanels){panel.alpha=show?0:1;panel.blocksRaycasts=panel.interactable=!show;}
            if(ResourcePanel!=null){ResourcePanel.alpha=1;ResourcePanel.blocksRaycasts=ResourcePanel.interactable=true;}
            if(!show)return;
            float scale=Mathf.Max(.01f,Hud.Layout.Canvas.scaleFactor),sw=Hud.Layout.Root.rect.width*scale,sh=Hud.Layout.Root.rect.height*scale;
            Narrow=sw<1280;float left=Narrow?16:Mathf.Max(16,320*scale),available=sw-left-16,w=Mathf.Min(1060,available),top=Mathf.Max(96,184*scale),height=Mathf.Clamp(sh-top-Mathf.Max(64,Hud.Layout.ConstructionBar.rect.height*scale+24),280,620);
            Root.localScale=Vector3.one/scale;Box(Root,(left+(available-w)*.5f)/scale,top/scale,w,height);
            if(ResourcePanel!=null&&Narrow){ResourcePanel.alpha=0;ResourcePanel.blocksRaycasts=ResourcePanel.interactable=false;}
            bool editing=Railway.HasDraft;Editor.Root.gameObject.SetActive(editing);
            if(editing){float ew=Mathf.Min(560,sw*.6f);Root.localScale=Vector3.one/scale;Box(Root,(sw-ew-16)/scale,top/scale,ew,height);List.gameObject.SetActive(false);Detail.gameObject.SetActive(false);Services.Root.gameObject.SetActive(false);Back.gameObject.SetActive(false);Box(Heading.rectTransform,16,12,ew-140,32);Heading.text=Railway.Title.text;Box((RectTransform)Close.transform,ew-104,10,88,36);Close.interactable=Railway.CanExecute("close",out _);Box(Editor.Root,16,56,ew-32,height-64);if(ResourcePanel!=null){ResourcePanel.alpha=0;ResourcePanel.blocksRaycasts=ResourcePanel.interactable=false;}Editor.Refresh();return;}
            Close.interactable=true;
            var route=Railway.SelectedRoute;bool detail=route!=null;
            bool service=detail&&(ShowingCargo||Railway.ShowingArmament);
            Services.Root.gameObject.SetActive(service);
            if(service){List.gameObject.SetActive(false);Detail.gameObject.SetActive(false);Back.gameObject.SetActive(false);Box(Heading.rectTransform,16,12,w-140,32);Heading.text=(ShowingCargo?"화물 · ":"방어칸 · ")+route.name;Box((RectTransform)Close.transform,w-104,10,88,36);Box(Services.Root,16,56,w-32,height-64);Services.Refresh();return;}
            List.gameObject.SetActive(!Narrow||!detail);Detail.gameObject.SetActive(!Narrow||detail);Back.gameObject.SetActive(detail);
            Box(Heading.rectTransform,16,12,w-240,32);Heading.text=detail?"철도 · "+route.name:"철도 · 노선 목록";Box((RectTransform)Close.transform,w-104,10,88,36);Box((RectTransform)Back.transform,w-204,10,92,36);
            float listWidth=Narrow?w-32:Mathf.Min(350,(w-48)*.38f),bodyHeight=height-64;
            Box(List,16,56,listWidth,bodyHeight);Box(Detail,Narrow?16:listWidth+32,56,Narrow?w-32:w-listWidth-48,bodyHeight);
            var routes=Railway.OverviewRoutes;int page=Railway.RoutePage;
            Box(Page.rectTransform,0,0,listWidth,28);Page.text=$"노선 {routes.Count}개 · {page+1}/{Math.Max(1,(routes.Count+Rows.Length-1)/Rows.Length)}";
            Empty.gameObject.SetActive(routes.Count==0);Empty.text="등록된 노선이 없습니다.\n역 두 곳을 연결하고 노선을 만드세요.";Box(Empty.rectTransform,0,36,listWidth,96);
            float rowHeight=Mathf.Min(70,(bodyHeight-136)/Rows.Length);
            for(int i=0;i<Rows.Length;i++){int index=page*Rows.Length+i;var row=Rows[i];row.gameObject.SetActive(index<routes.Count);Box((RectTransform)row.transform,0,32+i*(rowHeight+4),listWidth,rowHeight);if(index>=routes.Count)continue;var r=routes[index];row.GetComponentInChildren<TMP_Text>().text=(r==route?"▶ ":"")+r.name+"\n"+(r.train.waitingForStation?"역 진입 대기":RailwayHud.Status(r.train.status))+(Railway.Sandbox.Content.Railway.Network.HasPending(r)?" · 예약":"");}
            Box((RectTransform)Previous.transform,0,bodyHeight-88,(listWidth-8)*.5f,36);Box((RectTransform)Next.transform,(listWidth+8)*.5f,bodyHeight-88,(listWidth-8)*.5f,36);Previous.interactable=page>0;Next.interactable=(page+1)*Rows.Length<routes.Count;
            foreach(var a in Actions){bool listAction=a.Command is "new" or "connect";a.Button.gameObject.SetActive(listAction||detail);a.Button.interactable=a.Command=="cargo"?detail:Railway.CanExecute(a.Command,out _);}
            Place("new",List,0,bodyHeight-44,(listWidth-8)*.5f);Place("connect",List,(listWidth+8)*.5f,bodyHeight-44,(listWidth-8)*.5f);
            float dw=Detail.rect.width;
            if(!detail){Box(DetailScroll,0,0,dw,bodyHeight);DetailText.text="목록에서 노선을 선택하세요.\n\n현재·다음 역, 연료와 화물을 확인하고 운행을 시작할 수 있습니다.\n\n새 노선은 역 두 곳을 연결한 뒤 등록하세요.";return;}Box(DetailScroll,0,0,dw,bodyHeight-100);
            string[] primary={"start","stop","fuel"};for(int i=0;i<3;i++)Place(primary[i],Detail,i*(dw+8)/3,bodyHeight-92,(dw-16)/3);
            string[] secondary={"cargo","defense","edit","cancel-pending"};for(int i=0;i<4;i++)Place(secondary[i],Detail,i*(dw+8)/4,bodyHeight-44,(dw-24)/4);
            DetailText.text=Describe(route);
        }
        void Place(string command,RectTransform parent,float x,float y,float width){var b=Actions.First(a=>a.Command==command).Button;Box((RectTransform)b.transform,x,y,width,40);}
        public static string CargoName(string id)=>id switch {"iron"=>"철","copper"=>"구리","coal"=>"석탄","nanometal"=>"나노메탈","plasma_ore"=>"플라즈마","titanium"=>"티타늄","tungsten"=>"텅스텐","uranium"=>"우라늄",_=>id};
        public string Describe(RailRoute route)
        {
            var n=Railway.Sandbox.Content.Railway.Network;var t=route.train;var s=new StringBuilder();
            s.AppendLine("상태 · "+(t.waitingForStation?"역 진입 대기":RailwayHud.Status(t.status)));
            s.AppendLine((t.segmentPaid?"출발 역 · ":"현재 역 · ")+n.StationName(route.stops[t.stop].stationId));
            s.AppendLine("다음 역 · "+n.StationName(route.stops[route.NextStopIndex].stationId));
            s.AppendLine($"연료 · 석탄 {t.fuel:0.##} / {route.stops.Count*RailwayNetwork.FuelPerStation}");
            s.AppendLine($"화물 · {(t.HasCargoResource?CargoName(t.resource):"미지정")} {t.cargo:0.##} / {RailwayNetwork.CargoCapacity}");
            if(t.status==TrainStatus.StopRequested)s.AppendLine("다음 역 도착 후 정지합니다.");
            if(n.HasPending(route))s.AppendLine(route.pending.failed?"예약 실패 · "+route.pending.error:"예약 변경 · 기존 첫 역 복귀 때 적용");
            if(!string.IsNullOrEmpty(route.error))s.AppendLine("노선 오류 · "+route.error);
            foreach(var pair in new[]{("start","운행 시작"),("stop","중지 요청"),("fuel","석탄 보급")})if(!Railway.CanExecute(pair.Item1,out var reason)&&reason!=null)s.AppendLine(pair.Item2+" 불가 · "+reason);
            return s.ToString();
        }
    }
}
