using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace EternalSteam
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class FoundationHud : MonoBehaviour
    {
        public FoundationSandbox Sample;
        public SandboxInput Input;
        VisualElement root;
        readonly List<Button> catalogButtons = new();
        int previousRemaining = -1;
        SandboxPhase previousPhase;

        void Start()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            Bind("install", () => { Sample.Session.BeginPlacement(); Input.ClearSelection(); });
            Bind("recover", () => { Sample.Session.BeginRecovery(); Input.ClearSelection(); });
            Bind("confirm", () =>
            {
                var result = Sample.Session.Confirm(); Input.Message = result.Success ? "확정했습니다." : result.Message;
                if (result.Success) Input.ClearSelection();
            });
            Bind("cancel", () => { Sample.Session.Cancel(); Input.ClearSelection(); Input.Message = "작업을 취소했습니다."; });
            Bind("move", () => { Input.Moving = true; Input.Message = "이동할 격자를 클릭하세요."; });
            Bind("remove", () => { if (Input.SelectedPendingId.HasValue) Sample.Session.Placement.Remove(Input.SelectedPendingId.Value); Input.SelectedPendingId = null; });
            Bind("direction", () => { if (Input.SelectedId.HasValue) Sample.Session.BeginDirection(Input.SelectedId.Value); });
            Bind("ground", () => SetTargets(TargetKind.Ground));
            Bind("air", () => SetTargets(TargetKind.Air));
            Bind("both", () => SetTargets(TargetKind.All));
            Bind("start", () => { if (Sample.StartCombat()) Input.Message = "전투 중에는 편집할 수 없습니다."; });
            Bind("reset", () => { Input.ClearSelection(); Sample.ResetSample(); RebuildCatalog(); Input.Message = "검증 씬을 초기화했습니다."; });
            Input.Changed += Refresh;
            RebuildCatalog(); Refresh();
        }
        void Bind(string name, System.Action action)
        { root.Q<Button>(name).clicked += () => { if (Sample.Session != null || name == "reset") action(); Refresh(); }; }
        void SetTargets(TargetKind kinds)
        { if (Input.SelectedId.HasValue) Sample.Session.SetTargets(Input.SelectedId.Value, kinds); }
        public void RebuildCatalog()
        {
            if (root == null) return;
            var catalog = root.Q("catalog"); catalog.Clear(); catalogButtons.Clear();
            foreach (var definition in Sample.Available)
            {
                var captured = definition;
                var button = new Button(() => { Input.SelectedDefinition = captured; Input.SelectedPendingId = null; Input.Moving = false; Refresh(); })
                { text = definition.DisplayName + $"  /  {definition.Footprint.x}×{definition.Footprint.y}" };
                catalog.Add(button); catalogButtons.Add(button);
            }
            Input.SelectedDefinition = Sample.Available.Count > 0 ? Sample.Available[0] : null;
        }
        void Visible(string name, bool value) => root.Q(name).EnableInClassList("hidden", !value);
        void Refresh()
        {
            if (root == null) return;
            if (Sample.Session == null) { root.Q<Label>("message").text = Sample.Error; return; }
            var session = Sample.Session;
            bool editing = session.Mode != EditMode.None;
            root.Q<Label>("phase").text = session.Phase == SandboxPhase.Preparation ? "01 / 준비" : session.Phase == SandboxPhase.Combat ? $"02 / 전투 · 남은 적 {Sample.Remaining}" : $"03 / 결과 · 처치 {8-Sample.Escaped} · 통과 {Sample.Escaped}";
            Visible("install", session.CanEdit && !editing); Visible("recover", session.CanEdit && !editing);
            Visible("confirm", session.CanEdit && editing); Visible("cancel", session.CanEdit && editing);
            root.Q<Button>("confirm").SetEnabled(session.Mode == EditMode.Direction || session.Placement.Pending.Count > 0 || session.Recovery.Selected.Count > 0);
            root.Q<Button>("start").SetEnabled(session.CanEdit && !editing);
            root.Q<Button>("reset").SetEnabled(session.Phase != SandboxPhase.Combat);
            for (int i = 0; i < catalogButtons.Count; i++)
            {
                catalogButtons[i].SetEnabled(session.CanEdit && (session.Mode == EditMode.None || session.Mode == EditMode.Placement));
                catalogButtons[i].EnableInClassList("selected", Input.SelectedDefinition == Sample.Available[i]);
            }
            BuildingInstance selected = null;
            if (Input.SelectedId.HasValue) session.World.TryGet(Input.SelectedId.Value, out selected);
            var attack = selected?.Module<AttackModule>();
            string targetLabel = attack == null ? "" : attack.Kinds == TargetKind.Ground ? "지상" : attack.Kinds == TargetKind.Air ? "공중" : "지상·공중";
            root.Q<Label>("selection").text = selected == null ? "격자의 건물을 클릭하면 선택됩니다." : selected.DisplayName + (attack == null ? " / 공격 기능 없음" : " / 대상 " + targetLabel);
            Visible("selectionActions", session.CanEdit && !editing && attack != null);
            Visible("move", session.Mode == EditMode.Placement && Input.SelectedPendingId.HasValue);
            Visible("remove", session.Mode == EditMode.Placement && Input.SelectedPendingId.HasValue);
            var pending = root.Q("pending"); pending.Clear();
            foreach (var request in session.Placement.Pending)
            {
                int id = request.Id;
                var result = session.Placement.Validate(request);
                pending.Add(new Button(() => { Input.SelectedPendingId = id; Input.Moving = false; Refresh(); })
                { text = request.Definition.DisplayName + " " + request.Cell + (result.Success ? "" : " · 설치 불가") });
            }
            foreach (int selectedId in session.Recovery.Selected)
            {
                int id = selectedId;
                pending.Add(new Button(() => { session.Recovery.Deselect(id); Refresh(); })
                { text = (session.World.TryGet(id, out var item) ? item.DisplayName : "사라진 대상") + " · 회수 해제" });
            }
            root.Q<Label>("message").text = session.Mode == EditMode.Direction ? "마우스로 방향을 정한 뒤 확정하세요. 취소하면 기존 방향을 유지합니다." : Input.Message;
        }
        void Update()
        {
            if (root == null || Sample.Session == null) return;
            if (previousPhase != Sample.Session.Phase || previousRemaining != Sample.Remaining)
            { previousPhase = Sample.Session.Phase; previousRemaining = Sample.Remaining; Refresh(); }
        }
        void OnDestroy() { if (Input != null) Input.Changed -= Refresh; }
    }
}
