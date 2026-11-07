"""Blender 5.x post-process for Tripo GLBs → Unity-ready glTF (separate .gltf + .bin + textures/).

Run:  /Applications/Blender.app/Contents/MacOS/Blender -b --python postprocess.py -- \
        --in pip.glb --out ../../unity/Assets/Models/pip --name pip --faces 12000 --tex 1024 --yaw 0

Steps: import GLB → join meshes → apply transforms → optional decimate to --faces →
resize every image to --tex (never upscales) → rotate --yaw degrees about the up axis →
origin at the feet (bbox min y = 0, x/z centred) → export GLTF_SEPARATE with PNG textures.
Reports bounds (glTF units, Y up) as JSON on the last line so the caller can record them.
"""
import argparse
import json
import math
import os
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser()
ap.add_argument("--in", dest="src", required=True)
ap.add_argument("--out", dest="out_dir", required=True)
ap.add_argument("--name", required=True)
ap.add_argument("--faces", type=int, default=0, help="target triangle count (0 = keep)")
ap.add_argument("--tex", type=int, default=1024, help="max texture size")
ap.add_argument("--yaw", type=float, default=0.0, help="degrees about the up axis before export")
ap.add_argument("--align-long", action="store_true", help="rotate so the longest horizontal axis lies along X (logs)")
ap.add_argument("--cut-below", type=float, default=0.0, help="delete geometry below this fraction of the height (e.g. a ground disc), then re-seat at y=0")
ap.add_argument("--squash-depth", type=float, default=0.0, help="scale the depth axis (glTF z) by this factor after the yaw (flat coins)")
ap.add_argument("--drop-mr", action="store_true", help="drop metallicRoughness textures (Pip/Lit uses scalars)")
args = ap.parse_args(argv)

# clean scene
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=os.path.abspath(args.src))

meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
if not meshes:
    raise SystemExit("no mesh objects imported")
# remove non-mesh objects (empties, lights, cameras)
for o in list(bpy.context.scene.objects):
    if o.type != "MESH":
        bpy.data.objects.remove(o, do_unlink=True)
for o in meshes:
    o.parent = None
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
obj.rotation_mode = "XYZ"
obj.name = args.name
obj.data.name = args.name
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

tris_before = sum(len(p.vertices) - 2 for p in obj.data.polygons)
if args.faces and tris_before > args.faces * 1.05:
    mod = obj.modifiers.new("decim", "DECIMATE")
    mod.ratio = args.faces / tris_before
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=mod.name)
tris_after = sum(len(p.vertices) - 2 for p in obj.data.polygons)

# optional: align the longest horizontal axis (PCA in the ground plane) with X
if args.align_long:
    n = len(obj.data.vertices)
    cx = sum(v.co.x for v in obj.data.vertices) / n
    cy = sum(v.co.y for v in obj.data.vertices) / n
    sxx = sum((v.co.x - cx) ** 2 for v in obj.data.vertices)
    syy = sum((v.co.y - cy) ** 2 for v in obj.data.vertices)
    sxy = sum((v.co.x - cx) * (v.co.y - cy) for v in obj.data.vertices)
    phi = 0.5 * math.atan2(2 * sxy, sxx - syy)
    obj.rotation_euler = (0.0, 0.0, -phi)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    print("ALIGN_LONG rotated by %.1f deg" % math.degrees(-phi))

# optional: cut away a ground disc / pedestal below a height fraction
if args.cut_below > 0:
    import bmesh
    zs = [v.co.z for v in obj.data.vertices]; z0, z1 = min(zs), max(zs)
    cut = z0 + (z1 - z0) * args.cut_below
    bm = bmesh.new(); bm.from_mesh(obj.data)
    kill = [v for v in bm.verts if v.co.z < cut]
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    bm.to_mesh(obj.data); bm.free()
    print("CUT_BELOW removed %d verts below %.3f" % (len(kill), cut))

# yaw about Blender's up axis (Z); glTF exporter turns it back into Y-up
if args.yaw:
    obj.rotation_euler = (0.0, 0.0, math.radians(args.yaw))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

# optional: flatten along the depth axis (Blender Y = glTF Z) after the yaw
if args.squash_depth > 0:
    for v in obj.data.vertices:
        v.co.y *= args.squash_depth
    obj.data.update()
    print("SQUASH_DEPTH x%.2f" % args.squash_depth)

# origin at feet, centred on x/y (Blender), i.e. x/z in glTF
bb = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
mn = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb)))
mx = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
shift = Vector(((mn.x + mx.x) / 2, (mn.y + mx.y) / 2, mn.z))
for v in obj.data.vertices:
    v.co -= shift
obj.location = (0, 0, 0)
size = mx - mn  # Blender axes: x, y(depth), z(up)

# textures: resize, rename, PNG
images = []
for mat in obj.data.materials:
    if not mat or not mat.use_nodes:
        continue
    for n in mat.node_tree.nodes:
        if n.type == "TEX_IMAGE" and n.image:
            images.append((mat, n))
seen = {}
for mat, n in images:
    img = n.image
    kind = "albedo"
    for link in mat.node_tree.links:
        if link.from_node == n:
            to = link.to_socket.name.lower()
            if "normal" in to or link.to_node.type == "NORMAL_MAP":
                kind = "normal"
            elif "roughness" in to or "metallic" in to or link.to_node.type == "SEPARATE_COLOR":
                kind = "mr"
    if args.drop_mr and kind == "mr":
        mat.node_tree.nodes.remove(n)
        continue
    if img.name in seen:
        continue
    seen[img.name] = kind
    w, h = img.size
    if max(w, h) > args.tex:
        s = args.tex / max(w, h)
        img.scale(max(1, int(w * s)), max(1, int(h * s)))
    img.name = f"{args.name}_{kind}"
    img.file_format = "PNG"
# a dropped texture leaves the socket at Principled defaults (roughness 0.5, metallic 0)

os.makedirs(args.out_dir, exist_ok=True)
out = os.path.join(os.path.abspath(args.out_dir), args.name + ".gltf")
bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
bpy.ops.export_scene.gltf(
    filepath=out,
    export_format="GLTF_SEPARATE",
    use_selection=True,
    export_texture_dir="textures",
    export_image_format="AUTO",
    export_apply=True,
    export_yup=True,
    export_normals=True,
    export_tangents=False,
    export_materials="EXPORT",
    export_animations=False,
    export_skins=False,
    export_morph=False,
    export_cameras=False,
    export_lights=False,
    export_extras=False,
)
print("POSTPROCESS " + json.dumps({
    "name": args.name,
    "out": out,
    "tris_before": tris_before,
    "tris_after": tris_after,
    "textures": seen,
    "size_gltf": {"x": round(size.x, 4), "y": round(size.z, 4), "z": round(size.y, 4)},
}))
