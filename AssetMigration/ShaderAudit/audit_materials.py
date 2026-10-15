"""Read-only audit of migrated materials and serialized renderer bindings.

Outputs stay outside Assets. Counts describe .mat files, not model subassets.
Source and imported game assets are never modified.
"""
from pathlib import Path
import collections
import csv
import json
import re

WORK = Path(__file__).resolve().parent
PROJECT = WORK.parent.parent / 'Run'
ROOT = PROJECT / 'Assets/OldAsset'
PLAN = json.loads((WORK.parent / 'plan.json').read_text('utf-8'))
SOURCE_GUIDS = json.loads((WORK.parent / 'guid_index.json').read_text('utf-8'))
GUID_PATHS = {g: 'Assets/OldAsset/' + PLAN['mapping'][p]
              for g, p in SOURCE_GUIDS.items() if p in PLAN['mapping']}
PATH_GUIDS = {p: g for g, p in GUID_PATHS.items()}
FOCUS = {'Characters', 'Mounts', 'Pets', 'Monsters', 'Equipment'}

def read(path):
    return path.read_text('utf-8-sig', errors='replace')

def object_ref(value):
    guid = re.search(r'guid: ([0-9a-f]{32})', value)
    file_id = re.search(r'fileID: (-?\d+)', value)
    return {'guid': guid[1] if guid else '', 'file_id': int(file_id[1]) if file_id else 0}

def props(text, section):
    match = re.search(r'^    ' + section + r':\s*\n((?:    - .*(?:\n|$))*)', text, re.M)
    return dict(re.findall(r'^    - (\S+): (.+)$', match[1], re.M)) if match else {}

shaders = {}
for path in ROOT.rglob('*.shader'):
    value = read(path)
    name = re.search(r'^\s*Shader\s+"([^"]+)"', value, re.M)
    rel = path.relative_to(PROJECT).as_posix()
    shaders[PATH_GUIDS.get(rel, rel)] = {'name': name[1] if name else '?', 'path': rel}

materials = {}
for path in ROOT.rglob('*.mat'):
    value = read(path)
    rel = path.relative_to(PROJECT).as_posix()
    shader_ref = object_ref(re.search(r'^  m_Shader: (.+)$', value, re.M)[1])
    shader = shaders.get(shader_ref['guid'], {'name': 'BUILTIN_OR_UNRESOLVED:' + str(shader_ref['file_id']), 'path': GUID_PATHS.get(shader_ref['guid'], '')})
    textures = {}
    for match in re.finditer(r'^    - (\S+):\n        m_Texture: (.+)\n        m_Scale: (.+)\n        m_Offset: (.+)', value, re.M):
        ref = object_ref(match[2])
        textures[match[1]] = dict(ref, path=GUID_PATHS.get(ref['guid'], ''), scale=match[3], offset=match[4])
    floats = {k: float(v) for k, v in props(value, 'm_Floats').items()}
    colors = {k: {c: float(n) for c, n in re.findall(r'([rgba]): ([^,}]+)', v)} for k, v in props(value, 'm_Colors').items()}
    guid = PATH_GUIDS.get(rel, '')
    materials[guid or rel] = {'path': rel, 'guid': guid, 'category': rel.split('/')[2],
        'shader': shader['name'], 'shader_path': shader['path'], 'shader_ref': shader_ref,
        'floats': floats, 'colors': colors, 'textures': textures,
        'render_queue': int(re.search(r'^  m_CustomRenderQueue: (-?\d+)', value, re.M)[1]),
        'keywords': re.findall(r'^  - ([_A-Z0-9]+)$', value, re.M),
        'prefabs': [], 'mesh_prefabs': [], 'particle_prefabs': []}

prefab_counts = collections.Counter()
unresolved_slots = []
embedded_slots = []
for path in ROOT.rglob('*.prefab'):
    rel = path.relative_to(PROJECT).as_posix()
    category = rel.split('/')[2]
    if category not in FOCUS:
        continue
    prefab_counts[category] += 1
    value = read(path)
    for block in re.split(r'(?=^--- !u!)', value, flags=re.M):
        kind = re.match(r'--- !u!(\d+)', block)
        if not kind:
            continue
        for match in re.finditer(r'^  m_Materials:\n((?:  - .+\n)+)', block, re.M):
            for line in match[1].splitlines():
                ref = object_ref(line)
                if ref['guid'] in materials:
                    mat = materials[ref['guid']]
                    mat['prefabs'].append(rel)
                    mat['particle_prefabs' if kind[1] == '199' else 'mesh_prefabs'].append(rel)
                elif ref['guid'] and ref['guid'] in GUID_PATHS:
                    embedded_slots.append(dict(ref, prefab=rel, asset=GUID_PATHS[ref['guid']], class_id=kind[1]))
                else:
                    unresolved_slots.append(dict(ref, prefab=rel, class_id=kind[1]))

for mat in materials.values():
    for key in ['prefabs', 'mesh_prefabs', 'particle_prefabs']:
        mat[key] = sorted(set(mat[key]))

# Resolve Unity built-in file IDs from read-only live Editor inspection.
# Built-in references share a GUID, so fileID is part of the lookup key.
lookup_path = WORK / 'UnityShaderLookup.tsv'
if lookup_path.exists():
    by_path = {m['path']: m for m in materials.values()}
    with lookup_path.open(encoding='utf-8-sig', newline='') as stream:
        lookup_rows = list(csv.DictReader(stream, delimiter='\t'))
    lookup = {}
    for row in lookup_rows:
        sample = by_path.get(row['material'])
        if sample:
            ref = sample['shader_ref']
            lookup[(ref['guid'], ref['file_id'])] = row
    for mat in materials.values():
        ref = mat['shader_ref']
        row = lookup.get((ref['guid'], ref['file_id']))
        if row:
            mat['shader'] = row['shader']
            mat['shader_path'] = row['shader_path']

def count_by(items, key):
    return dict(collections.Counter(m[key] for m in items).most_common())

def describe(items):
    return {'count': len(items), 'by_shader': count_by(items, 'shader'), 'by_category': count_by(items, 'category')}

focus = [m for m in materials.values() if m['category'] in FOCUS]
mesh = [m for m in materials.values() if m['mesh_prefabs']]
toon = [m for m in focus if m['shader'] in ('project-rs/unitychan_dswf_mobile', 'UnityChanToonShader/Toon_DoubleShadeWithFeather')]
feature_keys = ['_Is_BLD', '_GI_Intensity', '_Set_SystemShadowsToBase', '_Is_LightColor_Base',
    '_Is_LightColor_1st_Shade', '_Is_LightColor_2nd_Shade', '_MatCap', '_Is_NormalMapToBase',
    '_Is_NormalMapForMatCap', '_RimLight', '_RimLight_Power', '_Outline_Width', '_OUTLINE',
    '_CullMode', '_HighColor_Power', '_Is_SpecularToHighColor', '_EMISSIVE', '_Unlit_Intensity']

def toon_stats(items):
    same = []
    different = []
    missing_main = []
    nondefault_uv = []
    for m in items:
        tex = m['textures']
        main = tex.get('_MainTex', {})
        shades = [tex.get(k, {}) for k in ['_1st_ShadeMap', '_2nd_ShadeMap']]
        if not main.get('guid'):
            missing_main.append(m['path'])
        elif all(s.get('guid') == main['guid'] and s.get('scale') == main['scale'] and s.get('offset') == main['offset'] for s in shades):
            same.append(m['path'])
        else:
            different.append(m['path'])
        if main and (main['scale'] != '{x: 1, y: 1}' or main['offset'] != '{x: 0, y: 0}'):
            nondefault_uv.append(m['path'])
    return {'count': len(items), 'properties': {k: dict(collections.Counter(str(m['floats'].get(k, 'UNSAVED')) for m in items)) for k in feature_keys},
        'populated_texture_slots': dict(collections.Counter(k for m in items for k,t in m['textures'].items() if t['guid'])),
        'same_base_and_two_shade_texture_uv': len(same), 'different_shade_texture_uv': different,
        'missing_main_texture': missing_main, 'nondefault_main_uv': nondefault_uv,
        'nonblack_emission': [m['path'] for m in items if any(m['colors'].get('_Emissive_Color', {}).get(c, 0) > 0 for c in 'rgb')]}

summary = {
    'scope': 'Explicit .mat files. Renderer use means serialized m_Materials in character/mount/pet/monster/equipment prefabs; excludes inherited overrides and FBX embedded materials from that metric. Android variants counted as separate assets.',
    'all_materials': len(materials), 'focus_folder_materials': describe(focus),
    'by_folder': {k: describe([m for m in focus if m['category'] == k]) for k in sorted(FOCUS)},
    'serialized_renderer_materials': describe([m for m in materials.values() if m['prefabs']]),
    'serialized_mesh_renderer_materials': describe(mesh),
    'prefabs_scanned': dict(prefab_counts), 'unresolved_renderer_slots': unresolved_slots,
    'embedded_or_other_asset_slots': embedded_slots,
    'toon_focus': toon_stats(toon),
    'toon_mounts': toon_stats([m for m in toon if m['category'] == 'Mounts']),
    'toon_mesh_bound': toon_stats([m for m in toon if m['mesh_prefabs']]),
}
(WORK / 'MaterialAudit.json').write_text(json.dumps(list(materials.values()), ensure_ascii=False, indent=2), 'utf-8')
(WORK / 'Summary.json').write_text(json.dumps(summary, ensure_ascii=False, indent=2), 'utf-8')
fields = ['category','material','shader','mesh_prefab_count','particle_prefab_count','main_texture','base_map','fixed_light','GI','scene_shadows','rim','outline_width','cull','emission_rgb','render_queue']
with (WORK / 'MaterialAudit.csv').open('w', encoding='utf-8-sig', newline='') as stream:
    writer = csv.DictWriter(stream, fieldnames=fields)
    writer.writeheader()
    for m in focus:
        f, t = m['floats'], m['textures']
        writer.writerow(dict(zip(fields, [m['category'],m['path'],m['shader'],len(m['mesh_prefabs']),len(m['particle_prefabs']),
            t.get('_MainTex',{}).get('path',''), t.get('_BaseMap',{}).get('path',''),f.get('_Is_BLD',''),f.get('_GI_Intensity',''),
            f.get('_Set_SystemShadowsToBase',''),f.get('_RimLight',''),f.get('_Outline_Width',''),f.get('_CullMode',''),
            m['colors'].get('_Emissive_Color',{}),m['render_queue']])))
print(json.dumps({k:v for k,v in summary.items() if k not in ['toon_focus','toon_mounts','toon_mesh_bound','unresolved_renderer_slots','embedded_or_other_asset_slots']}, ensure_ascii=False, indent=2))
print('Toon focus:', len(toon), 'same base/shade textures:', summary['toon_focus']['same_base_and_two_shade_texture_uv'])
print('Toon mounts:', summary['toon_mounts']['count'])
print('Unresolved slots:',len(unresolved_slots),'embedded/other slots:',len(embedded_slots))
