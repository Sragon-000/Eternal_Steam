using System;using System.IO;using UnityEngine;using UnityEditor.TestTools.TestRunner.Api;
public sealed class ProductResultRecorder:ICallbacks {
 public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}
 public void RunFinished(ITestResultAdaptor r){TestRunnerApi.SaveResultToFile(r,"Docs/Measurements/2026-10-02-train-hud-integration/product-tests.xml");}
}
public static class ExportProductTestResults{public static string Main(){ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new ProductResultRecorder());return "Authoritative full result XML callback registered";}}
