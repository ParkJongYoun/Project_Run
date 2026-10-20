"""Copy the selected purchased FX and dependencies; never modify Project OT."""
import json
import re
import uuid
import sys
from pathlib import Path

SOURCE = Path("D:/Project_OT/OT")
PROJECT = Path("D:/Project_Run/Run")
DEST = PROJECT / "Assets/ArtBundles/Race/SkillEffects"
RECORD = PROJECT / "DataBundles/작업기록/경기6종_스킬이펙트_20261021"
GUID = re.compile(r"\bguid: ([0-9a-f]{32})")
PICKS = {
    "Water": "3rdParty/Vefects/Stylized AoE VFX/VFX/Water/Particles/VFX_Water_Area_01.prefab",
    "Air": "3rdParty/Vefects/Stylized AoE VFX/VFX/Air/Particles/VFX_Air_Area_01.prefab",
    "Dark": "3rdParty/Vefects/Stylized AoE VFX/VFX/Dark/Particles/VFX_Dark_Area_01.prefab",
    "Dash": "3rdParty/Vefects/Anime Stylized VFX/Shared/Particles/VFX_Dash.prefab",
    "Buff": "3rdParty/Vefects/Anime Stylized VFX/Shared/Particles/VFX_Buff_Loop.prefab",
    "Ground": "Art/Effects/ThirdPartyURP/Lana Studio/Casual RPG VFX/Prefabs/Area_generic/Area_generic_blue.prefab",
    "Outbreak": "Art/Effects/ThirdPartyURP/Lana Studio/Casual RPG VFX/Prefabs/Area_generic/Area_generic_orange_outbreak.prefab",
    "Flash": "Art/Effects/ThirdPartyURP/Lana Studio/Casual RPG VFX/Prefabs/Burst/Flash_circle.prefab",
    "Speed": "Art/Effects/ThirdPartyURP/Lana Studio/Environment VFX pack/Prefabs/SpeedBoost/SpeedBoost_aside.prefab",
    "Wind": "Art/Effects/ThirdPartyURP/Lana Studio/Environment VFX pack/Prefabs/Wind_Leaves_Tornado/Wind_heavy.prefab",
}


def port_shader(text, name):
    """Preserve authored texture channels, LUTs, erosion and custom particle streams."""
    properties = text[text.index("Properties"):text.index("SubShader")]
    block = re.search(r"(?:CGINCLUDE|CGPROGRAM)(.*?)ENDCG", text, re.S).group(1)
    block = re.sub(r"^\s*#include.*$", "", block, flags=re.M)
    block = re.sub(r"^\s*#pragma (?!shader_feature)[^\n]*", "", block, flags=re.M)
    block = re.sub(r"UNITY_DECLARE_DEPTH_TEXTURE.*?;", "", block)
    block = re.sub(r"uniform float4 _CameraDepthTexture_TexelSize;", "", block)
    # This isolated race adaptation does not require camera depth/opaque texture changes.
    block = re.sub(r"float screenDepth\w+\s*=[^;]+;", "", block)
    block = re.sub(r"float distanceDepth(\w*)\s*=[^;]+;", r"float distanceDepth\1 = 1.0;", block)
    block = block.replace("_Time.y", "_RunRaceEffectTime")
    block = block.replace("_Time.x", "(_RunRaceEffectTime / 20.0)")
    block = block.replace("UNITY_PI", "PI")
    block = re.sub(r"^\s*INTERNAL_DATA\s*$", "", block, flags=re.M)
    extra_include = ""
    if "_GrabTexture" in block:
        # Retained source-only refraction part; the six game prefabs do not use it.
        extra_include = '#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"'
        block = re.sub(r"^\s*ASE_DECLARE_SCREENSPACE_TEXTURE\( _GrabTexture \)\s*$", "", block, flags=re.M)
        block = re.sub(r"float4 screenColor12 = [^;]+;",
                       "float4 screenColor12 = float4(SampleSceneColor(appendResult18 + lerpResult21), 1);", block)
    fields = re.search(r"struct Input\s*{(.*?)};", block, re.S).group(1)
    assignments = []
    for typ, field in re.findall(r"\b(float[234]?|half[234]?|fixed[234]?)\s+(\w+)(?:\s*:\s*\w+)?\s*;", fields):
        width = typ[-1] if typ[-1].isdigit() else "1"
        swizzle = {"1": ".x", "2": ".xy", "3": ".xyz", "4": ""}[width]
        if field.startswith("uv"):
            match = re.match(r"uv(\d*)_", field)
            channel = max(0, int(match.group(1) or "1") - 1)
            value = f"v.uv{channel}{swizzle}"
        else:
            value = {"vertexColor": "v.color", "screenPos": "v.screenPos",
                     "worldPos": "v.worldPos", "worldNormal": "v.worldNormal",
                     "viewDir": "GetWorldSpaceNormalizeViewDir(v.worldPos)",
                     "ASEIsFrontFacing": "IS_FRONT_VFACE(face, 1, -1)"}[field]
        assignments.append(f"i.{field} = {value};")
    if "vertexDataFunc" in block:
        raise RuntimeError(f"Review authored vertex displacement before porting {name}")
    return f'''// Project Run URP adaptation of the purchased shader. Source preserved in Project OT.
Shader "ProjectRun/RaceSkill/{name}"
{{
{properties}
SubShader {{
Tags {{ "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }}
Pass {{
Tags {{ "LightMode"="SRPDefaultUnlit" }}
Cull [_Cull]
ZWrite Off
ZTest LEqual
Blend [_Src] [_Dst]
HLSLPROGRAM
#pragma target 3.5
#pragma vertex RaceVert
#pragma fragment RaceFrag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
{extra_include}
float _RunRaceEffectTime;
struct SurfaceOutput {{ half3 Albedo; half3 Normal; half3 Emission; half Specular; half Gloss; half Alpha; }};
{block}
struct RaceAttributes {{
 float4 vertex:POSITION; float3 normal:NORMAL; float4 color:COLOR;
 float4 uv0:TEXCOORD0; float4 uv1:TEXCOORD1; float4 uv2:TEXCOORD2; float4 uv3:TEXCOORD3;
}};
struct RaceVaryings {{
 float4 positionCS:SV_POSITION; float4 color:COLOR;
 float4 uv0:TEXCOORD0; float4 uv1:TEXCOORD1; float4 uv2:TEXCOORD2; float4 uv3:TEXCOORD3;
 float4 screenPos:TEXCOORD4; float3 worldPos:TEXCOORD5; float3 worldNormal:TEXCOORD6;
}};
RaceVaryings RaceVert(RaceAttributes v) {{
 RaceVaryings o=(RaceVaryings)0;
 o.worldPos=TransformObjectToWorld(v.vertex.xyz);
 o.positionCS=TransformWorldToHClip(o.worldPos);
 o.worldNormal=TransformObjectToWorldNormal(v.normal);
 o.screenPos=ComputeScreenPos(o.positionCS);
 o.color=v.color; o.uv0=v.uv0; o.uv1=v.uv1; o.uv2=v.uv2; o.uv3=v.uv3; return o;
}}
half4 RaceFrag(RaceVaryings v, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC):SV_Target {{
 Input i=(Input)0; {' '.join(assignments)}
 SurfaceOutput o=(SurfaceOutput)0; surf(i,o); return half4(o.Emission, saturate(o.Alpha));
}}
ENDHLSL
}}
}}
}}
'''


def main():
    if DEST.exists():
        raise RuntimeError("Destination already exists; do not overwrite tuned assets.")
    index = {}
    for meta in (SOURCE / "Assets").rglob("*.meta"):
        m = re.search(r"^guid: (\w+)", meta.read_text(errors="replace"), re.M)
        if m:
            index[m.group(1)] = Path(str(meta)[:-5])
    # URP package references use the same installed package GUIDs in both projects.
    todo = [SOURCE / "Assets" / p for p in PICKS.values()]
    found = {}
    while todo:
        path = todo.pop()
        if path in found:
            continue
        if not path.is_file():
            continue
        if path.suffix == ".cs":
            raise RuntimeError(f"Unexpected behaviour dependency: {path}")
        found[path] = uuid.uuid4().hex
        if path.suffix in {".prefab", ".mat", ".asset", ".shader", ".meta"}:
            for guid in GUID.findall(path.read_text(errors="replace")):
                if guid in index:
                    todo.append(index[guid])
    remap = {}
    destinations = {}
    for path, new in found.items():
        old = re.search(r"^guid: (\w+)", Path(str(path)+".meta").read_text(), re.M).group(1)
        remap[old] = new
        group = {".prefab": "SourcePrefabs", ".mat": "Materials", ".shader": "Shaders"}.get(path.suffix, "Dependencies")
        destinations[path] = DEST / group / (path.stem + "_" + old[:8] + path.suffix)
    prepared = {}
    for path, target in destinations.items():
        data = path.read_bytes()
        if path.suffix in {".prefab", ".mat", ".shader", ".asset"}:
            text = data.decode("utf-8-sig")
            if path.suffix == ".shader":
                text = port_shader(text, target.stem)
            data = GUID.sub(lambda m: "guid: "+remap.get(m.group(1), m.group(1)), text).encode("utf-8")
        meta = Path(str(path)+".meta").read_text(encoding="utf-8-sig")
        meta = GUID.sub(lambda m: "guid: "+remap.get(m.group(1), m.group(1)), meta)
        prepared[path] = (data, meta)
    rows = []
    for path, target in destinations.items():
        target.parent.mkdir(parents=True, exist_ok=True)
        data, meta = prepared[path]
        target.write_bytes(data)
        Path(str(target)+".meta").write_text(meta, encoding="utf-8")
        rows.append({"source":str(path), "destination":str(target), "guid":found[path]})
    RECORD.mkdir(parents=True, exist_ok=True)
    manifest = {"assets": rows, "prefabs": {name: str(destinations[SOURCE/"Assets"/p].relative_to(PROJECT)).replace("\\","/") for name,p in PICKS.items()}}
    (RECORD/"에셋_출처와복사기록.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")
    print(json.dumps({"count":len(rows),"bytes":sum(x.stat().st_size for x in destinations.values()),"prefabs":manifest["prefabs"]},ensure_ascii=False))


if __name__ == "__main__":
    if "--refresh-shaders" in sys.argv:
        manifest = json.loads((RECORD/"에셋_출처와복사기록.json").read_text(encoding="utf-8"))
        for row in manifest["assets"]:
            src, dst = Path(row["source"]), Path(row["destination"])
            if src.suffix == ".shader":
                dst.write_text(port_shader(src.read_text(encoding="utf-8-sig"),dst.stem),encoding="utf-8")
        print("Refreshed only the copied race shader adaptations.")
    else:
        main()
