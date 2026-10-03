using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace EternalSteam.OpenWorld
{
    // Authored view only. Drafts, path finding, payment and revisions stay in RailwayHud.
    public sealed class RailwayEditorPanel:MonoBehaviour
    {
        [Flags] public enum Screen { Connection=1, Stations=2, Diagnostics=4, Confirm=8, Discard=16 }
        [Serializable] public struct Binding {public string Command;public Button Button,Source;public Screen Screens;public bool Footer;}
        public RailwayOverview Overview;
        public RectTransform Root,Scroll,Content,Tabs;
        public TMP_Text Description,Feedback,ScrollHint;
        public Button Stations,Diagnostics;
        public Binding[] Bindings;
        public bool ShowingDiagnostics {get;private set;}
        Screen? previous;
        public Screen Current=>Overview.Railway.ConnectionPanel.activeSelf?Screen.Connection:Overview.Railway.Stage==RailwayHud.EditorStage.Confirming?Screen.Confirm:Overview.Railway.Stage==RailwayHud.EditorStage.DiscardPrompt?Screen.Discard:ShowingDiagnostics?Screen.Diagnostics:Screen.Stations;
        public void SelectPage(int page){ShowingDiagnostics=page==1;Refresh();}
        public void Execute(string command){Overview.Railway.Execute(command);Overview.Apply();}
        static void Box(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
        public void Refresh()
        {
            var rail=Overview.Railway;if(!rail.HasDraft)return;var screen=Current;float w=Root.rect.width,h=Root.rect.height;bool tabs=screen is Screen.Stations or Screen.Diagnostics;
            Tabs.gameObject.SetActive(tabs);Box(Tabs,0,0,w,40);Box((RectTransform)Stations.transform,0,0,(w-8)/2,36);Box((RectTransform)Diagnostics.transform,(w+8)/2,0,(w-8)/2,36);
            Stations.GetComponentInChildren<TMP_Text>().text=(ShowingDiagnostics?"":"▶ ")+"역 순서";Diagnostics.GetComponentInChildren<TMP_Text>().text=(ShowingDiagnostics?"▶ ":"")+"구간·오류";
            Box(Scroll,0,tabs?44:0,w,h-(tabs?44:0)-130);Box(Feedback.rectTransform,0,h-124,w,58);Feedback.text=string.IsNullOrEmpty(rail.Feedback.text)?"":"최근 실행 · "+rail.Feedback.text;
            Box(ScrollHint.rectTransform,0,h-64,w,22);ScrollHint.text="휠로 위·아래 보기";ScrollHint.gameObject.SetActive(Content.rect.height>Scroll.rect.height+1);
            Description.text=screen switch {
                Screen.Connection=>rail.Summary.text,
                Screen.Stations=>rail.DraftSelection.text+"\n지도에서 역을 선택한 뒤 추가하세요. 순서 변경은 확정 전 초안입니다.",
                Screen.Diagnostics=>rail.SelectedSegment.text+"\n"+rail.IssueHeader.text+(rail.DraftIssue==null?"":"\n"+rail.DraftIssue.Message),
                Screen.Confirm=>rail.Summary.text,
                _=>"미저장 노선 변경을 폐기할까요?\n계속 편집하면 초안을 유지합니다. 폐기는 아직 확정하지 않은 변경만 버립니다."};
            foreach(var b in Bindings){bool visible=(b.Screens&screen)!=0;if(visible&&!b.Footer)visible=b.Source.gameObject.activeSelf;b.Button.gameObject.SetActive(visible);b.Button.interactable=rail.CanExecute(b.Command,out _);if(!b.Footer)b.Button.GetComponentInChildren<TMP_Text>().text=b.Source.GetComponentInChildren<TMP_Text>().text;}
            var footer=Bindings.Where(b=>b.Footer&&b.Button.gameObject.activeSelf).ToArray();for(int i=0;i<footer.Length;i++)Box((RectTransform)footer[i].Button.transform,i*(w+6)/footer.Length,h-40,(w-6*(footer.Length-1))/footer.Length,40);
            if(previous!=screen){previous=screen;LayoutRebuilder.ForceRebuildLayoutImmediate(Content);Scroll.GetComponent<ScrollRect>().verticalNormalizedPosition=1;}
        }
    }
}
