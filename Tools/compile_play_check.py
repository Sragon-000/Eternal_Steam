"""Build a Tools verification class for eval reflection inside the live Unity Editor."""
from pathlib import Path
import subprocess,sys,re
root=Path(__file__).resolve().parents[1]
source=root/sys.argv[1]
out=Path('/tmp')/(source.stem+'-live.dll')
rsp=next((root/'Library/Bee/artifacts').glob('*.dag/EternalSteam.EditModeTests.rsp')).read_text()
lines=[]
for line in rsp.splitlines():
 if line.startswith(('"Assets/','-out:','-refout:')):continue
 lines.append(line)
lines += ['-r:"Library/ScriptAssemblies/EternalSteam.LegacyCombat.dll"']
lines += ['-r:"Library/ScriptAssemblies/EternalSteam.LegacyDefinitions.dll"']
lines += ['-out:"'+str(out)+'"','"'+str(source)+'"']
p=out.with_suffix('.rsp');p.write_text('\n'.join(lines))
scripting=Path('/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/Resources/Scripting')
subprocess.run([str(scripting/'NetCoreRuntime/dotnet'),str(scripting/'DotNetSdkRoslyn/csc.dll'),'@'+str(p),'-nologo'],cwd=root,check=True)
print(out)
