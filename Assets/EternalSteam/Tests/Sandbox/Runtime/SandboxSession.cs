using System;
using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam
{
    public enum SandboxPhase { Preparation, Combat, Result }
    public enum EditMode { None, Placement, Recovery, Direction }
    public interface IEditPolicy { bool Allows(SandboxPhase phase); }
    public sealed class PreparationOnly : IEditPolicy
    { public bool Allows(SandboxPhase phase) => phase == SandboxPhase.Preparation; }
    public interface ISandboxCommands
    {
        void BeginPlacement();
        void BeginRecovery();
        PlacementResult Confirm();
        void Cancel();
        bool StartCombat();
    }

    public sealed class SandboxSession : ISandboxCommands, IDisposable
    {
        readonly IEditPolicy editPolicy;
        readonly List<BuildingInstance> tickBuffer = new();
        BuildingInstance directionBuilding;
        public BuildingWorld World { get; }
        public PlacementSession Placement { get; }
        public RecoverySession Recovery { get; }
        public SandboxPhase Phase { get; private set; }
        public EditMode Mode { get; private set; }
        public Vector3 PreviewDirection { get; private set; } = Vector3.forward;
        public int? DirectionBuildingId => directionBuilding?.Id;
        public bool CanEdit => editPolicy.Allows(Phase);
        public SandboxSession(BuildingWorld world, IEditPolicy policy)
        { World = world; editPolicy = policy; Placement = new PlacementSession(world); Recovery = new RecoverySession(world); }
        public void BeginPlacement() { if (CanEdit && Mode == EditMode.None) Mode = EditMode.Placement; }
        public void BeginRecovery() { if (CanEdit && Mode == EditMode.None) Mode = EditMode.Recovery; }
        public bool BeginDirection(int id)
        {
            if (!CanEdit || Mode != EditMode.None || !World.TryGet(id, out directionBuilding) || directionBuilding.Module<AttackModule>() == null) return false;
            Mode = EditMode.Direction;
            PreviewDirection = directionBuilding.Direction;
            directionBuilding.BeginDirectionEdit();
            return true;
        }
        public void Aim(Vector3 worldPosition)
        {
            if (Mode != EditMode.Direction || directionBuilding == null) return;
            var delta = worldPosition - directionBuilding.Position;
            delta.y = 0;
            if (float.IsFinite(delta.sqrMagnitude) && delta.sqrMagnitude > 0.001f) PreviewDirection = delta.normalized;
        }
        public PlacementResult Add(BuildingDefinition definition, Vector2Int cell, out PlacementRequest request)
        {
            request = null;
            return CanEdit && Mode == EditMode.Placement ? Placement.Add(definition, cell, out request) : new PlacementResult("mode", "설치 모드가 아닙니다.");
        }
        public PlacementResult Move(int id, Vector2Int cell) => CanEdit && Mode == EditMode.Placement
            ? Placement.Move(id, cell) : new PlacementResult("mode", "설치 모드가 아닙니다.");
        public bool ToggleRecovery(int id) => CanEdit && Mode == EditMode.Recovery && Recovery.Toggle(id);
        public void SetTargets(int id, TargetKind kinds)
        {
            if (!CanEdit || Mode != EditMode.None || !World.TryGet(id, out var building)) return;
            if (building.Module<AttackModule>() is AttackModule attack) attack.Kinds = kinds;
        }
        public PlacementResult Confirm()
        {
            if (!CanEdit) return new PlacementResult("phase", "준비 단계에서만 편집할 수 있습니다.");
            PlacementResult result;
            switch (Mode)
            {
                case EditMode.Placement: result = Placement.Confirm(); break;
                case EditMode.Recovery: result = Recovery.Confirm(); break;
                case EditMode.Direction:
                    if (directionBuilding == null || directionBuilding.Disposed) return new PlacementResult("missing", "방향 변경 대상이 없습니다.");
                    directionBuilding.ConfirmDirection(PreviewDirection);
                    directionBuilding = null;
                    result = PlacementResult.Ok;
                    break;
                default: return new PlacementResult("mode", "진행 중인 작업이 없습니다.");
            }
            if (result.Success) Mode = EditMode.None;
            return result;
        }
        public void Cancel()
        {
            Placement.Cancel(); Recovery.Cancel();
            directionBuilding?.CancelDirectionEdit();
            directionBuilding = null;
            Mode = EditMode.None;
        }
        public bool StartCombat()
        {
            if (Phase != SandboxPhase.Preparation || Mode != EditMode.None || Placement.Pending.Count > 0 || Recovery.Selected.Count > 0) return false;
            Phase = SandboxPhase.Combat;
            return true;
        }
        public void Tick(float dt)
        {
            if (Phase != SandboxPhase.Combat) return;
            tickBuffer.Clear(); tickBuffer.AddRange(World.Buildings);
            foreach (var building in tickBuffer) building.Tick(dt);
        }
        public void CompleteCombat() { if (Phase == SandboxPhase.Combat) Phase = SandboxPhase.Result; }
        public void Dispose() { Cancel(); World.Dispose(); }
    }
}
