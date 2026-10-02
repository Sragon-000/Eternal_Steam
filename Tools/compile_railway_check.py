"""Compile changed assemblies with Unity's cached response files; does not claim Editor validation."""
from pathlib import Path
import re,subprocess
root=Path(__file__).resolve().parents[1]
out=Path('/tmp/eternal-railway-compile');out.mkdir(exist_ok=True)
scripting=Path('/Applications/Unity/Hub/Editor/6000.3.14f1/Unity.app/Contents/Resources/Scripting')
assemblies=['EternalSteam.Runtime','EternalSteam.Progression','EternalSteam.Economy','EternalSteam.Railway','EternalSteam.OpenWorldSandbox','EternalSteam.EditModeTests']
for name in assemblies:
 original=next((root/'Library/Bee/artifacts').glob('*.dag/'+(name if name!='EternalSteam.Railway' else 'EternalSteam.Runtime')+'.rsp'))
 text=original.read_text()
 text=re.sub(r'^-out:.*$',f'-out:"{out/name}.dll"',text,flags=re.M)
 text=re.sub(r'^-refout:.*$',f'-refout:"{out/name}.ref.dll"',text,flags=re.M)
 for dep in assemblies:
  if dep==name:continue
  text=re.sub(r'^-r:"[^"]*/'+re.escape(dep)+r'(?:\.ref)?\.dll"$',f'-r:"{out/dep}.dll"',text,flags=re.M) if (out/(dep+'.dll')).exists() else text
 if name=='EternalSteam.Railway':
  text='\n'.join(l for l in text.splitlines() if not (l.startswith('"Assets/') and l.endswith('.cs"')))
  text+='\n-r:"'+str(out/'EternalSteam.Runtime.dll')+'"\n'
  files=list((root/'Assets/EternalSteam/Code/Railway').glob('*.cs'))
 else:
  locations={'EternalSteam.Progression':'Code/Progression','EternalSteam.Economy':'Code/Economy','EternalSteam.Runtime':'Code/Contracts','EternalSteam.OpenWorldSandbox':'Tests/OpenWorld/Runtime','EternalSteam.EditModeTests':'Tests/EditMode'}
  files=list((root/'Assets/EternalSteam'/locations[name]).glob('*.cs'))
 for p in files:
  relative=str(p.relative_to(root))
  if '"'+relative+'"' not in text:text+='\n"'+relative+'"\n'
 if name in ['EternalSteam.OpenWorldSandbox','EternalSteam.EditModeTests']:text+='\n-r:"'+str(out/'EternalSteam.Railway.dll')+'"\n'
 rsp=out/(name+'.rsp');rsp.write_text(text)
 result=subprocess.run([str(scripting/'NetCoreRuntime/dotnet'),str(scripting/'DotNetSdkRoslyn/csc.dll'),'@'+str(rsp),'-nologo'],cwd=root,capture_output=True,text=True)
 (out/(name+'.log')).write_text(result.stdout+result.stderr)
 print(name,result.returncode,(result.stdout+result.stderr)[-7000:])
 if result.returncode:raise SystemExit(result.returncode)
