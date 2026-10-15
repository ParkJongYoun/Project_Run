"""Prepare an exact Git path list from Unity's three-scene dependency closure.

Run after exporting unity_dependencies.json. This never stages, commits or deletes
anything. Include runtime sources and the completed scene authoring tools, plus
the URL-loaded fox movie which AssetDatabase cannot find through serialization.
"""
from pathlib import Path
import json

ROOT=Path(__file__).resolve().parents[2]
REPOSITORY=ROOT.parent
QA=ROOT/'DataBundles/검증/2D레이스_20261016'

def main():
    dependencies=json.loads((QA/'unity_dependencies.json').read_text(encoding='utf-8-sig'))
    # Package paths are virtual AssetDatabase paths restored by the tracked UPM lockfile.
    candidates={ROOT/p for p in dependencies if p.startswith('Assets/')}
    for folder in ('Assets/Scripts/Core','Assets/Scripts/Game','Assets/Scripts/Editor/Racecourse'):
        candidates.update(p for p in (ROOT/folder).rglob('*') if p.is_file())
    candidates.add(ROOT/'Assets/StreamingAssets/Video/FoxUltimate1024.mp4')
    candidates.add(ROOT/'Assets/Settings/PC_RPAsset.asset')
    for path in list(candidates):
        if path.suffix!='.meta' and path.with_name(path.name+'.meta').is_file():
            candidates.add(path.with_name(path.name+'.meta'))
        for parent in path.parents:
            if parent==ROOT:break
            meta=parent.with_name(parent.name+'.meta')
            if meta.is_file():candidates.add(meta)
    candidates.add(ROOT/'ProjectSettings/EditorBuildSettings.asset')
    missing=[str(p) for p in candidates if not p.is_file()]
    if missing:raise ValueError(missing)
    paths=sorted(p.relative_to(REPOSITORY).as_posix() for p in candidates)
    (QA/'runtime_commit_paths.nul').write_bytes(b''.join(p.encode('utf-8')+b'\0' for p in paths))
    (QA/'runtime_commit_paths.json').write_text(json.dumps(paths,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'files':len(paths),'bytes':sum(p.stat().st_size for p in candidates)},ensure_ascii=False))

if __name__=='__main__':main()
