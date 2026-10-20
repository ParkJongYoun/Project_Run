"""Inventory project-owned procedural UI artwork. Read only; no asset rewriting."""
from pathlib import Path
import json
import re

root = Path(__file__).resolve().parents[2]
out = root / "DataBundles/아트/경기UI/이미지스프라이트_20261020"
out.mkdir(parents=True, exist_ok=True)
sources = list((root / "Assets/Scripts").rglob("*.cs"))
pattern = re.compile(r"OnPopulateMesh|MaskableGraphic|new Texture2D|SetPixels|SetPixel\(|GUI\.DrawTexture")
matches = []
for path in sources:
    for number, line in enumerate(path.read_text(encoding="utf-8-sig").splitlines(), 1):
        if pattern.search(line):
            matches.append(dict(path=path.relative_to(root).as_posix(), line=number, text=line.strip()))

serialized = []
for folder in ("Assets/Scenes", "Assets/Resources/Prefabs/UI", "Assets/Resources/UI"):
    for path in (root / folder).rglob("*"):
        if path.suffix in (".unity", ".prefab", ".asset"):
            serialized.append((path, path.read_text(encoding="utf-8-sig", errors="replace")))

targets = [
    "Assets/Scripts/Game/UI/Race/UIItem_SpeedLines.cs",
    "Assets/Scripts/Game/UI/Race/UIItem_WindLines.cs",
    "Assets/ArtBundles/RaceShowcase/UI/RoundPanel.png",
    "Assets/ArtBundles/RaceShowcase/UI/Dot.png",
    "Assets/ArtBundles/RaceShowcase/UI/SoftDisc.png",
]
skin = root / "Assets/Scripts/Editor/Ranch/RanchCollectionBuilder.Skin.cs"
skin_text = skin.read_text(encoding="utf-8-sig")
names = re.findall(r'Shape\("([^"]+)"', skin_text)
names = [name for name in names if name != "Gauge_"]
names += ["Gauge_" + key for key in ("speed", "stamina", "strength", "intelligence", "spirit")]
names += ["GroundShadow"]
targets += ["Assets/ArtBundles/UI/RanchCollection/ReferenceMatch/" + name + ".png" for name in names]
targets += [p.relative_to(root).as_posix() for p in (root / "Assets/ArtBundles").rglob("UI_Frame.png")]
targets += [p.relative_to(root).as_posix() for p in (root / "Assets/ArtBundles").rglob("UI_Fill.png")]
usage = []
for target in targets:
    meta = root / (target + ".meta")
    if not meta.exists():
        continue
    match = re.search(r"^guid: ([a-f0-9]+)", meta.read_text(encoding="utf-8-sig"), re.M)
    if not match:
        continue
    guid = match.group(1)
    users = [p.relative_to(root).as_posix() for p, text in serialized if guid in text]
    usage.append(dict(target=target, references=users))

report = dict(
    scanned_project_csharp=len(sources),
    scanned_serialized_ui_and_scenes=len(serialized),
    ranch_generated_parts=len(names),
    matches=matches,
    references=usage,
    excluded="External packages and preserved OldAsset sources; model/animation art and preview captures are classified separately.",
)
(out / "UI코드그리기_검색결과.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(dict(scripts=len(sources), serialized=len(serialized), ranch_parts=len(names),
                     custom_graphics=[x["path"] for x in matches if " : MaskableGraphic" in x["text"]]), ensure_ascii=False))
