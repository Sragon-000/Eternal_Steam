using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace EternalSteam.OpenWorld
{
    // The modal hierarchy is saved in each scene. Opening never changes campaign pause state.
    [DefaultExecutionOrder(-300)]
    public sealed class HudPauseMenu : MonoBehaviour
    {
        public CanvasWorldHud Hud;
        public CanvasGroup Overlay,Content;
        public RectTransform Panel,SaveSection,ProgressSection,DeveloperEntry;
        public UnityEngine.UI.ScrollRect Scroll;
        public GameObject LoadConfirmation,NewGameConfirmation,Developer;
        public TMP_Text Heading;
        public bool IsOpen {get;private set;}
        public bool BlocksInput=>IsOpen||opacity>0||consumedFrame==Time.frameCount;
        public bool Composing {get;private set;}
        float opacity;
        int consumedFrame=-1,compositionEndedFrame=-1;
        Keyboard keyboard;
        TMP_InputField rememberedInput;
        bool restoreFocus,rememberedInputEnabled,inputSuspended;
        public bool HasUnsavedWork=>Hud.Input.IsEditing||(Hud.Input.Edits?.Count??0)>0||Hud.Sandbox.RailwayHud?.HasDraft==true;
        void OnEnable(){keyboard=Keyboard.current;if(keyboard!=null)keyboard.onIMECompositionChange+=Composition;}
        void OnDisable(){RestoreInput(false);if(keyboard!=null)keyboard.onIMECompositionChange-=Composition;Composing=false;IsOpen=false;opacity=0;if(Overlay!=null)Overlay.gameObject.SetActive(false);}
        void Composition(UnityEngine.InputSystem.LowLevel.IMECompositionString value){if(Composing&&value.Count==0)compositionEndedFrame=Time.frameCount;Composing=value.Count>0;}
        void Awake(){Overlay.alpha=0;Overlay.gameObject.SetActive(false);}
        void Update()
        {
            if(Keyboard.current?.escapeKey.wasPressedThisFrame==true)Escape();
            Advance(Time.unscaledDeltaTime);
        }
        public void Escape()
        {
            consumedFrame=Time.frameCount;
            if(Composing||compositionEndedFrame==Time.frameCount)return;
            if(IsOpen&&LoadConfirmation.activeSelf){LoadConfirmation.SetActive(false);return;}
            if(IsOpen&&NewGameConfirmation.activeSelf){NewGameConfirmation.SetActive(false);return;}
            if(IsOpen&&Developer.activeSelf){Developer.SetActive(false);return;}
            SetOpen(!IsOpen);
        }
        public void SetOpen(bool open)
        {
            if(IsOpen==open)return;
            consumedFrame=Time.frameCount;
            if(open){
                // Keep the original field across close/reopen while the modal is still fading.
                if(!inputSuspended){rememberedInput=EventSystem.current?.currentSelectedGameObject?.GetComponent<TMP_InputField>();
                    if(rememberedInput!=null){rememberedInputEnabled=rememberedInput.enabled;rememberedInput.DeactivateInputField();rememberedInput.enabled=false;inputSuspended=true;}}
                EventSystem.current?.SetSelectedGameObject(null);
                Hud.Input.AbortPointer();Hud.Minimap.CancelInteraction();
                LoadConfirmation.SetActive(false);NewGameConfirmation.SetActive(false);Developer.SetActive(false);
                Overlay.gameObject.SetActive(true);Overlay.transform.SetAsLastSibling();
            }
            IsOpen=open;restoreFocus=!open;Overlay.blocksRaycasts=true;Overlay.interactable=open;
        }
        public void Advance(float seconds)
        {
            opacity=Mathf.MoveTowards(opacity,IsOpen?1:0,Mathf.Max(0,seconds)/.18f);
            Overlay.alpha=opacity*opacity*(3-2*opacity);
            Overlay.interactable=IsOpen&&opacity>=1;
            Content.interactable=Content.blocksRaycasts=!LoadConfirmation.activeSelf&&!Developer.activeSelf;
            if(!IsOpen&&opacity<=0){Overlay.gameObject.SetActive(false);if(restoreFocus){restoreFocus=false;RestoreInput(true);}}
            UpdateGeometry();
        }
        void RestoreInput(bool focus)
        {
            if(!inputSuspended)return;
            inputSuspended=false;
            if(rememberedInput!=null){
                rememberedInput.enabled=rememberedInputEnabled;
                if(focus&&rememberedInputEnabled&&rememberedInput.gameObject.activeInHierarchy){EventSystem.current?.SetSelectedGameObject(rememberedInput.gameObject);rememberedInput.ActivateInputField();}
            }
            rememberedInput=null;
        }
        void LateUpdate(){UpdateGeometry();}
        void UpdateGeometry()
        {
            float scale=Mathf.Max(.01f,Hud.Layout.Canvas.scaleFactor);
            var pixels=Hud.Layout.Root.rect.size*scale;
            Panel.localScale=Vector3.one/scale;
            ((RectTransform)LoadConfirmation.transform).localScale=Vector3.one/scale;
            ((RectTransform)Developer.transform).localScale=Vector3.one/scale;
            float contentHeight=0;
            if(SaveSection.gameObject.activeSelf){SaveSection.anchoredPosition=Vector2.zero;contentHeight=SaveSection.rect.height+8;}
            if(ProgressSection.gameObject.activeSelf){ProgressSection.anchoredPosition=new Vector2(0,-contentHeight);contentHeight+=ProgressSection.rect.height+8;}
            DeveloperEntry.anchoredPosition=new Vector2(0,-contentHeight);contentHeight+=DeveloperEntry.rect.height+8;
            Scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,contentHeight);
            Panel.sizeDelta=new Vector2(Mathf.Min(580,pixels.x-32),Mathf.Min(Mathf.Min(600,pixels.y-48),Mathf.Max(170,contentHeight+116)));
            if(Heading!=null)Heading.text=Hud.Sandbox.Clock!=null&&Hud.Sandbox.Clock.Paused?"메뉴 · 기존 일시정지 유지":"메뉴 · 일시정지 중";
        }
        public void Execute(string command)
        {
            if(command=="close"){SetOpen(false);return;}
            if(!IsOpen)return;
            if(command=="load"){
                if(Hud.Sandbox.Persistence?.HasContinue!=true)return;
                if(HasUnsavedWork){LoadConfirmation.SetActive(true);return;}
                Hud.Sandbox.Persistence.ContinueSaved();
            }
            else if(command=="load-confirm"&&LoadConfirmation.activeSelf)Hud.Sandbox.Persistence?.ContinueSaved();
            else if(command=="load-cancel")LoadConfirmation.SetActive(false);
        }
        public bool AllowsHudCommand(string command)
        {
            if(IsOpen&&LoadConfirmation.activeSelf)return false;
            if(command=="new-game-confirm"&&!NewGameConfirmation.activeSelf)return false;
            return !BlocksInput||command is "pause" or "save" or "load" or "new-game" or "new-game-confirm" or "new-game-cancel" or "fold:menu" or "fold:developer-body"||Developer.activeInHierarchy&&(command is "run" or "spawn" or "spawn-air" or "reset" or "infinite-resources" or "day" or "night");
        }
    }
}
