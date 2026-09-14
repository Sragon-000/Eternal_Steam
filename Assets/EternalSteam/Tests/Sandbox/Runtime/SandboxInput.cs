using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace EternalSteam
{
    public sealed class SandboxInput : MonoBehaviour
    {
        public FoundationSandbox Sample;
        public UIDocument Document;
        public BuildingDefinition SelectedDefinition { get; set; }
        public int? SelectedId { get; set; }
        public int? SelectedPendingId { get; set; }
        public bool Moving { get; set; }
        public string Message { get; set; } = "카탈로그에서 건물을 선택하세요.";
        public event System.Action Changed;
        public void NotifyChanged() => Changed?.Invoke();
        public void ClearSelection() { SelectedId = SelectedPendingId = null; Moving = false; }
        void Update()
        {
            var session = Sample.Session;
            if (session == null) return;
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || Mouse.current?.rightButton.wasPressedThisFrame == true)
            { session.Cancel(); ClearSelection(); Message = "작업을 취소했습니다."; NotifyChanged(); }
            if (!session.CanEdit || Mouse.current == null || Sample.ViewCamera == null) return;
            var mouse = Mouse.current.position.ReadValue();
            var root = Document.rootVisualElement;
            var sidebar = root.Q("sidebar");
            var panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(mouse.x, Screen.height - mouse.y));
            if (sidebar != null && sidebar.worldBound.Contains(panelPosition)) return;
            var ray = Sample.ViewCamera.ScreenPointToRay(mouse);
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance)) return;
            var point = ray.GetPoint(distance);
            if (session.Mode == EditMode.Direction) { session.Aim(point); return; }
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            var cell = session.World.Grid.WorldToCell(point);
            if (session.Mode == EditMode.Placement)
            {
                if (Moving && SelectedPendingId.HasValue)
                {
                    var moved = session.Move(SelectedPendingId.Value, cell);
                    Message = moved.Success ? "이동했습니다." : moved.Message;
                    if (moved.Success) Moving = false;
                }
                else if (session.World.Grid.ReservationAt(cell) is int pending)
                { SelectedPendingId = pending; Message = "임시 건물 선택 — 이동 또는 제거할 수 있습니다."; }
                else
                {
                    var result = session.Add(SelectedDefinition, cell, out var request);
                    if (result.Success) SelectedPendingId = request.Id;
                    Message = result.Success ? "임시 배치됨 — 추가 배치하거나 확정하세요." : result.Message;
                }
            }
            else if (session.World.Grid.OccupantAt(cell) is int id)
            {
                SelectedId = id;
                if (session.Mode == EditMode.Recovery) Message = session.ToggleRecovery(id) ? "회수 선택을 변경했습니다." : "회수할 수 없는 건물입니다.";
            }
            else SelectedId = null;
            NotifyChanged();
        }
    }
}
