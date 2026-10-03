using System;using System.IO;using UnityEngine;using UnityEditor.TestTools.TestRunner.Api;
public sealed class Hud05ResultRecorder:ICallbacks {
 public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}
 public void RunFinished(ITestResultAdaptor r){TestRunnerApi.SaveResultToFile(r,"Docs/Measurements/2026-10-02-hud-05/product-tests.xml");}
}
public static class ExportHud05Results{public static string Main(){ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Hud05ResultRecorder());return "Authoritative full result XML callback registered";}}
