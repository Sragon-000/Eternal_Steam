if(UnityEditor.EditorApplication.isPlaying)throw new System.Exception("Read-only audit probe requires Edit mode");
var type=System.Reflection.Assembly.Load("EternalSteam.EditModeTests").GetType("EternalSteam.Tests.RailwayTests");var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
object Run(System.Func<object,object> check){var fixture=System.Activator.CreateInstance(type);type.GetMethod("Setup").Invoke(fixture,null);try{return check(fixture);}finally{type.GetMethod("Cleanup").Invoke(fixture,null);}}
var empty=Run(f=>{
 var n=(EternalSteam.Railway.RailwayNetwork)type.GetField("network",flags).GetValue(f);var draft=(System.Collections.Generic.List<EternalSteam.Railway.RailStop>)type.GetField("draft",flags).GetValue(f);
 n.Commit(System.Guid.NewGuid().ToString("N"),draft,out var route,out var error);bool originalNull=route.train.resource==null;
 var capture=n.Capture();var encoded=UnityEngine.JsonUtility.ToJson(capture);n.Restore(capture);bool accepted=n.Configure(n.Routes[0],"iron",0,100,0,out error);
 return new{beforeNull=originalNull,capturedResource=capture.routes[0].train.resource,configureAfterRestore=accepted,error,storedExcerpt=encoded.Substring(encoded.IndexOf("resource"),System.Math.Min(70,encoded.Length-encoded.IndexOf("resource")))};
});
var recovery=Run(f=>{
 var n=(EternalSteam.Railway.RailwayNetwork)type.GetField("network",flags).GetValue(f);var r=(EternalSteam.Railway.RailRoute)type.GetMethod("Route",flags).Invoke(f,null);
 var buildings=(System.Collections.Generic.List<EternalSteam.BuildingInstance>)type.GetField("buildings",flags).GetValue(f);var tracks=buildings.Where(b=>b.Module<EternalSteam.Railway.RailFacility>()?.Kind==EternalSteam.Railway.RailFacilityKind.Track).ToArray();
 bool first=n.CanRecover(tracks[0],out _);tracks[0].Dispose();n.Refresh(buildings);bool second=n.CanRecover(tracks[1],out var error);
 return new{firstRecoveryAllowed=first,status=r.train.status.ToString(),atStation=!r.train.segmentPaid,secondRecoveryAllowed=second,error};
});
return new{emptyCargo=empty,errorRecovery=recovery,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,dirty=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty};
