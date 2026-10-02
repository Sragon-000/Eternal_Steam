using System;
using System.Linq;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;

public static class VerifyRailwayG10Line
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    public static string Main()
    {
        Check(Application.isPlaying && Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT") == "/tmp/eternal-railway-g12", "Isolated Play required");
        var sandbox = UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        sandbox.Persistence.Automatic = false;
        sandbox.Clock.Paused = true;
        var input = sandbox.GetComponent<OpenWorldInput>();
        var content = sandbox.Content;
        var track = sandbox.RailwayHud.Track;
        input.Cancel();
        var bank = content.Inventories.Available(content.Bases.SelectedBaseId);
        Check(bank != null, "Active base inventory");
        if (bank.Amount("iron") < 10) bank.Deposit("iron", 100);
        Vector2Int? start = null;
        for (int z = 8; z < 38 && !start.HasValue; z++)
            for (int x = -18; x < 28 && !start.HasValue; x++)
            {
                bool valid = true;
                for (int n = 0; n < 3; n++)
                    if (!input.Edits.PreviewContent(track, WorldGridGeometry.Center(new Vector2Int(x + n, z), 2), out _, out _, out _, out _))
                        valid = false;
                if (valid) start = new Vector2Int(x, z);
            }
        Check(start.HasValue, "Three adjacent valid track cells");
        var stroke = new WallPlacementStroke(input.Edits);
        stroke.Begin(track);
        stroke.AddPoint(WorldGridGeometry.Center(start.Value, 2));
        stroke.AddPoint(WorldGridGeometry.Center(start.Value + new Vector2Int(2, 0), 2));
        Check(stroke.Added == 3 && stroke.Rejected == 0, "Three track cells reserved by straight stroke: " + stroke.LastFailure);
        var quote = input.Edits.Quote();
        Check(quote.Tracks == 3 && quote.Iron == 3 && quote.Affordable, "Total line quote");
        var before = bank.Amount("iron");
        stroke.Complete();
        Check(input.Confirm(), "Line confirmation: " + sandbox.Message);
        Check(Math.Abs(bank.Amount("iron") - before + 3) < .001, "Exact line payment");

        input.BeginEditing();
        input.SelectContent(track);
        Vector3? next = null;
        for (int z = 8; z < 38 && !next.HasValue; z++)
            for (int x = -18; x < 28 && !next.HasValue; x++)
            {
                var point = WorldGridGeometry.Center(new Vector2Int(x, z), 2);
                if (input.Edits.PreviewContent(track, point, out _, out _, out _, out _)) next = point;
            }
        Check(next.HasValue && input.ClickWorld(next.Value), "One pending track before stock change");
        var pending = input.Edits.ContentPending.Single();
        var held = bank.Amount("iron");
        bank.Withdraw("iron", held);
        Check(!input.Confirm(), "Live stock shortage rejects confirmation");
        Check(input.Edits.ContentPending.Count == 1 && ReferenceEquals(input.Edits.ContentPending[0], pending), "Rejected confirmation preserves reservation");
        Check(bank.Amount("iron") == 0, "Rejected confirmation does not charge");
        input.Cancel();
        return "PASS three-cell straight rail reservation, quote=payment 3 iron, live shortage rejects atomically and preserves pending work";
    }
}
