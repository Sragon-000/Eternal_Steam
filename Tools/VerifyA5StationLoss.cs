using System;
using System.Linq;
using UnityEngine;
using EternalSteam.OpenWorld;
using EternalSteam.Railway;

public static class VerifyA5StationLoss
{
    const string Root = "/tmp/eternal-a5-stationloss-live";
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
        var railway = sandbox.Content.Railway;
        var route = railway.Network.Routes.Single();
        Check(route.train.status == TrainStatus.Dwelling && !route.train.segmentPaid, "Dwelling route required");
        railway.Tick(11);
        Check(route.train.status == TrainStatus.Moving && route.train.segmentPaid && route.train.progress > 0,
            "Moving train required");
        var train = sandbox.RailwayView.Defense(route.trainId)?.gameObject;
        Check(train != null && train.activeSelf, "Train must be visible before station loss");
        var before = train.transform.position;
        var fuel = route.train.fuel;
        var cargo = route.train.cargo;
        var progress = route.train.progress;
        var departure = railway.Network.Station(route.stops[route.train.stop].stationId);
        Check(departure != null, "Departure station required");

        sandbox.Content.GroundWorld.Remove(departure.Id);
        railway.Tick(.1);
        Check(railway.Network.Station(departure.PersistentId) == null, "Station reference should be gone");
        Check(route.train.status == TrainStatus.RouteError && route.error.Contains("역"),
            "Missing station must stop the route with an error: " + route.error);
        Check(route.train.segmentPaid && route.train.fuel == fuel && route.train.cargo == cargo &&
              route.train.progress == progress,
            "Station loss must preserve paid segment, fuel, cargo and progress");
        Check(train.activeSelf, "Train must stay visible on its paid segment");
        Check((train.transform.position - before).sqrMagnitude < .001f,
            "Train must stay at its last segment position");
        railway.Tick(5);
        Check(route.train.progress == progress && train.activeSelf, "Route error must hold position and visibility");
        return "PASS lostDepartureWhileMoving=true; routeError=true; paidFuelCargoProgressPreserved=true; trainVisibleAtTrack=true";
    }
}
