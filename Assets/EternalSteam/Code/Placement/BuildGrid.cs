using System;
using System.Collections.Generic;
using UnityEngine;

namespace EternalSteam
{
    public readonly struct PlacementResult
    {
        public bool Success => Code == null;
        public readonly string Code;
        public readonly string Message;
        public readonly IReadOnlyList<Vector2Int> Cells;
        public PlacementResult(string code, string message, params Vector2Int[] cells)
        { Code = code; Message = message; Cells = cells; }
        public static PlacementResult Ok => default;
    }
    public sealed class PlacementRequest
    {
        public int Id { get; }
        public BuildingDefinition Definition { get; }
        public Vector2Int Cell { get; internal set; }
        public Vector2Int Footprint { get; }
        public Vector3 Direction { get; set; } = Vector3.forward;
        public PlacementRequest(int id, BuildingDefinition definition, Vector2Int cell)
        { Id = id; Definition = definition; Cell = cell; Footprint = definition.Footprint; }
    }
    public interface IPlacementRule
    {
        PlacementResult Validate(PlacementRequest request, IReadOnlyList<PlacementRequest> batch, BuildGrid grid);
    }
    public sealed class TerrainRule : IPlacementRule
    {
        public PlacementResult Validate(PlacementRequest request, IReadOnlyList<PlacementRequest> batch, BuildGrid grid)
        {
            foreach (var cell in grid.Cells(request.Cell, request.Footprint))
                if (!grid.IsBuildable(cell)) return new PlacementResult("terrain", "설치 불가 격자입니다.", cell);
            return PlacementResult.Ok;
        }
    }
    public sealed class OccupancyRule : IPlacementRule
    {
        public PlacementResult Validate(PlacementRequest request, IReadOnlyList<PlacementRequest> batch, BuildGrid grid)
        {
            foreach (var cell in grid.Cells(request.Cell, request.Footprint))
                if (grid.IsOccupied(cell) || (grid.ReservationAt(cell) is int owner && owner != request.Id))
                    return new PlacementResult("occupied", "다른 건물 또는 임시 예약과 겹칩니다.", cell);
            return PlacementResult.Ok;
        }
    }
    public sealed class BuildGrid
    {
        readonly Dictionary<Vector2Int, int> reservations = new();
        readonly Dictionary<Vector2Int, int> occupied = new();
        readonly HashSet<Vector2Int> blocked = new();
        public RectInt Bounds { get; }
        public float CellSize { get; }
        public Vector3 Origin { get; }
        public Quaternion Rotation { get; }
        public float Yaw { get; }
        public int ReservationCount => reservations.Count;
        public int OccupiedCount => occupied.Count;
        public BuildGrid(RectInt bounds, float cellSize, Vector3 origin = default, float yaw = 0)
        {
            if (bounds.width <= 0 || bounds.height <= 0 || !float.IsFinite(cellSize) || cellSize <= 0) throw new ArgumentException("Invalid grid.");
            if (!float.IsFinite(origin.x) || !float.IsFinite(origin.y) || !float.IsFinite(origin.z)) throw new ArgumentException("Invalid grid origin.");
            if (!float.IsFinite(yaw)) throw new ArgumentException("Invalid grid rotation.");
            Bounds = bounds; CellSize = cellSize; Origin = origin; Yaw = yaw; Rotation = Quaternion.Euler(0,yaw,0);
        }
        public Vector2Int WorldToCell(Vector3 position)
        { var local = Quaternion.Inverse(Rotation) * (position - Origin); return new Vector2Int(Mathf.FloorToInt(local.x / CellSize), Mathf.FloorToInt(local.z / CellSize)); }
        public Vector3 Center(Vector2Int cell, Vector2Int size) => Origin + Rotation * new Vector3((cell.x + size.x * 0.5f) * CellSize, 0, (cell.y + size.y * 0.5f) * CellSize);
        public IEnumerable<Vector2Int> Cells(Vector2Int cell, Vector2Int size)
        {
            for (int z = 0; z < size.y; z++) for (int x = 0; x < size.x; x++) yield return cell + new Vector2Int(x, z);
        }
        public bool IsBuildable(Vector2Int cell) => Bounds.Contains(cell) && !blocked.Contains(cell);
        public void SetBlocked(Vector2Int cell, bool value) { if (value) blocked.Add(cell); else blocked.Remove(cell); }
        public bool IsOccupied(Vector2Int cell) => occupied.ContainsKey(cell);
        public int? OccupantAt(Vector2Int cell) => occupied.TryGetValue(cell, out var id) ? id : null;
        public int? ReservationAt(Vector2Int cell) => reservations.TryGetValue(cell, out var id) ? id : null;
        internal void Reserve(PlacementRequest request)
        { foreach (var cell in Cells(request.Cell, request.Footprint)) reservations.Add(cell, request.Id); }
        internal void ReleaseReservation(PlacementRequest request)
        {
            foreach (var cell in Cells(request.Cell, request.Footprint))
                if (ReservationAt(cell) == request.Id) reservations.Remove(cell);
        }
        internal void Occupy(BuildingInstance building)
        { foreach (var cell in Cells(building.Cell, building.Footprint)) occupied.Add(cell, building.Id); }
        internal void Release(BuildingInstance building)
        {
            foreach (var cell in Cells(building.Cell, building.Footprint))
                if (OccupantAt(cell) == building.Id) occupied.Remove(cell);
        }
    }
}
