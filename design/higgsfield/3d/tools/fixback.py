"""Remove the 'ghost face' Tripo paints on the back of a character's head.
Back-facing faces (normal toward glTF -Z, i.e. Blender +Y after import) inside a height zone get the median
albedo of the SIDE-facing faces at the same height, and a flat normal. Without --zone it only prints a
height profile (per 2 % slice: median colour of side faces | back faces) so the zone can be chosen.
Blender -b --python fixback.py -- --in model.gltf [--zone 0.42 0.68] [--out-suffix ""]
Writes the fixed textures back over textures/<name>_albedo.jpg and <name>_normal.png."""
import argparse, os, sys, math
import bpy, numpy as np
from mathutils import Vector
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
ap = argparse.ArgumentParser(); ap.add_argument("--in", dest="src", required=True)
ap.add_argument("--zone", nargs=2, type=float); ap.add_argument("--back", type=float, default=0.3, help="min normal.y (back)")
ap.add_argument("--side", type=float, default=0.6, help="min |normal.x| (side)")
ap.add_argument("--remap", action="store_true", help="give the fixed faces their own texels in a free part of the atlas (needed when Tripo shares texels between front and back)")
ap.add_argument("--behind", type=float, default=0.05, help="face centre must be this fraction of the depth behind the bbox centre")
args = ap.parse_args(argv)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=os.path.abspath(args.src))
obj = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
me = obj.data; uvl = me.uv_layers.active.data
zs = [v.co.z for v in me.vertices]; zmin, zmax = min(zs), max(zs); H = zmax - zmin
ys = [v.co.y for v in me.vertices]; ymid = (min(ys) + max(ys)) / 2; D = max(ys) - min(ys)
mat = me.materials[0]; alb = nrm = None
for n in mat.node_tree.nodes:
    if n.type == "TEX_IMAGE" and n.image:
        for l in mat.node_tree.links:
            if l.from_node == n:
                if l.to_node.type == "NORMAL_MAP": nrm = n.image
                elif "Base Color" in l.to_socket.name: alb = n.image
assert alb is not None, "no albedo"
W, Hh = alb.size
A = np.array(alb.pixels[:], dtype=np.float32).reshape(Hh, W, 4)
N = np.array(nrm.pixels[:], dtype=np.float32).reshape(nrm.size[1], nrm.size[0], 4) if nrm else None
# classify faces
side_samples = {}  # slice -> list of rgb
back_faces = []
for p in me.polygons:
    nrm_v = p.normal; c = p.center; frac = (c.z - zmin) / H; sl = int(frac * 50)
    uvs = [uvl[li].uv for li in p.loop_indices]
    uc = sum(uvs, Vector((0, 0))) / len(uvs)
    px, py = int(uc.x * W) % W, int(uc.y * Hh) % Hh
    if abs(nrm_v.x) > args.side and abs(nrm_v.y) < 0.5:
        side_samples.setdefault(sl, []).append(A[py, px, :3])
    if nrm_v.y > args.back and c.y > ymid + args.behind * D:
        back_faces.append((frac, sl, uvs, A[py, px, :3]))
def hexc(v): return "#%02x%02x%02x" % tuple(int(max(0, min(1, x)) * 255) for x in v)
if not args.zone:
    print("PROFILE slice  height   side(median)  back(median)")
    back_by = {}
    for frac, sl, uvs, col in back_faces: back_by.setdefault(sl, []).append(col)
    for sl in range(50):
        s = np.median(np.array(side_samples[sl]), axis=0) if sl in side_samples else None
        b = np.median(np.array(back_by[sl]), axis=0) if sl in back_by else None
        print("PROFILE %2d  %.2f  %s  %s  n_side=%d n_back=%d" % (sl, sl / 50, hexc(s) if s is not None else "-", hexc(b) if b is not None else "-", len(side_samples.get(sl, [])), len(back_by.get(sl, []))))
    raise SystemExit
lo, hi = args.zone
def fill_tri(img, uvs, col):
    h, w = img.shape[:2]
    pts = [(u.x * w, u.y * h) for u in uvs]
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    x0, x1 = max(0, int(min(xs)) - 1), min(w - 1, int(max(xs)) + 1)
    y0, y1 = max(0, int(min(ys)) - 1), min(h - 1, int(max(ys)) + 1)
    if x1 < x0 or y1 < y0: return 0
    gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    (ax, ay), (bx, by), (cx, cy) = pts[:3]
    det = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay)
    if abs(det) < 1e-9: return 0
    l1 = ((bx - gx) * (cy - gy) - (cx - gx) * (by - gy)) / det
    l2 = ((cx - gx) * (ay - gy) - (ax - gx) * (cy - gy)) / det
    l3 = 1 - l1 - l2
    eps = -0.02  # slight dilation
    m = (l1 >= eps) & (l2 >= eps) & (l3 >= eps)
    sub = img[y0:y1 + 1, x0:x1 + 1]
    sub[m, :3] = col
    return int(m.sum())
def raster_mask(mask, uvs):
    h, w = mask.shape
    pts = [(u.x * w, u.y * h) for u in uvs]
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
    x0, x1 = max(0, int(min(xs)) - 1), min(w - 1, int(max(xs)) + 1)
    y0, y1 = max(0, int(min(ys)) - 1), min(h - 1, int(max(ys)) + 1)
    if x1 < x0 or y1 < y0: return
    gx, gy = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
    (ax, ay), (bx, by), (cx, cy) = pts[:3]
    det = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay)
    if abs(det) < 1e-9: return
    l1 = ((bx - gx) * (cy - gy) - (cx - gx) * (by - gy)) / det
    l2 = ((cx - gx) * (ay - gy) - (ax - gx) * (cy - gy)) / det
    l3 = 1 - l1 - l2
    mask[y0:y1 + 1, x0:x1 + 1] |= (l1 >= -0.1) & (l2 >= -0.1) & (l3 >= -0.1)

if args.remap:
    cov = np.zeros((Hh, W), dtype=bool)
    for p in me.polygons:
        raster_mask(cov, [uvl[li].uv for li in p.loop_indices])
    BS = 32; free = []
    for by in range(0, Hh - BS + 1, BS // 2):
        for bx in range(0, W - BS + 1, BS // 2):
            if not cov[by:by + BS, bx:bx + BS].any():
                free.append((bx, by))
                cov[by:by + BS, bx:bx + BS] = True  # reserve
    # slices used by the zone faces
    used = sorted({sl for frac, sl, uvs, col in back_faces if lo <= frac <= hi})
    if len(free) < len(used):
        raise SystemExit("REMAP not enough free atlas space: need %d blocks, found %d" % (len(used), len(free)))
    block_of = {}
    for k, sl in enumerate(used):
        cand = []
        for d in (0, -1, 1, -2, 2):
            if sl + d in side_samples: cand = side_samples[sl + d]; break
        scol = np.median(np.array(cand), axis=0) if cand else np.array([0.5, 0.5, 0.5], dtype=np.float32)
        bx, by = free[k]; block_of[sl] = (bx, by)
        A[by:by + BS, bx:bx + BS, :3] = scol
        if N is not None:
            sN = N.shape[0] / Hh
            N[int(by * sN):int((by + BS) * sN), int(bx * sN):int((bx + BS) * sN), :3] = np.array([0.5, 0.5, 1.0], dtype=np.float32)
    n_faces = 0
    for p in me.polygons:
        c = p.center; frac = (c.z - zmin) / H; sl = int(frac * 50)
        if not (p.normal.y > args.back and c.y > ymid + args.behind * D and lo <= frac <= hi): continue
        bx, by = block_of[sl]; cx, cy = bx + BS / 2, by + BS / 2; r = BS / 2 - 6
        tri = [(cx - r, cy - r), (cx + r, cy - r), (cx, cy + r)]
        for i, li in enumerate(p.loop_indices):
            tx, ty = tri[i % 3]
            uvl[li].uv = (tx / W, ty / Hh)
        n_faces += 1
    # export the mesh first (the exporter rewrites the texture files from the ORIGINAL bytes), then save the pixels over them
    out = os.path.abspath(args.src)
    path_a = bpy.path.abspath(alb.filepath); path_n = bpy.path.abspath(nrm.filepath) if N is not None else None
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.gltf(filepath=out, export_format="GLTF_SEPARATE", export_texture_dir="textures", export_image_format="AUTO",
        export_apply=True, export_yup=True, export_normals=True, export_tangents=False, export_materials="EXPORT",
        export_animations=False, export_skins=False, export_morph=False, export_cameras=False, export_lights=False, export_extras=False)
    def write_image(name, arr, path, fmt):
        img = bpy.data.images.new(name, arr.shape[1], arr.shape[0], alpha=True)
        img.pixels.foreach_set(arr.ravel()); img.filepath_raw = path; img.file_format = fmt
        img.save(filepath=path, quality=92)
    write_image("fix_albedo", A, path_a, "JPEG" if path_a.lower().endswith(".jpg") else "PNG")
    if N is not None: write_image("fix_normal", N, path_n, "PNG")
    print("FIXBACK remap faces=%d blocks=%d free=%d zone=%.2f-%.2f out=%s" % (n_faces, len(used), len(free), lo, hi, out))
    raise SystemExit

n_faces = n_tex = 0
for frac, sl, uvs, col in back_faces:
    if frac < lo or frac > hi: continue
    # median side colour in this slice, falling back to neighbours
    cand = []
    for d in (0, -1, 1, -2, 2):
        if sl + d in side_samples: cand = side_samples[sl + d]; break
    if not cand: continue
    scol = np.median(np.array(cand), axis=0)
    n_faces += 1
    n_tex += fill_tri(A, uvs, scol)
    if N is not None: fill_tri(N, uvs, np.array([0.5, 0.5, 1.0], dtype=np.float32))
alb.pixels.foreach_set(A.ravel()); alb.filepath_raw = bpy.path.abspath(alb.filepath); 
path_a = bpy.path.abspath(alb.filepath); alb.file_format = "JPEG" if path_a.lower().endswith(".jpg") else "PNG"
alb.save(filepath=path_a, quality=92)
if N is not None:
    nrm.pixels.foreach_set(N.ravel()); path_n = bpy.path.abspath(nrm.filepath); nrm.file_format = "PNG"; nrm.save(filepath=path_n)
print("FIXBACK faces=%d texels=%d zone=%.2f-%.2f albedo=%s" % (n_faces, n_tex, lo, hi, path_a))
