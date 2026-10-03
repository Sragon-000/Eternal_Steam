using System;using System.IO;using UnityEngine;using UnityEditor.TestTools.TestRunner.Api;
public sealed class Hud08DResultRecorder:ICallbacks {
 public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}
 public void RunFinished(ITestResultAdaptor r){TestRunnerApi.SaveResultToFile(r,"Docs/Measurements/2026-10-03-hud-08d/product-tests.xml");}
}
public static class ExportHud08DResults{public static string Main(){ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Hud08DResultRecorder());return "Authoritative full result XML callback registered";}}
