using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace EternalSteam.OpenWorld
{
    // Fixed, authored row slots. Browsing never calls BaseRegistry.Select or changes the payer.
    public sealed class BaseResourceAccordion:MonoBehaviour
    {
        [Serializable] public sealed class ResourceLine {public string Id;public TMP_Text Value;}
        [Serializable] public sealed class Row {public GameObject Root,Body;public Button Header;public TMP_Text Name;public LayoutElement Layout;public ResourceLine[] Resources;[NonSerialized] public string BaseId;}
        public CanvasWorldHud Hud;
        public Row[] Rows=Array.Empty<Row>();
        public ScrollRect Scroll;
        public TMP_Text Empty,PageLabel,Title;
        public Button Previous,Next;
        public string ExpandedBaseId {get;private set;}
        public int Page {get;private set;}
        public int BaseCount {get;private set;}
        float nextRefresh;
        public void Toggle(int row){if(row<0||row>=Rows.Length||Rows[row].BaseId==null)return;ExpandedBaseId=ExpandedBaseId==Rows[row].BaseId?null:Rows[row].BaseId;Refresh();}
        public void PreviousPage(){Page=Mathf.Max(0,Page-1);ResetScroll();Refresh();}
        public void NextPage(){Page=Mathf.Min(Mathf.Max(0,(BaseCount-1)/Mathf.Max(1,Rows.Length)),Page+1);ResetScroll();Refresh();}
        void ResetScroll(){Scroll.StopMovement();Scroll.verticalNormalizedPosition=1;}
        void Update(){if(Hud.Groups.Construction.gameObject.activeInHierarchy&&Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.2f;Refresh();}}
        public void Refresh()
        {
            if(Hud?.Sandbox?.Content==null||Rows.Length==0)return;
            var content=Hud.Sandbox.Content;var bases=content.Bases.Bases.Values.Where(b=>b.Active).OrderBy(b=>b.Id,StringComparer.Ordinal).ToArray();BaseCount=bases.Length;
            if(ExpandedBaseId!=null&&!bases.Any(b=>b.Id==ExpandedBaseId))ExpandedBaseId=null;
            Page=Mathf.Clamp(Page,0,Mathf.Max(0,(BaseCount-1)/Rows.Length));
            int expandedIndex=Array.FindIndex(bases,b=>b.Id==ExpandedBaseId);
            Title.text=expandedIndex>=Page*Rows.Length&&expandedIndex<(Page+1)*Rows.Length?"재고 · "+bases[expandedIndex].Nexus.DisplayName+" · "+(expandedIndex+1):"기지별 보유 자원";Empty.gameObject.SetActive(BaseCount==0);Empty.text=content.BaseRules?"활성 기지가 없습니다.":"이 씬은 기지별 재고를 사용하지 않습니다.";
            Previous.interactable=Page>0;Next.interactable=(Page+1)*Rows.Length<BaseCount;PageLabel.text=BaseCount==0?"0 / 0":$"{Page+1} / {(BaseCount+Rows.Length-1)/Rows.Length}";
            for(int i=0;i<Rows.Length;i++){
                var row=Rows[i];int index=Page*Rows.Length+i;bool visible=index<BaseCount;row.Root.SetActive(visible);if(!visible){row.BaseId=null;continue;}
                var b=bases[index];row.BaseId=b.Id;bool expanded=ExpandedBaseId==b.Id;row.Name.text=(expanded?"▼ ":"▶ ")+b.Nexus.DisplayName+" · "+(index+1);row.Body.SetActive(expanded);row.Layout.preferredHeight=44+(expanded?row.Resources.Length*28+8:0);
                var bank=content.Inventories.Available(b.Id);foreach(var line in row.Resources){string value=(bank?.Amount(line.Id)??0).ToString("N0");if(line.Value.text!=value)line.Value.text=value;}
            }
        }
    }
}
