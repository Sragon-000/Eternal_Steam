using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
namespace EternalSteam.OpenWorld
{
    // A quantity field is editable only after an explicit pointer click, never UI navigation.
    public sealed class SpawnQuantityInput : IDisposable
    {
        readonly TextField field;
        readonly VisualElement root;
        readonly List<VisualElement> focusables = new();
        string lastValid;
        public bool Editing { get; private set; }
        public SpawnQuantityInput(VisualElement root, TextField field)
        {
            this.root=root;this.field=field;lastValid=field.value;
            field.Query<VisualElement>().ForEach(element=>{if(element.focusable)focusables.Add(element);});
            if(field.focusable&&!focusables.Contains(field))focusables.Add(field);
            field.maxLength=9;
            field.RegisterCallback<PointerDownEvent>(OnPointer,TrickleDown.TrickleDown);
            field.RegisterCallback<KeyDownEvent>(OnKey,TrickleDown.TrickleDown);
            field.RegisterValueChangedCallback(OnValue);
            root.RegisterCallback<NavigationMoveEvent>(OnNavigation,TrickleDown.TrickleDown);
            EndEdit();
        }
        void OnPointer(PointerDownEvent e){if(e.button!=0)return;Editing=true;field.isReadOnly=false;foreach(var element in focusables)element.focusable=true;field.Focus();}
        void OnNavigation(NavigationMoveEvent e){e.StopImmediatePropagation();e.PreventDefault();}
        void OnKey(KeyDownEvent e)
        {
            if(e.keyCode==UnityEngine.KeyCode.Return||e.keyCode==UnityEngine.KeyCode.KeypadEnter||e.keyCode==UnityEngine.KeyCode.Escape){EndEdit();e.StopImmediatePropagation();e.PreventDefault();return;}
            if(!Editing || (e.character>=' ' && (e.character<'0'||e.character>'9') && !e.ctrlKey && !e.commandKey)) {e.StopImmediatePropagation();e.PreventDefault();}
        }
        void OnValue(ChangeEvent<string> e)
        {
            foreach(char c in e.newValue)if(c<'0'||c>'9'){field.SetValueWithoutNotify(lastValid);return;}
            lastValid=e.newValue;
        }
        public void EndEdit()
        {
            Editing=false;field.isReadOnly=true;
            var focused=field.panel?.focusController.focusedElement as VisualElement;
            if(focused==field || focused?.GetFirstAncestorOfType<TextField>()==field)focused.Blur();
            foreach(var element in focusables)element.focusable=false;
        }
        public void Dispose()
        {
            EndEdit();field.UnregisterCallback<PointerDownEvent>(OnPointer,TrickleDown.TrickleDown);
            field.UnregisterCallback<KeyDownEvent>(OnKey,TrickleDown.TrickleDown);field.UnregisterValueChangedCallback(OnValue);
            root.UnregisterCallback<NavigationMoveEvent>(OnNavigation,TrickleDown.TrickleDown);
        }
    }
}
