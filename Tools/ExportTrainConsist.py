# Run through Blender MCP. Original scenes and source .blend files are preserved.
import bpy, os, json
from mathutils import Matrix
project='/Users/limseth/Projects/Eternal_Steam/Eternal_Steam'
source=bpy.data.scenes['TRAIN_A_CONSIST_V3']
source.frame_set(1)
s=bpy.data.scenes.new('UNITY_TRAIN_EXPORT')
mp={}
for o in source.objects:
    if o.type not in {'MESH','FONT','EMPTY'}:continue
    n=o.copy()
    if o.data:n.data=o.data.copy()
    s.collection.objects.link(n);mp[o]=n
for o,n in mp.items():
    if o.parent:
        n.parent=mp[o.parent];n.matrix_parent_inverse=o.matrix_parent_inverse.copy();n.matrix_basis=o.matrix_basis.copy()
    if n.animation_data:
        for f in n.animation_data.drivers:
            for v in f.driver.variables:
                for t in v.targets:
                    if t.id in mp:t.id=mp[t.id]
bpy.context.window.scene=s;s.frame_start=1;s.frame_end=49;s.render.fps=24;s.frame_set(1)
loco=bpy.data.objects.new('Locomotive',None);s.collection.objects.link(loco)
for o in list(s.objects):
    if o==loco or o.parent or o.name.startswith(('CARGO_ROOT','FOUNDATION_ROOT')):continue
    mw=o.matrix_world.copy();o.parent=loco;o.matrix_world=mw
# Keep driven parts and pivot hierarchy; combine only rigid geometry sharing one parent.
groups={}
for o in list(s.objects):
    if o.type in {'MESH','FONT'} and not o.animation_data:
        groups.setdefault(o.parent,[]).append(o)
for parent,objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.convert(target='MESH')
    bpy.ops.object.join()
    bpy.context.object.name=(parent.name if parent else 'Train')+'_Rigid'
s.frame_set(1);bpy.context.view_layer.update()
# Exporter samples constraints/drivers every frame; no Blender drivers are needed in Unity.
folder=project+'/Assets/EternalSteam/Art/Train';os.makedirs(folder,exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=folder+'/TrainConsist.fbx',use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=False,bake_anim_step=1,bake_anim_simplify_factor=0,add_leaf_bones=False)
report={'source':'Docs/Art/Train/model-v3-wagons/train-a-v3-consist.blend','export':'Assets/EternalSteam/Art/Train/TrainConsist.fbx','objects':len(s.objects),'meshes':sum(o.type=='MESH' for o in s.objects),'frames':[1,49],'fps':24,'sourceObjects':len(source.objects),'groups':len(groups),'materials':[m.name for m in bpy.data.materials]}
with open(project+'/Docs/Measurements/2026-10-02-train-hud-integration/blender-export.json','w') as f:json.dump(report,f,indent=2)
bpy.context.window.scene=source;source.frame_set(1)
print(json.dumps(report))
