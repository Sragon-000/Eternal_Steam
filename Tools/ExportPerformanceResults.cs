using System;using System.IO;using UnityEngine;using UnityEditor.TestTools.TestRunner.Api;
public sealed class PerformanceResultRecorder:ICallbacks {
 public void RunStarted(ITestAdaptor t){}public void TestStarted(ITestAdaptor t){}public void TestFinished(ITestResultAdaptor t){}
 public void RunFinished(ITestResultAdaptor r){TestRunnerApi.SaveResultToFile(r,"Docs/Measurements/2026-10-03-performance/product-tests.xml");}
}
public static class ExportPerformanceResults{public static string Main(){ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new PerformanceResultRecorder());return "Authoritative full result XML callback registered";}}
