"""Render front / side / back of a GLB in one image with Workbench (textured), plus stats.
Blender -b --python preview.py -- in.glb out.png"""
import json, math, sys, os
import bpy
from mathutils import Vector
src, out = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=os.path.abspath(src))
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
for o in list(bpy.context.scene.objects):
    if o.type != "MESH":
        bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.object.select_all(action="DESELECT")
for o in meshes:
    o.parent = None; o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active; obj.rotation_mode = "XYZ"
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bb = [Vector(c) for c in obj.bound_box]
mn = Vector((min(v.x for v in bb), min(v.y for v in bb), min(v.z for v in bb)))
mx = Vector((max(v.x for v in bb), max(v.y for v in bb), max(v.z for v in bb)))
size = mx - mn
ctr = (mn + mx) / 2
for v in obj.data.vertices:
    v.co -= Vector((ctr.x, ctr.y, mn.z))
obj.location = (0, 0, 0)
w = max(size.x, size.y)
gap = w * 1.25
copies = [obj]
for i, rot in enumerate((90, 180)):
    c = obj.copy(); c.data = obj.data; c.rotation_mode = "XYZ"
    bpy.context.scene.collection.objects.link(c)
    c.rotation_euler = (0, 0, math.radians(rot))
    copies.append(c)
copies[0].location.x = -gap
copies[1].location.x = 0
copies[2].location.x = gap

# axis markers: red cube at Blender +X (glTF +X), blue cube at Blender -Y (glTF +Z, toward the camera)
def marker(name, loc, rgb):
    bpy.ops.mesh.primitive_cube_add(size=w * 0.12, location=loc)
    m = bpy.context.active_object; m.name = name
    mat = bpy.data.materials.new(name); mat.diffuse_color = (*rgb, 1); m.data.materials.append(mat)
marker("mk_px", (-gap + w * 0.75, 0, w * 0.06), (1, 0, 0))
marker("mk_pz", (-gap, -w * 0.75, w * 0.06), (0, 0, 1))
# camera: in front (-Y), slightly above
cam_d = bpy.data.cameras.new("cam"); cam_d.type = "ORTHO"; cam_d.ortho_scale = gap * 2 + w * 1.1
cam = bpy.data.objects.new("cam", cam_d); bpy.context.scene.collection.objects.link(cam)
elev = math.radians(12)
dist = 10 * w
cam.location = (0, -dist * math.cos(elev), size.z / 2 + dist * math.sin(elev))
cam.rotation_euler = (math.pi / 2 - elev, 0, 0)
bpy.context.scene.camera = cam
sc = bpy.context.scene
sc.render.engine = "BLENDER_WORKBENCH"
sc.display.shading.light = "STUDIO"
sc.display.shading.color_type = "TEXTURE"  # markers show their material colour too
sc.display.shading.show_shadows = False
sc.display.shading.show_cavity = False
sc.render.resolution_x = 960; sc.render.resolution_y = int(960 * (size.z * 1.15) / (gap * 2 + w * 1.1)) + 40
sc.render.film_transparent = False
world = bpy.data.worlds.new("w"); sc.world = world; world.color = (0.82, 0.86, 0.92)
sc.render.filepath = os.path.abspath(out)
sc.render.image_settings.file_format = "PNG"
bpy.ops.render.render(write_still=True)
tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
imgs = {im.name: list(im.size) for im in bpy.data.images if im.size[0]}
mats = [m.name for m in obj.data.materials if m]
print("STATS " + json.dumps({"src": os.path.basename(src), "tris": tris, "verts": len(obj.data.vertices),
      "size": [round(size.x, 3), round(size.y, 3), round(size.z, 3)], "images": imgs, "materials": mats}))
