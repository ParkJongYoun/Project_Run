import json
import shutil
import subprocess
from pathlib import Path

root = Path(__file__).resolve().parents[1]
project = root.parents[2]
plan = json.loads((root / "작업기록" / "트리포_모델생성_계획.json").read_text(encoding="utf-8"))
blender = r"C:\Program Files\Blender Foundation\Blender 5.1\blender.exe"
for asset in plan["assets"]:
    source = root / "트리포_생성모델" / Path(asset["source"]).stem / "model.fbx"
    record = source.parent / "task.json"
    if not source.exists() or not record.exists():
        continue
    destination = project / "Assets" / "ArtBundles" / "Ranch" / "TripoModels" / asset["id"] / (asset["id"] + ".fbx")
    destination.parent.mkdir(parents=True, exist_ok=True)
    if not destination.exists():
        shutil.copy2(source, destination)
    report = root / "작업기록" / "트리포_적용" / (asset["id"] + "_원본토폴로지.json")
    if not report.exists():
        result = subprocess.run([blender, "--background", "--factory-startup", "--python",
                                 str(root / "작업기록" / "FBX_토폴로지_검수.py"), "--", str(source), str(report)],
                                capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=180)
        if result.returncode or not report.exists():
            raise RuntimeError(asset["id"] + " inspection failed: " + result.stderr[-2000:] + result.stdout[-2000:])
    data = json.loads(report.read_text(encoding="utf-8"))
    print(json.dumps({"id": asset["id"], "faces": data["total_faces"], "quads": data["total_quads"],
                      "triangles": data["total_render_triangles"], "asset": str(destination)}, ensure_ascii=False), flush=True)
