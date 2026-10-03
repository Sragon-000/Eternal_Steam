using UnityEngine;
using UnityEngine.UI;
namespace EternalSteam.OpenWorld
{
    // One authored workspace owns construction and railway navigation; no runtime UI creation.
    [DefaultExecutionOrder(200)]
    public sealed class GameplayHudWorkspace:MonoBehaviour
    {
        public CanvasWorldHud Hud;public RailwayHud Railway;
        public RectTransform RailwayWorkspace;public Button ConstructionTab,RailwayTab;
        bool railWasOpen,restoreInventory;
        public void Construction()
        {
            if(Railway.HasDraft){Railway.Execute("close");if(Railway.HasDraft)return;}
            if(Railway.Panel.activeSelf)Railway.Execute("close");
            Hud.Layout.InventoryBody.gameObject.SetActive(true);restoreInventory=true;
            Hud.Refresh();
        }
        public void Railways()
        {
            if(Hud.Input.IsEditing){
                if((Hud.Input.Edits?.Count??0)>0){Hud.Sandbox.Message="건설 작업을 확정하거나 취소한 뒤 철도 관리를 여세요.";return;}
                Hud.Input.Cancel();
            }
            Railway.Execute("open");
        }
        void LateUpdate(){Apply();}
        public void Apply()
        {
            bool open=Railway.Panel.activeSelf;
            if(open&&Hud.Input.IsEditing&&!Railway.HasDraft){Railway.Execute("close");open=Railway.Panel.activeSelf;}
            if(open!=railWasOpen){
                if(open){restoreInventory=Hud.Layout.InventoryBody.gameObject.activeSelf;Hud.Layout.InventoryBody.gameObject.SetActive(false);}
                else Hud.Layout.InventoryBody.gameObject.SetActive(restoreInventory);
                railWasOpen=open;Hud.Layout.Apply(true);
            }
            var size=Hud.Layout.Root.rect.size;
            // Keep the management workspace between resource bank, selected-info column and bottom bar.
            float availableWidth=Mathf.Max(1,size.x-632),availableHeight=Mathf.Max(1,size.y-104-Hud.Layout.ConstructionBar.rect.height-160);
            float scale=Mathf.Min(1,availableWidth/948,availableHeight/(Railway.DraftStopsPanel.activeSelf?810:666));
            RailwayWorkspace.localScale=Vector3.one*scale;
            SetSelected(ConstructionTab,!open);SetSelected(RailwayTab,open);
        }
        static void SetSelected(Button button,bool selected){var colors=button.colors;colors.normalColor=selected?new Color(1.18f,1.10f,.84f):Color.white;button.colors=colors;}
    }
}
