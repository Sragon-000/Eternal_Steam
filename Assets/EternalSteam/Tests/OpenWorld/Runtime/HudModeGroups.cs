using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
namespace EternalSteam.OpenWorld
{
    // The hierarchy and slide hosts are authored. Switching only updates state and transforms.
    [DefaultExecutionOrder(-200)]
    public sealed class HudModeGroups:MonoBehaviour
    {
        [Serializable] public sealed class Slide { public RectTransform Host;public Vector2 Direction; [NonSerialized] public float Distance; }
        public CanvasWorldHud Hud;
        public CanvasGroup Combat,Construction;
        public Slide[] CombatSlides=Array.Empty<Slide>(),ConstructionSlides=Array.Empty<Slide>();
        public TMP_Text ModeLabel;
        public float Duration=.24f;
        public bool ConstructionSelected {get;private set;}
        public bool Transitioning {get;private set;}
        public bool CanUseConstruction=>ConstructionSelected&&!Transitioning;
        float progress,from,to,elapsed;
        Vector2 measuredSize;
        readonly Vector3[] corners=new Vector3[4];
        bool deferredToggle,composing;
        Keyboard keyboard;
        TMP_InputField rememberedInput;bool restoreInput;
        void OnEnable(){keyboard=Keyboard.current;if(keyboard!=null)keyboard.onIMECompositionChange+=Composition;}
        void OnDisable(){if(keyboard!=null)keyboard.onIMECompositionChange-=Composition;}
        void Composition(UnityEngine.InputSystem.LowLevel.IMECompositionString value){composing=value.Count>0;}
        public void Toggle()=>Select(!ConstructionSelected);
        public void Select(bool construction)
        {
            if(Hud?.Sandbox?.PauseMenu?.BlocksInput==true||ConstructionSelected==construction)return;
            if(Hud.Input.Dragging){Hud.Sandbox.Message="드래그를 마친 뒤 UI를 전환하세요.";return;}
            var selected=EventSystem.current?.currentSelectedGameObject;
            if(selected!=null&&selected.TryGetComponent<TMP_InputField>(out var input)){if(!construction&&input.transform.IsChildOf(Construction.transform))rememberedInput=input;input.DeactivateInputField();}
            restoreInput=construction&&rememberedInput!=null;
            EventSystem.current?.SetSelectedGameObject(null);
            Hud.Minimap.CancelInteraction();
            ConstructionSelected=construction;from=progress;to=construction?1:0;elapsed=0;Transitioning=true;
            Combat.gameObject.SetActive(true);Construction.gameObject.SetActive(true);
            Apply();
        }
        void Awake(){progress=0;ConstructionSelected=false;Transitioning=false;Apply();}
        void Update()
        {
            if(Hud?.Sandbox?.PauseMenu?.BlocksInput==true){deferredToggle=false;Advance(Time.unscaledDeltaTime);return;}
            if(Keyboard.current?.tabKey.wasPressedThisFrame==true)deferredToggle=!deferredToggle;
            // An IME composition must finish before focus is moved away from its input.
            if(deferredToggle&&!composing){deferredToggle=false;Toggle();}
            Advance(Time.unscaledDeltaTime);
        }
        public void Advance(float delta)
        {
            if(Transitioning){elapsed+=Mathf.Max(0,delta);float t=Mathf.Clamp01(elapsed/Mathf.Max(.01f,Duration));float ease=t*t*(3-2*t);progress=Mathf.Lerp(from,to,ease);if(t>=1){progress=to;Transitioning=false;}}
            Apply();
        }
        void Apply()
        {
            if(Combat==null||Construction==null||Hud==null)return;
            Combat.interactable=Combat.blocksRaycasts=!ConstructionSelected&&!Transitioning;
            Construction.interactable=Construction.blocksRaycasts=CanUseConstruction;
            Combat.gameObject.SetActive(progress<1||Transitioning);Construction.gameObject.SetActive(progress>0||Transitioning);
            Position(CombatSlides,progress);Position(ConstructionSlides,1-progress);measuredSize=Hud.Layout.Root.rect.size;
            if(ModeLabel!=null)ModeLabel.text=ConstructionSelected?"Tab · 전투 화면":"Tab · 건설 화면";
        }
        void LateUpdate(){Apply();if(Hud?.Sandbox?.PauseMenu?.BlocksInput!=true&&restoreInput&&CanUseConstruction&&rememberedInput!=null&&rememberedInput.gameObject.activeInHierarchy){restoreInput=false;EventSystem.current?.SetSelectedGameObject(rememberedInput.gameObject);rememberedInput.ActivateInputField();}}
        void Position(Slide[] slides,float hidden)
        {
            var root=Hud.Layout.Root;
            foreach(var slide in slides)if(slide.Host!=null){
                if(slide.Distance<=0||measuredSize!=root.rect.size){
                    slide.Host.anchoredPosition=Vector2.zero;var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
                    foreach(var graphic in slide.Host.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)){graphic.rectTransform.GetWorldCorners(corners);foreach(var point in corners){var v=(Vector2)root.InverseTransformPoint(point);min=Vector2.Min(min,v);max=Vector2.Max(max,v);}}
                    var r=root.rect;float distance=slide.Direction.x<0?max.x-r.xMin:slide.Direction.x>0?r.xMax-min.x:slide.Direction.y<0?max.y-r.yMin:r.yMax-min.y;
                    slide.Distance=float.IsFinite(distance)?Mathf.Max(0,distance)+24:0;
                }
                slide.Host.anchoredPosition=slide.Direction*slide.Distance*hidden;
            }
        }
    }
}
