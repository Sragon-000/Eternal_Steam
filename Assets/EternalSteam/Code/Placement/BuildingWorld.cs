using System;
using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam
{
    // Factories must stage inactive views. Activate must not publish irreversible gameplay effects.
    public interface IBuildingFactory
    {
        BuildingInstance Stage(int id, PlacementRequest request, Vector3 position);
        void Activate(BuildingInstance building);
        void Remove(BuildingInstance building);
    }
    public sealed class BuildingWorld : IDisposable
    {
        readonly Dictionary<int, BuildingInstance> buildings = new();
        readonly IBuildingFactory factory;
        int nextId;
        public BuildGrid Grid { get; }
        public IReadOnlyCollection<BuildingInstance> Buildings => buildings.Values;
        public BuildingWorld(BuildGrid grid, IBuildingFactory factory) { Grid = grid; this.factory = factory; }
        public int AllocateId() => ++nextId;
        public bool TryGet(int id, out BuildingInstance building) => buildings.TryGetValue(id, out building);
        internal PlacementResult Install(IReadOnlyList<PlacementRequest> requests)
        {
            var staged = new List<BuildingInstance>();
            try
            {
                foreach (var request in requests)
                {
                    var building = factory.Stage(AllocateId(), request, Grid.Center(request.Cell, request.Footprint));
                    staged.Add(building ?? throw new InvalidOperationException("Building factory returned null."));
                    building.ConfirmDirection(request.Direction);
                }
                foreach (var building in staged)
                {
                    buildings.Add(building.Id, building);
                    Grid.Occupy(building);
                    building.Destroyed += OnDestroyed;
                }
                foreach (var building in staged) { building.Activate(); factory.Activate(building); }
                return PlacementResult.Ok;
            }
            catch (Exception exception)
            {
                foreach (var building in staged)
                {
                    building.Destroyed -= OnDestroyed;
                    buildings.Remove(building.Id);
                    Grid.Release(building);
                    building.Dispose();
                    factory.Remove(building);
                }
                return new PlacementResult("creation", "생성 실패 — 임시 배치를 유지합니다: " + exception.Message);
            }
        }
        void OnDestroyed(BuildingInstance building) => Remove(building.Id);
        public void Remove(int id)
        {
            if (!buildings.Remove(id, out var building)) return;
            building.Destroyed -= OnDestroyed;
            Grid.Release(building);
            building.Dispose();
            factory.Remove(building);
        }
        public void Clear()
        { foreach (int id in new List<int>(buildings.Keys)) Remove(id); }
        public void Dispose() => Clear();
    }
    public sealed class PlacementSession : IDisposable
    {
        readonly BuildingWorld world;
        readonly List<IPlacementRule> rules;
        readonly List<PlacementRequest> pending = new();
        public IReadOnlyList<PlacementRequest> Pending => pending;
        public PlacementSession(BuildingWorld world, params IPlacementRule[] extraRules)
        {
            this.world = world;
            rules = new List<IPlacementRule> { new TerrainRule(), new OccupancyRule() };
            rules.AddRange(extraRules);
        }
        public PlacementResult Validate(PlacementRequest request)
        {
            if (request.Definition == null) return new PlacementResult("definition", "건물 정의가 없습니다.");
            var errors = request.Definition.Validate();
            if (errors.Count > 0) return new PlacementResult("definition", string.Join("\n", errors));
            if (request.Footprint != request.Definition.Footprint) return new PlacementResult("definition-changed", "점유 크기가 변경되었습니다. 임시 건물을 다시 배치하세요.");
            foreach (var rule in rules)
            {
                var result = rule.Validate(request, pending, world.Grid);
                if (!result.Success) return result;
            }
            return PlacementResult.Ok;
        }
        public PlacementResult Add(BuildingDefinition definition, Vector2Int cell, out PlacementRequest request)
        {
            request = null;
            if (definition == null) return new PlacementResult("definition", "건물을 선택하세요.");
            var candidate = new PlacementRequest(world.AllocateId(), definition, cell);
            pending.Add(candidate);
            var result = Validate(candidate);
            if (!result.Success) { pending.Remove(candidate); return result; }
            world.Grid.Reserve(candidate);
            request = candidate;
            return PlacementResult.Ok;
        }
        public PlacementResult Move(int id, Vector2Int cell)
        {
            var request = pending.Find(item => item.Id == id);
            if (request == null) return new PlacementResult("missing", "임시 건물이 없습니다.");
            var previous = request.Cell;
            request.Cell = cell;
            var result = Validate(request);
            request.Cell = previous;
            if (!result.Success) return result;
            world.Grid.ReleaseReservation(request);
            request.Cell = cell;
            world.Grid.Reserve(request);
            return result;
        }
        public void Remove(int id)
        {
            var request = pending.Find(item => item.Id == id);
            if (request == null) return;
            world.Grid.ReleaseReservation(request);
            pending.Remove(request);
        }
        public PlacementResult Confirm()
        {
            if (pending.Count == 0) return new PlacementResult("empty", "임시 건물이 없습니다.");
            foreach (var request in pending)
            { var result = Validate(request); if (!result.Success) return result; }
            var installed = world.Install(pending);
            if (installed.Success) Cancel();
            return installed;
        }
        public void Cancel()
        {
            foreach (var request in pending) world.Grid.ReleaseReservation(request);
            pending.Clear();
        }
        public void Dispose() => Cancel();
    }
    public sealed class RecoverySession
    {
        readonly BuildingWorld world;
        readonly HashSet<int> selected = new();
        public IReadOnlyCollection<int> Selected => selected;
        public RecoverySession(BuildingWorld world) { this.world = world; }
        public bool Toggle(int id)
        {
            if (selected.Remove(id)) return true;
            if (!world.TryGet(id, out var building) || !building.Recoverable) return false;
            return selected.Add(id);
        }
        public void Deselect(int id) => selected.Remove(id);
        public PlacementResult Confirm()
        {
            if (selected.Count == 0) return new PlacementResult("empty", "회수 대상을 선택하세요.");
            foreach (int id in selected)
                if (!world.TryGet(id, out var building) || building.Disposed || !building.Recoverable)
                    return new PlacementResult("recovery-invalid", "회수 대상이 없어졌거나 회수할 수 없습니다. 목록에서 해제하세요.");
            foreach (int id in selected) world.Remove(id);
            selected.Clear();
            return PlacementResult.Ok;
        }
        public void Cancel() => selected.Clear();
    }
}
