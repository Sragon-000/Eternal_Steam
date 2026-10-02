using System;
using System.Linq;
using UnityEngine;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;

public static class MeasureA5CurveTiming
{
    const string Root = "/tmp/eternal-a5-timing-live";
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
        sandbox.GetComponent<OpenWorldInput>().Cancel();
        var network = sandbox.Content.Railway.Network;
        var view = sandbox.RailwayView;
        var baseline = network.Capture();
        var original = network.Routes.Single();
        var seconds = original.ExpectedSeconds;
        var turns = original.legs.Sum(leg =>
            Enumerable.Range(1, leg.cells.Count - 2).Count(i =>
                leg.cells[i] - leg.cells[i - 1] != leg.cells[i + 1] - leg.cells[i]));
        Check(turns > 0 && seconds == 46, "Expected 46-second curved route fixture");

        network.Tick(seconds);
        var large = network.Routes.Single();
        var largeStatus = large.train.status;
        var largeStop = large.train.stop;
        var largeRevision = large.revision;
        var largeFuel = large.train.fuel;
        var largeCargo = large.train.cargo;
        var largeProgress = large.train.progress;
        var largeDwell = large.train.dwell;

        network.Restore(baseline);
        var small = network.Routes.Single();
        var previous = view.Pose(small, sandbox.Content).position;
        var largestStep = 0f;
        for (var i = 0; i < 460; i++)
        {
            network.Tick(.1);
            var position = view.Pose(small, sandbox.Content).position;
            largestStep = Mathf.Max(largestStep, Vector3.Distance(previous, position));
            previous = position;
        }
        var same = small.train.status == largeStatus && small.train.stop == largeStop &&
                   small.revision == largeRevision &&
                   Math.Abs(small.train.fuel - largeFuel) < .0001 &&
                   Math.Abs(small.train.cargo - largeCargo) < .0001 &&
                   Math.Abs(small.train.progress - largeProgress) < .0001 &&
                   Math.Abs(small.train.dwell - largeDwell) < .0001;

        network.Restore(baseline);
        var depart = network.Routes.Single();
        network.Tick(9.999);
        var beforeDeparture = view.Pose(depart, sandbox.Content).position;
        network.Tick(.001);
        var afterDeparture = view.Pose(depart, sandbox.Content).position;
        var departureJump = Vector3.Distance(beforeDeparture, afterDeparture);
        Check(same, "Large and small ticks reached different train states");
        Check(departureJump < .01f && largestStep < 1,
            $"Train pose jumped: departure={departureJump}, largest 0.1-second step={largestStep}");
        return $"PASS largeSmallEqual=true; turns={turns}; expectedSeconds={seconds}; largestPointOneStep={largestStep}; departureJump={departureJump}";
    }
}
