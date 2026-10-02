using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using EternalSteam;
using EternalSteam.OpenWorld;

// Observes natural simulation after OS pointer input; no gameplay mutation or tick injection.
public static class RecordPointerDelivery
{
    static double cargo,bank;
    static int stop;
    static string path;
    static OpenWorldSandbox world;
    public static string Begin(string output)
    {
        EditorApplication.update-=Observe;
        world=UnityEngine.Object.FindFirstObjectByType<OpenWorldSandbox>();
        if(!Application.isPlaying||Environment.GetEnvironmentVariable("ETERNAL_STEAM_SAVE_TEST_ROOT")!="/tmp/eternal-qa06-pointer-20260930")throw new Exception("Isolated pointer QA required");
        path=output;Snapshot();EditorApplication.update+=Observe;return "Read-only delivery observer registered";
    }
    static void Snapshot()
    {
        var r=world.Content.Railway.Network.Routes[0];cargo=r.train.cargo;stop=r.train.stop;
        var destination=world.Content.Railway.Network.Station(r.stops[1].stationId);
        bank=world.Content.PaymentBankFor(destination).Amount("iron");
    }
    static void Observe()
    {
        if(world==null||!Application.isPlaying){EditorApplication.update-=Observe;return;}
        var r=world.Content.Railway.Network.Routes[0];
        var destination=world.Content.Railway.Network.Station(r.stops[1].stationId);
        double current=world.Content.PaymentBankFor(destination).Amount("iron");
        if(stop==0&&r.train.stop==1&&cargo==30&&r.train.cargo==0){
            File.WriteAllText(path,"{\"pass\":"+(current-bank>=30?"true":"false")+",\"cargoBefore\":30,\"cargoAfter\":0,\"destinationIronBefore\":"+bank+",\"destinationIronAfter\":"+current+",\"sourceBase\":\""+world.Content.Railway.Network.Station(r.stops[0].stationId).OwnerBaseId+"\",\"destinationBase\":\""+destination.OwnerBaseId+"\",\"input\":\"OS pointer via CUA\",\"simulation\":\"natural frames, no injected ticks\"}");
            EditorApplication.update-=Observe;return;
        }
        Snapshot();
    }
}
