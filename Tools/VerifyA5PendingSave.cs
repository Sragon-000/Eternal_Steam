using System;
using System.IO;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;

public static class VerifyA5PendingSave
{
    const string Root = "/tmp/eternal-a5-pending-live";
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static string Main()
    {
        Check(Application.isPlaying &&
            Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT") == Root,
            "Expected isolated Play save root");
        var sandbox = UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        Check(sandbox != null && sandbox.Persistence != null, "Live sandbox and persistence required");
        sandbox.Persistence.Automatic = false;
        sandbox.GetComponent<OpenWorldInput>().Cancel();
        sandbox.Clock.Paused = true;
        sandbox.Clock.SetPhase(DayPhase.Day);
        Check(!sandbox.Persistence.Blocked, sandbox.Persistence.Status);
        var route = sandbox.Content.Railway.Network.Routes.Single();
        var defense = sandbox.Content.Railway.Defense;
        var mounted = defense.Mounted.Single();
        Check(route.train.armament.definition == "defense.arc", "Mounted arc fixture required");
        Check(!defense.HasPendingExecution, "No attack should be pending at load");
        Check(sandbox.Persistence.CanSave(out var initialReason), initialReason);

        // Test-only battery injection in a copied save slot; production charging is covered elsewhere.
        route.train.armament.battery = 100;
        var point = mounted.Building.Position + Vector3.forward * 2;
        Check(sandbox.Enemies.TrySpawn(point, point, 0, 1000), "Enemy spawn failed");
        sandbox.Enemies.MoveAndIndex(0);
        for (var i = 0; i < 100 && !defense.HasPendingExecution; i++)
            sandbox.Content.Railway.Tick(.05, true);
        Check(defense.HasPendingExecution, "Mounted weapon never began pending execution");

        sandbox.Enemies.Reset();
        sandbox.TargetAdapter.Reset();
        Check(sandbox.Enemies.Alive == 0 && defense.HasPendingExecution,
            "Pending attack must outlive the enemy");
        var path = Path.Combine(Root, "current.json");
        var before = File.ReadAllBytes(path);
        var lastSaved = sandbox.Persistence.LastSavedUtc;
        Check(!sandbox.Persistence.CanSave(out var reason) && reason.Contains("기차 방어"),
            "Pending train attack must be the save blocker: " + reason);
        Check(!sandbox.Persistence.Save() && sandbox.Persistence.Status.Contains("기차 방어"),
            "Unsafe save should be rejected");
        Check(before.SequenceEqual(File.ReadAllBytes(path)) &&
            sandbox.Persistence.LastSavedUtc == lastSaved,
            "Rejected save changed the current slot or save timestamp");

        var steps = 0;
        while (defense.HasPendingExecution && steps++ < 300)
            sandbox.Content.Railway.Tick(.1, true);
        Check(!defense.HasPendingExecution, "Pending attack did not finish");
        Check(sandbox.Persistence.CanSave(out var resumedReason), resumedReason);
        Check(sandbox.Persistence.Save(), sandbox.Persistence.Status);
        Check(!before.SequenceEqual(File.ReadAllBytes(path)), "Safe save did not update the current slot");
        Check(sandbox.Persistence.Capture().railway.routes.Single().train.armament.definition == "defense.arc",
            "Safe save lost mounted weapon");
        return $"PASS blockedReason={reason}; rejectedSaveUnchanged=true; resumedSteps={steps}; safeSave=true";
    }
}
