using System;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;

public static class VerifyA5MovingBranch
{
    const string Root = "/tmp/eternal-a5-branch-live";
    static readonly Vector2Int[] Directions = {Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down};
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static string Main()
    {
        Check(Application.isPlaying && Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT") == Root,
            "Isolated Play fixture required");
        var sandbox = UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        Check(sandbox != null && !sandbox.Persistence.Blocked, "Live sandbox required");
        sandbox.Persistence.Automatic = false;
        sandbox.Clock.Paused = true;
        var input = sandbox.GetComponent<OpenWorldInput>();
        input.Cancel();
        var content = sandbox.Content;
        var network = content.Railway.Network;
        var route = network.Routes.Single();
        Check(route.train.status == TrainStatus.Dwelling && !route.train.segmentPaid, "Dwelling route required");
        Check(route.train.HasCargoResource, "Configured cargo required");
        content.Railway.Tick(11);
        Check(route.train.status == TrainStatus.Moving && route.train.segmentPaid && route.train.progress > 0,
            "Moving train required");
        var fuel = route.train.fuel;
        var cargo = route.train.cargo;
        var progress = route.train.progress;
        var stop = route.train.stop;

        var used = route.legs.SelectMany(leg => leg.cells).ToHashSet();
        var track = sandbox.RailwayHud.Track;
        var owner = content.GroundWorld.Buildings.First(b => b.Module<RailFacility>()?.Kind == RailFacilityKind.Track).OwnerBaseId;
        Check(content.Bases.Select(owner), "Track owner's base unavailable");
        var bank = content.Inventories.Available(owner);
        Check(bank != null, "Track owner's inventory unavailable");
        if (bank.Amount("iron") < 1) bank.Deposit("iron", 1);
        var iron = bank.Amount("iron");
        input.BeginEditing();
        Vector2Int? branchCell = null;
        foreach (var leg in route.legs)
        {
            for (var i = 1; i < leg.cells.Count - 1 && !branchCell.HasValue; i++)
            {
                foreach (var direction in Directions)
                {
                    var candidate = leg.cells[i] + direction;
                    if (used.Contains(candidate) || Directions.Count(d => used.Contains(candidate + d)) != 1) continue;
                    if (!content.GroundPlacement.Validate(new PlacementRequest(-1, track, candidate)).Success) continue;
                    if (!input.Edits.AddContent(track, content.GroundWorld.Grid.Center(candidate, Vector2Int.one),
                            WorldGridGeometry.Rotation * Vector3.forward, out _)) continue;
                    branchCell = candidate;
                    break;
                }
            }
            if (branchCell.HasValue) break;
        }
        Check(branchCell.HasValue, "No valid adjacent branch cell in live scene");
        Check(input.Confirm(), sandbox.Message);
        input.Cancel();
        content.Railway.Refresh();
        var branch = content.GroundWorld.Buildings.Single(b => b.Cell == branchCell.Value);
        Check(bank.Amount("iron") == iron - 1, "Branch installation must charge one iron");
        Check(route.train.status == TrainStatus.RouteError && route.error.Contains("분기"),
            "Added branch must stop the invalid route: " + route.error);
        Check(route.train.fuel == fuel && route.train.cargo == cargo && route.train.progress == progress && route.train.stop == stop,
            "Route error must preserve paid fuel, cargo and position");
        content.Railway.Tick(30);
        Check(route.train.progress == progress && route.train.fuel == fuel, "Invalid route must remain stopped");

        input.BeginEditing();
        Check(input.Edits.ToggleGroundRecovery(branch, out var error), error);
        Check(input.Confirm(), sandbox.Message);
        input.Cancel();
        content.Railway.Refresh();
        Check(branch.Disposed && network.ValidateExisting(route, out error), "Branch recovery must restore route validity: " + error);
        Check(network.Start(route, out error), error);
        Check(route.train.status == TrainStatus.Moving && route.train.progress == progress &&
              route.train.fuel == fuel && route.train.cargo == cargo,
            "Manual resume must not charge fuel or transfer cargo twice");
        content.Railway.Tick(.5);
        Check(route.train.progress > progress, "Recovered train did not continue from its paid position");
        return $"PASS branchCell={branchCell.Value}; stoppedOnBranch=true; preservedFuelCargoProgress=true; manualResumeNoDoubleDebit=true";
    }
}
