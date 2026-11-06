"""Install source into an existing Unity project. Does not overwrite customized source."""
from pathlib import Path
import argparse,json,shutil
parser=argparse.ArgumentParser();parser.add_argument('project',type=Path);args=parser.parse_args()
root=Path(__file__).resolve().parent;project=args.project.resolve();manifest=project/'Packages/manifest.json'
if not (project/'Assets').is_dir() or not manifest.is_file():raise SystemExit('Select an existing Unity 6 project with Assets and Packages/manifest.json.')
data=json.loads(manifest.read_text());deps=data.setdefault('dependencies',{})
for name in ['com.unity.xr.arfoundation','com.unity.xr.arcore']:
 if name in deps and deps[name]!='6.3.1':raise SystemExit(f'{name} is already {deps[name]}; align package versions manually before installation.')
 deps[name]='6.3.1'
for name in ['com.unity.modules.imgui','com.unity.modules.unitywebrequest','com.unity.modules.unitywebrequesttexture']:
 deps.setdefault(name,'1.0.0')
target=project/'Assets/MetaSonGiAR/RuntimeImageTracker.cs'
if target.exists() and target.read_bytes()!=(root/'RuntimeImageTracker.cs').read_bytes():raise SystemExit('Refusing to overwrite customized RuntimeImageTracker.cs')
backup=manifest.with_suffix('.json.before-metasongi')
if not backup.exists():shutil.copyfile(manifest,backup)
target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(root/'RuntimeImageTracker.cs',target)
assets=project/'Assets/StreamingAssets';assets.mkdir(parents=True,exist_ok=True)
if not (assets/'target.png').exists():shutil.copyfile(root/'target.png',assets/'target.png')
temp=manifest.with_suffix('.json.tmp');temp.write_text(json.dumps(data,indent=2)+'\n');temp.replace(manifest)
print('Source installed. Follow README for AR Session, XR Origin, initial library and ARCore project validation.')
