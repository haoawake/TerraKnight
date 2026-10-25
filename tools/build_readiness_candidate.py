"""Build a candidate and record its exact source manifest, without installing it."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]

def manifest():
    return {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted((ROOT/'src/TerraKnight').rglob('*'))
            if p.is_file() and not {'bin','obj'}.intersection(p.parts)
            and p.suffix in {'.cs','.csproj','.png','.json','.wav','.shader'}}

if __name__ == '__main__':
    ap=argparse.ArgumentParser(); ap.add_argument('output',type=Path); args=ap.parse_args()
    out=args.output.resolve(); out.mkdir(parents=True,exist_ok=False)
    before=manifest()
    with (out/'build.log').open('w',encoding='utf-8') as log:
        subprocess.run(['dotnet','build','src/TerraKnight/TerraKnight.csproj','-c','Release',
            '-o',str(out/'build'),'-p:SkipPluginInstall=true','-v:q'],cwd=ROOT,stdout=log,stderr=subprocess.STDOUT,check=True)
    after=manifest()
    stable=before==after
    (out/'source-manifest.json').write_text(json.dumps(after,ensure_ascii=False,indent=2),'utf-8')
    result={'SourceStableDuringBuild':stable,'SourceTreeHash':hashlib.sha256(json.dumps(after,sort_keys=True).encode()).hexdigest(),
            'BuildHash':hashlib.sha256((out/'build/TerraKnight.dll').read_bytes()).hexdigest(),
            'Head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
            'Build':str(out/'build'),'Installed':False}
    (out/'receipt.json').write_text(json.dumps(result,indent=2),'utf-8')
    print(json.dumps(result,indent=2))
    if not stable: raise SystemExit('Source changed during build; preserve this candidate and build a fresh one.')
