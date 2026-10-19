import bpy
import bmesh
import json
import sys
from pathlib import Path

source, output = map(Path, sys.argv[sys.argv.index('--') + 1:][:2])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source))
records = []
for obj in bpy.context.scene.objects:
    if obj.type != 'MESH':
        continue
    mesh = obj.data
    count = (len(mesh.vertices), len(mesh.polygons), len(mesh.uv_layers))
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    before = [face.normal.copy() for face in bm.faces]
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    flipped = sum(a.dot(face.normal) < 0 for a, face in zip(before, bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    assert count == (len(mesh.vertices), len(mesh.polygons), len(mesh.uv_layers))
    records.append({'object': obj.name, 'vertices': count[0], 'faces': count[1], 'uv_layers': count[2], 'flipped_faces': flipped})
output.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.export_scene.fbx(filepath=str(output), use_selection=False, object_types={'MESH'},
    use_mesh_modifiers=False, bake_anim=False, path_mode='COPY', embed_textures=True,
    axis_forward='-Z', axis_up='Y', add_leaf_bones=False)
output.with_suffix('.json').write_text(json.dumps(records, ensure_ascii=False, indent=2), encoding='utf-8')
print('REPAIRED ' + json.dumps(records))
