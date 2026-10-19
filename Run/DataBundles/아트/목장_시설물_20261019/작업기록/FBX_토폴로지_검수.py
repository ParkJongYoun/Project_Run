import bpy
import bmesh
import json
import sys
from collections import Counter
from pathlib import Path

args = sys.argv[sys.argv.index("--") + 1:]
source, output = map(Path, args[:2])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source))
records = []
for obj in bpy.context.scene.objects:
    if obj.type != "MESH":
        continue
    mesh = obj.data
    polygon_sizes = Counter(len(face.vertices) for face in mesh.polygons)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    records.append({
        "object": obj.name,
        "vertices": len(mesh.vertices),
        "faces": len(mesh.polygons),
        "quads": polygon_sizes.get(4, 0),
        "triangles": polygon_sizes.get(3, 0),
        "ngons": sum(count for sides, count in polygon_sizes.items() if sides > 4),
        "render_triangles": sum(len(face.vertices) - 2 for face in mesh.polygons),
        "loose_vertices": sum(not vertex.link_edges for vertex in bm.verts),
        "boundary_edges": sum(edge.is_boundary for edge in bm.edges),
        "non_manifold_edges": sum(not edge.is_manifold for edge in bm.edges),
        "uv_layers": len(mesh.uv_layers),
        "materials": [slot.material.name if slot.material else None for slot in obj.material_slots],
    })
    bm.free()
report = {"source": str(source), "objects": records,
          "total_faces": sum(record["faces"] for record in records),
          "total_quads": sum(record["quads"] for record in records),
          "total_render_triangles": sum(record["render_triangles"] for record in records)}
output.parent.mkdir(parents=True, exist_ok=True)
output.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print("RANCH_TOPOLOGY " + json.dumps(report, ensure_ascii=False))
