using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Filters;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json.Linq;

// Runs synchronous NUnit tests directly: Unity's default runner opens a temporary scene.
// UnityTest coroutines are deliberately not part of this runner.
public static class RunCurrentSceneChecks
{
    static string DefaultRoot=>"Docs/Measurements/CurrentScene/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"/";
    static void Drain(IEnumerable sequence){var iterator=sequence.GetEnumerator();try{while(iterator.MoveNext()){if(iterator.Current is IEnumerator nested)DrainIterator(nested);else if(iterator.Current is IEnumerable child)Drain(child);}}finally{(iterator as IDisposable)?.Dispose();}}
    static void DrainIterator(IEnumerator iterator){try{while(iterator.MoveNext()){if(iterator.Current is IEnumerator child)DrainIterator(child);}}finally{(iterator as IDisposable)?.Dispose();}}
    public static string Main(string category="CurrentScene",string outputDirectory=null)
    {
        string Root=outputDirectory??DefaultRoot;
        var allowed=Path.GetFullPath("Docs/Measurements")+Path.DirectorySeparatorChar;
        Root=Path.GetFullPath(Root)+Path.DirectorySeparatorChar;
        if(!Root.StartsWith(allowed,StringComparison.Ordinal)||category.Any(c=>!char.IsLetterOrDigit(c)&&c!='.'))throw new Exception("Invalid evidence destination or category");
        if(Application.isPlaying)throw new Exception("Edit mode required");
        var scene=SceneManager.GetActiveScene();
        if(scene.isDirty||SceneManager.sceneCount!=1||string.IsNullOrEmpty(scene.path))throw new Exception("One clean saved current scene required");
        if(File.Exists(Root+category+"-summary.json"))Root=Path.Combine(Root,"run-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"))+Path.DirectorySeparatorChar;
        Directory.CreateDirectory(Root);
        var report=new JArray();var errors=new List<string>();
        var contextProperty=typeof(UnityEngine.TestTools.UnityTestAttribute).Assembly.GetType("UnityEngine.TestRunner.NUnitExtensions.Runner.UnityTestExecutionContext",true).GetProperty("CurrentContext");
        var previousContext=contextProperty.GetValue(null);
        var initialBytes=File.ReadAllBytes(scene.path);
        void Log(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        Application.logMessageReceived+=Log;
        var oldConstructor=Reflect.ConstructorCallWrapper;
        Reflect.ConstructorCallWrapper=(type,args)=>Activator.CreateInstance(type,args);
        try {
            foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a=>new[]{"EternalSteam.EditModeTests","EternalSteam.LegacyCombatTests","EternalSteam.LegacyBuildingTests","EternalSteam.LegacyDeploymentTests"}.Contains(a.GetName().Name)))
            {
                var unityAssembly=typeof(UnityEngine.TestTools.UnityTestAttribute).Assembly;
                var editorAssembly=typeof(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi).Assembly;
                var builderType=unityAssembly.GetType("UnityEngine.TestTools.NUnitExtensions.UnityTestAssemblyBuilder",true);
                var contextType=unityAssembly.GetType("UnityEngine.TestRunner.NUnitExtensions.Runner.UnityTestExecutionContext",true);
                var factoryType=editorAssembly.GetTypes().Single(t=>t.Name=="EditmodeWorkItemFactory");
                var runnerType=unityAssembly.GetType("UnityEngine.TestRunner.NUnitExtensions.Runner.UnityTestAssemblyRunner",true);
                var builder=Activator.CreateInstance(builderType,new object[]{null,0});
                var context=Activator.CreateInstance(contextType);
                var flags=contextType.GetProperty("FeatureFlags");flags.SetValue(context,Activator.CreateInstance(flags.PropertyType));
                contextType.GetProperty("CurrentContext").SetValue(null,context);
                var runner=Activator.CreateInstance(runnerType,new[]{builder,Activator.CreateInstance(factoryType),context});
                var load=runnerType.GetMethod("Load");
                var platform=Enum.Parse(load.GetParameters()[1].ParameterType,"EditMode");
                load.Invoke(runner,new object[]{new[]{assembly},platform,new Dictionary<string,object>()});
                ITestFilter filter=category=="all"?TestFilter.Empty:new CategoryFilter(category);
                Drain((IEnumerable)runnerType.GetMethod("Run").Invoke(runner,new object[]{TestListener.NULL,filter}));
                var result=(ITestResult)runnerType.GetProperty("Result").GetValue(runner);
                string file=Root+category+"-"+assembly.GetName().Name+".xml";
                File.WriteAllText(file,result.ToXml(true).OuterXml);
                report.Add(new JObject{{"assembly",assembly.GetName().Name},{"total",result.PassCount+result.FailCount+result.SkipCount+result.InconclusiveCount},{"passed",result.PassCount},{"failed",result.FailCount},{"skipped",result.SkipCount},{"inconclusive",result.InconclusiveCount},{"xml",file}});
                if(scene.handle!=SceneManager.GetActiveScene().handle||SceneManager.sceneCount!=1)throw new Exception("Scene scope changed");
            }
        } finally { Application.logMessageReceived-=Log; Reflect.ConstructorCallWrapper=oldConstructor; contextProperty.SetValue(null,previousContext); }
        var summary=new JObject{{"scene",scene.path},{"sceneHandleUnchanged",scene.handle==SceneManager.GetActiveScene().handle},{"dirty",scene.isDirty},{"savedSceneBytesUnchanged",initialBytes.SequenceEqual(File.ReadAllBytes(scene.path))},{"category",category},{"assemblies",report},{"errors",new JArray(errors)}};
        File.WriteAllText(Root+category+"-summary.json",summary.ToString());
        int total=report.Sum(r=>(int)r["total"]),failed=report.Sum(r=>(int)r["failed"]);
        if(total==0||failed>0||errors.Count>0||scene.isDirty||!(bool)summary["savedSceneBytesUnchanged"])throw new Exception("Validation failed; see "+Root+category+"-summary.json");
        return summary.ToString();
    }
}
