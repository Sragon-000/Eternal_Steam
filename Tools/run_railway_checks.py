"""Rebuild and run independent economy/railway checks. Not Unity EditMode or PlayMode."""
from pathlib import Path
import subprocess,re,shutil,json,sys
root=Path(__file__).resolve().parents[1];out=Path('/tmp/eternal-railway-compile')
subprocess.run(['python3',str(root/'Tools/compile_railway_check.py')],cwd=root,check=True)
scripting=Path('/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/Resources/Scripting');dotnet=scripting/'NetCoreRuntime/dotnet';compiler=scripting/'DotNetSdkRoslyn/csc.dll'
source=(out/'EternalSteam.EditModeTests.rsp').read_text()
source='\n'.join(l for l in source.splitlines() if not(l.startswith('"Assets/') and l.endswith('.cs"')) and not l.startswith('-refout:'))
source=source.replace('-target:library','-target:exe')+'\n-r:"'+str(out/'EternalSteam.EditModeTests.dll')+'"\n'
for path in re.findall(r'^-r:"([^\"]+)"',source,re.M):
 p=Path(path);p=p if p.is_absolute() else root/p
 if p.exists() and p.parent!=out:shutil.copy2(p,out/p.name)
(out/'EconomyChecks.cs').write_text('''using System;using System.Linq;class Program{static int Main(){int passed=0;var t=typeof(EternalSteam.Tests.RailwayEconomyTests);var instance=Activator.CreateInstance(t);foreach(var m in t.GetMethods()){if(m.GetCustomAttributes(false).Any(a=>a.GetType().Name=="TestAttribute")){m.Invoke(instance,null);Console.WriteLine("PASS "+m.Name);passed++;}}foreach(var n in new[]{double.NaN,double.PositiveInfinity,-1}){t.GetMethod("InvalidTransfersLeaveStockUnchanged").Invoke(instance,new object[]{n});passed++;}Console.WriteLine("Passed "+passed+" economy checks outside Unity");return 0;}}''')
results={}
for name,cs in [('EconomyChecks',out/'EconomyChecks.cs'),('RailwayCoreChecks',root/'Tools/RailwayCoreChecks.cs')]:
 text=re.sub(r'^-out:.*$',f'-out:"{out/name}.dll"',source,flags=re.M)+'\n"'+str(cs)+'"\n'
 rsp=out/(name+'.rsp');rsp.write_text(text)
 (out/(name+'.runtimeconfig.json')).write_text('{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.21"}}}')
 subprocess.run([str(dotnet),str(compiler),'@'+str(rsp),'-nologo'],cwd=root,check=True)
 r=subprocess.run([str(dotnet),str(out/(name+'.dll'))],cwd=root,capture_output=True,text=True);print(r.stdout);print(r.stderr)
 results[name]={'exitCode':r.returncode,'output':r.stdout,'error':r.stderr}
 if r.returncode:raise SystemExit(r.returncode)
report=Path(sys.argv[1]) if len(sys.argv)>1 else root/'Docs/Validation/2026-09-28-railway-independent-checks.json';report.write_text(json.dumps({'scope':'Standalone managed checks; not Unity import, NUnit runner, scene or Play validation','results':results},ensure_ascii=False,indent=2)+'\n')
