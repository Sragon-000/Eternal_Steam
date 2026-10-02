"""Compile current source and run synthetic managed upgrade commands; not Editor/Play validation."""
from pathlib import Path
import subprocess,re,shutil,json,sys
root=Path(__file__).resolve().parents[1];out=Path('/tmp/eternal-railway-compile')
subprocess.run(['python3',str(root/'Tools/compile_railway_check.py')],cwd=root,check=True)
scripting=Path('/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/Resources/Scripting');dotnet=scripting/'NetCoreRuntime/dotnet'
source=(out/'EternalSteam.EditModeTests.rsp').read_text()
source='\n'.join(l for l in source.splitlines() if not(l.startswith('"Assets/') and l.endswith('.cs"')) and not l.startswith('-refout:'))
source=source.replace('-target:library','-target:exe')
for path in re.findall(r'^-r:"([^\"]+)"',source,re.M):
 p=Path(path);p=p if p.is_absolute() else root/p
 if p.exists() and p.parent!=out:shutil.copy2(p,out/p.name)
name='UpgradeLevelChecks';rsp=out/(name+'.rsp')
rsp.write_text(re.sub(r'^-out:.*$',f'-out:"{out/name}.dll"',source,flags=re.M)+'\n"'+str(root/'Tools/UpgradeLevelChecks.cs')+'"\n')
(out/(name+'.runtimeconfig.json')).write_text('{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"6.0.21"}}}')
subprocess.run([str(dotnet),str(scripting/'DotNetSdkRoslyn/csc.dll'),'@'+str(rsp),'-nologo'],cwd=root,check=True)
r=subprocess.run([str(dotnet),str(out/(name+'.dll'))],cwd=root,capture_output=True,text=True);print(r.stdout);print(r.stderr)
report=Path(sys.argv[1]) if len(sys.argv)>1 else out/'upgrade-checks.json'
report.write_text(json.dumps({'scope':'Current compiled source; synthetic building handles; not Unity asset/lifecycle/UI/save-reload validation','exitCode':r.returncode,'output':r.stdout,'error':r.stderr},ensure_ascii=False,indent=2)+'\n')
sys.exit(r.returncode)
