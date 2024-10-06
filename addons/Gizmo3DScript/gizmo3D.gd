# Translated from C++ to GDScript with alterations, source from:
# - https://github.com/godotengine/godot/blob/master/editor/plugins/node_3d_editor_plugin.h
# - https://github.com/godotengine/godot/blob/master/editor/plugins/node_3d_editor_plugin.cpp
class_name Gizmo3D;
extends Node3D

const DEFAULT_FLOAT_STEP := 0.001
const MAX_Z := 1000000.0

const GIZMO_ARROW_SIZE := 0.35
const GIZMO_RING_HALF_WIDTH := .1
const GIZMO_PLANE_SIZE := .2
const GIZMO_PLANE_DST := .3
const GIZMO_CIRCLE_SIZE := 1.1
const GIZMO_SCALE_OFFSET := GIZMO_CIRCLE_SIZE - .3
const GIZMO_ARROW_OFFSET := GIZMO_CIRCLE_SIZE + .15

@export
var mode := ToolMode.ALL

@export_flags_3d_render
var layers := 1;

@export
var target : Node3D
var snapping : bool
var message : String

var editing : bool

@export_group("Style")
@export
var size := 80.0
@export
var show_axes := true
@export
var show_selection_box := true

@export
var opacity := .9
@export
var colors := [
	Color(0.96, 0.20, 0.32),
	Color(0.53, 0.84, 0.01),
	Color(0.16, 0.55, 0.96)
]
@export
var selection_box_color := Color(1.0, .5, 0)

@export_group("Position")
@export
var local_coords : bool
@export_range(0.0, 360.0)
var rotate_snap := 15.0
@export_range(0.0, 10.0)
var translate_snap := 1.0
@export_range(0.0, 5.0)
var scale_snap = .25

var _move_gizmo := [ArrayMesh, 3]
var _move_plane_gizmo := [ArrayMesh, 3]
var _rotate_gizmo := [ArrayMesh, 4]
var _scale_gizmo := [ArrayMesh, 3]
var _scale_plane_gizmo := [ArrayMesh, 3]
var _axis_gizmo := [ArrayMesh, 3]
var _gizmo_color := [StandardMaterial3D, 3]
var _plane_gizmo_color := [StandardMaterial3D, 3]
var _rotate_gizmo_color := [StandardMaterial3D, 4]
var _gizmo_color_hl := [StandardMaterial3D, 3]
var _plane_gizmo_color_hl := [StandardMaterial3D, 3]
var _rotate_gizmo_color_hl := [StandardMaterial3D, 3]

var _move_gizmo_instance := [3]
var _move_plane_gizmo_instance := [3]
var _rotate_gizmo_instance := [3]
var _scale_gizmo_instance := [3]
var _scale_plane_gizmo_instance := [3]
var _axis_gizmo_instance := [3]

var _selection_box : ArrayMesh
var _selection_box_xray : ArrayMesh
var _selection_box_mat : StandardMaterial3D
var _selection_box_xray_mat : StandardMaterial3D
var _sbox_instance : RID
var _sbox_instance_offset : RID
var _sbox_xray_instance : RID
var _sbox_xray_instance_offset : RID

var _edit := EditData.new()
var _gizmo_scale := 1.0

enum ToolMode { ALL, MOVE, ROTATE, SCALE }
enum TransformMode { NONE, ROTATE, TRANSLATE, SCALE }
enum TransformPlane { VIEW, X, Y, Z, YZ, XZ, XY }

func _ready() -> void:
	_init_indicators()
	_set_colors()
	_init_gizmo_instance()
	_update_transform_gizmo_view()
	visibility_changed.connect(func(): _set_visibility(visible))

func _unhandled_input(event: InputEvent) -> void:
	if !visible:
		editing = false
		return
	if event is InputEventKey and event.keycode == KEY_CTRL:
		snapping = event.pressed
	elif event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT:
		editing = event.pressed
		if !editing:
			return
		edit.mouse_pos = event.position
		editing = _transform_gizmo_select(event.position)
	elif event is InputEventMouseMotion:
		if editing and event.button_mask.has_flag(MOUSE_BUTTON_MASK_LEFT):
			edit.mouse_pos = event.position
			_update_transform(false)
			return
		_transform_gizmo_select(event.position, true)

func _process(delta: float) -> void:
	if !target || is_instance_valid(target) || target.is_queued_for_deletion():
		return
	position = target.position
	_update_transform_gizmo_view()

func _exit_tree() -> void:
	for i in range(3):
		RenderingServer.free_rid(_move_gizmo_instance[i])
		RenderingServer.free_rid(_move_plane_gizmo_instance[i])
		RenderingServer.free_rid(_rotate_gizmo_instance[i])
		RenderingServer.free_rid(_scale_gizmo_instance[i])
		RenderingServer.free_rid(_scale_plane_gizmo_instance[i])
		RenderingServer.free_rid(_axis_gizmo_instance[i])
	RenderingServer.free_rid(_rotate_gizmo_instance[3])
	
	RenderingServer.free_rid(_sbox_instance)
	RenderingServer.free_rid(_sbox_instance_offset)
	RenderingServer.free_rid(_sbox_xray_instance)
	RenderingServer.free_rid(_sbox_xray_instance_offset)

func _init_gizmo_instance() -> void:
	for i in range(3):
		_move_gizmo_instance[i] = RenderingServer.instance_create()
		RenderingServer.instance_set_base(_move_gizmo_instance[i], _move_gizmo[i].get_rid())
		RenderingServer.instance_set_scenario(_move_gizmo_instance[i], get_tree().root.world_3d.scenario)
		RenderingServer.instance_geometry_set_cast_shadows_setting(_move_gizmo_instance[i], RenderingServer.ShadowCastingSetting.SHADOW_CASTING_SETTING_OFF)
		RenderingServer.instance_set_layer_mask(_move_gizmo_instance[i], layers)
		RenderingServer.instance_geometry_set_flag(_move_gizmo_instance[i], RenderingServer.InstanceFlags.INSTANCE_FLAG_IGNORE_OCCLUSION_CULLING, true)
		RenderingServer.instance_geometry_set_flag(_move_gizmo_instance[i], RenderingServer.InstanceFlags.INSTANCE_FLAG_USE_BAKED_LIGHT, false)
		
		_move_plane_gizmo_instance[i] = RenderingServer.instance_create()
		RenderingServer.instance_set_base(_move_plane_gizmo_instance[i], _move_plane_gizmo[i].get_rid())
		RenderingServer.instance_set_scenario(_move_plane_gizmo_instance[i], get_tree().root.world_3d.scenario)
		RenderingServer.instance_geometry_set_cast_shadows_setting(_move_plane_gizmo_instance[i], RenderingServer.ShadowCastingSetting.SHADOW_CASTING_SETTING_OFF)
		RenderingServer.instance_set_layer_mask(_move_plane_gizmo_instance[i], layers)
		RenderingServer.instance_geometry_set_flag(_move_plane_gizmo_instance[i], RenderingServer.InstanceFlags.INSTANCE_FLAG_IGNORE_OCCLUSION_CULLING, true)
		RenderingServer.instance_geometry_set_flag(_move_plane_gizmo_instance[i], RenderingServer.InstanceFlags.INSTANCE_FLAG_USE_BAKED_LIGHT, false)
		
		_rotate_gizmo_instance[i] = RenderingServer.instance_create()
		RenderingServer.instance_set_base(_rotate_gizmo_instance[i], _rotate_gizmo[i].get_rid())
		RenderingServer.instance_set_scenario(_rotate_gizmo_instance[i], get_tree().root.world_3d.scenario)
		RenderingServer.instance_geometry_set_cast_shadows_setting(_rotate_gizmo_instance[i], RenderingServer.ShadowCastingSetting.SHADOW_CASTING_SETTING_OFF)
		RenderingServer.instance_set_layer_mask(_rotate_gizmo_instance[i], layers)
		RenderingServer.instance_geometry_set_flag(_rotate_gizmo_instance[i], RenderingServer.InstanceFlags.INSTANCE_FLAG_IGNORE_OCCLUSION_CULLING, true)
		RenderingServer.instance_geometry_set_flag(_rotate_gizmo_instance[i], RenderingServer.InstanceFlags.INSTANCE_FLAG_USE_BAKED_LIGHT, false)
		
		_scale_gizmo_instance[i] = RenderingServer.instance_create()
		RenderingServer.instance_set_base(_scale_gizmo_instance[i], _scale_gizmo[i].get_rid())
		RenderingServer.instance_set_scenario(_scale_gizmo_instance[i], get_tree().root.world_3d.scenario)
		RenderingServer.instance_geometry_set_cast_shadows_setting(_scale_gizmo_instance[i], RenderingServer.ShadowCastingSetting.SHADOW_CASTING_SETTING_OFF)
		RenderingServer.instance_set_layer_mask(_scale_gizmo_instance[i], layers)
		RenderingServer.instance_geometry_set_flag(_scale_gizmo_instance[i], RenderingServer.InstanceFlags.INSTANCE_FLAG_IGNORE_OCCLUSION_CULLING, true)
		RenderingServer.instance_geometry_set_flag(_scale_gizmo_instance[i], RenderingServer.InstanceFlags.INSTANCE_FLAG_USE_BAKED_LIGHT, false)
		
		_scale_plane_gizmo_instance[i] = RenderingServer.instance_create()
		RenderingServer.instance_set_base(_scale_plane_gizmo_instance[i], _scale_plane_gizmo[i].get_rid())
		RenderingServer.instance_set_scenario(_scale_plane_gizmo_instance[i], get_tree().root.world_3d.scenario)
		RenderingServer.instance_geometry_set_cast_shadows_setting(_scale_plane_gizmo_instance[i], RenderingServer.ShadowCastingSetting.SHADOW_CASTING_SETTING_OFF)
		RenderingServer.instance_set_layer_mask(_scale_plane_gizmo_instance[i], layers)
		RenderingServer.instance_geometry_set_flag(_scale_plane_gizmo_instance[i], RenderingServer.InstanceFlags.INSTANCE_FLAG_IGNORE_OCCLUSION_CULLING, true)
		RenderingServer.instance_geometry_set_flag(_scale_plane_gizmo_instance[i], RenderingServer.InstanceFlags.INSTANCE_FLAG_USE_BAKED_LIGHT, false)
		
		_axis_gizmo_instance[i] = RenderingServer.instance_create()
		RenderingServer.instance_set_base(_axis_gizmo_instance[i], _axis_gizmo[i].get_rid())
		RenderingServer.instance_set_scenario(_axis_gizmo_instance[i], get_tree().root.world_3d.scenario)
		RenderingServer.instance_geometry_set_cast_shadows_setting(_axis_gizmo_instance[i], RenderingServer.ShadowCastingSetting.SHADOW_CASTING_SETTING_OFF)
		RenderingServer.instance_set_layer_mask(_axis_gizmo_instance[i], layers)
		RenderingServer.instance_geometry_set_flag(_axis_gizmo_instance[i], RenderingServer.InstanceFlags.INSTANCE_FLAG_IGNORE_OCCLUSION_CULLING, true)
		RenderingServer.instance_geometry_set_flag(_axis_gizmo_instance[i], RenderingServer.InstanceFlags.INSTANCE_FLAG_USE_BAKED_LIGHT, false)
	
	_rotate_gizmo_instance[3] = RenderingServer.instance_create()
	RenderingServer.instance_set_base(_rotate_gizmo_instance[3], _rotate_gizmo[3].get_rid())
	RenderingServer.instance_set_scenario(_rotate_gizmo_instance[3], get_tree().root.world_3d.scenario)
	RenderingServer.instance_geometry_set_cast_shadows_setting(_rotate_gizmo_instance[3], RenderingServer.ShadowCastingSetting.SHADOW_CASTING_SETTING_OFF)
	RenderingServer.instance_set_layer_mask(_rotate_gizmo_instance[3], layers)
	RenderingServer.instance_geometry_set_flag(_rotate_gizmo_instance[3], RenderingServer.InstanceFlags.INSTANCE_FLAG_IGNORE_OCCLUSION_CULLING, true)
	RenderingServer.instance_geometry_set_flag(_rotate_gizmo_instance[3], RenderingServer.InstanceFlags.INSTANCE_FLAG_USE_BAKED_LIGHT, false)

func _init_indicators() -> void:
	# Inverted zxy.
	var ivec := Vector3(0, 0, - 1)
	var nivec := Vector3(-1, -1, 0)
	var ivec2 := Vector3(-1, 0, 0)
	var ivec3 := Vector3(0, -1, 0)
	
	for i in range(3):
		_move_gizmo[i] = ArrayMesh.new()
		_move_plane_gizmo[i] = ArrayMesh.new()
		_rotate_gizmo[i] = ArrayMesh.new()
		_scale_gizmo[i] = ArrayMesh.new()
		_scale_plane_gizmo[i] = ArrayMesh.new()
		_axis_gizmo[i] = ArrayMesh.new()
		
		var mat := StandardMaterial3D.new()
		mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		mat.disable_fog = true
		mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		mat.cull_mode = BaseMaterial3D.CULL_DISABLED
		GizmoHelper.set_on_top_of_alpha(mat)
		_gizmo_color[i] = mat
		_gizmo_color_hl[i] = mat.duplicate()
		
		# Translate
		var surfTool = SurfaceTool.new()
		surfTool.begin(Mesh.PRIMITIVE_TRIANGLES)
		
		# Arrow profile
		var arrow_points := 5
		var arrow := [
			nivec * 0.0 + ivec * GIZMO_ARROW_OFFSET,
			nivec * 0.01 + ivec * GIZMO_ARROW_OFFSET,
			nivec * 0.01 + ivec * GIZMO_ARROW_OFFSET,
			nivec * 0.12 + ivec * GIZMO_ARROW_OFFSET,
			nivec * 0.0 + ivec * (GIZMO_ARROW_OFFSET + GIZMO_ARROW_SIZE)
		];
		
		var arrow_sides := 16
		var arrow_sides_step := TAU / arrow_sides
		for k in range(arrow_sides):
			var maa := Basis(ivec, k * arrow_sides_step)
			var mbb := Basis(ivec, (k + 1) * arrow_sides_step)
			for j in range(arrow_points - 1):
				var apoints := [
					maa * arrow[j],
					mbb * arrow[j],
					mbb * arrow[j + 1],
					maa * arrow[j + 1]
				]
				surfTool.add_vertex(apoints[0])
				surfTool.add_vertex(apoints[1])
				surfTool.add_vertex(apoints[2])
				
				surfTool.add_vertex(apoints[0])
				surfTool.add_vertex(apoints[1])
				surfTool.add_vertex(apoints[2])
		surfTool.set_material(mat)
		surfTool.commit(_move_gizmo[i])
		
		# Plane translation
		surfTool = SurfaceTool.new()
		surfTool.begin(Mesh.PRIMITIVE_TRIANGLES)
		
		var vec := ivec2 - ivec3
		var plane := [
			vec * GIZMO_PLANE_DST,
			vec * GIZMO_PLANE_DST + ivec2 * GIZMO_PLANE_SIZE,
			vec * (GIZMO_PLANE_DST + GIZMO_PLANE_SIZE),
			vec * GIZMO_PLANE_DST - ivec3 * GIZMO_PLANE_SIZE
		]
		
		var ma := Basis(ivec, PI / 2)
		var points := [
			ma * plane[0],
			ma * plane[1],
			ma * plane[2],
			ma * plane[3]
		]
		surfTool.add_vertex(points[0])
		surfTool.add_vertex(points[1])
		surfTool.add_vertex(points[2])

		surfTool.add_vertex(points[0])
		surfTool.add_vertex(points[2])
		surfTool.add_vertex(points[3])
		
		var plane_mat := StandardMaterial3D.new()
		plane_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		plane_mat.disable_fog = true
		plane_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		plane_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
		GizmoHelper.set_on_top_of_alpha(plane_mat)
		_plane_gizmo_color[i] = plane_mat
		surfTool.set_material(plane_mat)
		surfTool.commit(_move_plane_gizmo[i])
		_plane_gizmo_color_hl[i] = plane_mat.duplicate()
		
		# Rotation
		surfTool = SurfaceTool.new()
		surfTool.begin(Mesh.PRIMITIVE_TRIANGLES)
		
		var CIRCLE_SEGMENTS := 128
		var CIRCLE_SEGMENT_THICKNESS := 3
		
		var step := TAU / CIRCLE_SEGMENTS
		for j in range(CIRCLE_SEGMENTS):
			var basis := Basis(ivec, j * step)
			var vertex := basis * (ivec2 * GIZMO_CIRCLE_SIZE)
			for k in range(CIRCLE_SEGMENT_THICKNESS):
				var ofs := Vector2(cos((TAU * k) / CIRCLE_SEGMENT_THICKNESS), sin((TAU * k) / CIRCLE_SEGMENT_THICKNESS))
				var normal := ivec * ofs.x + ivec2 * ofs.y
				surfTool.set_normal(basis * normal)
				surfTool.add_vertex(vertex)
		
		for j in range(CIRCLE_SEGMENTS):
			for k in range(CIRCLE_SEGMENT_THICKNESS):
				var current_ring := j * CIRCLE_SEGMENT_THICKNESS
				var next_ring := ((j + 1) % CIRCLE_SEGMENTS) * CIRCLE_SEGMENT_THICKNESS
				var current_segment := k
				var next_segment := (k + 1) % CIRCLE_SEGMENT_THICKNESS
				
				surfTool.add_index(current_ring + next_segment)
				surfTool.add_index(current_ring + current_segment)
				surfTool.add_index(next_ring + current_segment)

				surfTool.add_index(next_ring + current_segment)
				surfTool.add_index(next_ring + next_segment)
				surfTool.add_index(current_ring + next_segment)
		
		var rotate_shader := Shader.new()
		rotate_shader.code = """
// 3D editor rotation manipulator gizmo shader.

shader_type spatial;

render_mode unshaded, depth_test_disabled, fog_disabled;

uniform vec4 albedo;

mat3 orthonormalize(mat3 m) {
	vec3 x = normalize(m[0]);
	vec3 y = normalize(m[1] - x * dot(x, m[1]));
	vec3 z = m[2] - x * dot(x, m[2]);
	z = normalize(z - y * (dot(y, m[2])));
	return mat3(x,y,z);
}

void vertex() {
	mat3 mv = orthonormalize(mat3(MODELVIEW_MATRIX));
	vec3 n = mv * VERTEX;
	float orientation = dot(vec3(0.0, 0.0, -1.0), n);
	if (orientation <= 0.005) {
		VERTEX += NORMAL * 0.02;
	}
}

void fragment() {
	ALBEDO = albedo.rgb;
	ALPHA = albedo.a;"""
		
		var rotate_mat := ShaderMaterial.new()
		rotate_mat.render_priority = Material.RENDER_PRIORITY_MAX
		rotate_mat.shader = rotate_shader
		_rotate_gizmo_color[i] = rotate_mat
		
		var arrays := surfTool.commit_to_arrays()
		_rotate_gizmo[i].add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
		_rotate_gizmo[i].surface_set_material(0, rotate_mat)
		
		_rotate_gizmo_color_hl[i] = rotate_mat.duplicate()
		
		if i == 2: # Rotation white outline
			var border_mat = rotate_mat.duplicate()
			
			var border_shader = Shader.new()
			border_shader.code = """
// 3D editor rotation manipulator gizmo shader (white outline).

shader_type spatial;

render_mode unshaded, depth_test_disabled, fog_disabled;

uniform vec4 albedo;

mat3 orthonormalize(mat3 m) {
	vec3 x = normalize(m[0]);
	vec3 y = normalize(m[1] - x * dot(x, m[1]));
	vec3 z = m[2] - x * dot(x, m[2]);
	z = normalize(z - y * (dot(y, m[2])));
	return mat3(x, y, z);
}

void vertex() {
	mat3 mv = orthonormalize(mat3(MODELVIEW_MATRIX));
	mv = inverse(mv);
	VERTEX += NORMAL * 0.008;
	vec3 camera_dir_local = mv * vec3(0.0, 0.0, 1.0);
	vec3 camera_up_local = mv * vec3(0.0, 1.0, 0.0);
	mat3 rotation_matrix = mat3(cross(camera_dir_local, camera_up_local), camera_up_local, camera_dir_local);
	VERTEX = rotation_matrix * VERTEX;
}

void fragment() {
	ALBEDO = albedo.rgb;
	ALPHA = albedo.a;
}"""
			border_mat.shader = border_shader
			_rotate_gizmo_color[3] = border_mat
			
			_rotate_gizmo[3] = ArrayMesh.new()
			_rotate_gizmo[3].add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
			_rotate_gizmo[3].surface_set_material(0, border_mat)
		
		# Scale
		surfTool = SurfaceTool.new()
		surfTool.begin(Mesh.PRIMITIVE_TRIANGLES)
		
		# Cube arrow profile
		arrow_points = 6
		arrow = [
			nivec * 0.0 + ivec * 0.0,
			nivec * 0.01 + ivec * 0.0,
			nivec * 0.01 + ivec * 1.0 * GIZMO_SCALE_OFFSET,
			nivec * 0.07 + ivec * 1.0 * GIZMO_SCALE_OFFSET,
			nivec * 0.07 + ivec * 1.2 * GIZMO_SCALE_OFFSET,
			nivec * 0.0 + ivec * 1.2 * GIZMO_SCALE_OFFSET
		]
		
		arrow_sides = 4
		arrow_sides_step = TAU / arrow_sides
		for k in range(4):
			var maa := Basis(ivec, k * arrow_sides_step)
			var mbb := Basis(ivec, (k + 1) * arrow_sides_step)
			for j in range(arrow_points - 1):
				var apoints := [
					maa * arrow[j],
					mbb * arrow[j],
					mbb * arrow[j + 1],
					maa * arrow[j + 1]
				]
				surfTool.add_vertex(apoints[0])
				surfTool.add_vertex(apoints[1])
				surfTool.add_vertex(apoints[2])

				surfTool.add_vertex(apoints[0])
				surfTool.add_vertex(apoints[2])
				surfTool.add_vertex(apoints[3])
		surfTool.set_material(mat)
		surfTool.commit(_scale_gizmo[i])
		
		# Plane scale
		surfTool = SurfaceTool.new()
		surfTool.begin(Mesh.PRIMITIVE_TRIANGLES)
		
		vec = ivec2 - ivec3
		plane = [
			vec * GIZMO_PLANE_DST,
			vec * GIZMO_PLANE_DST + ivec2 * GIZMO_PLANE_SIZE,
			vec * (GIZMO_PLANE_DST + GIZMO_PLANE_SIZE),
			vec * GIZMO_PLANE_DST - ivec3 * GIZMO_PLANE_SIZE
		]
		
		ma = Basis(ivec, PI / 2)
		
		points = [
			ma * plane[0],
			ma * plane[1],
			ma * plane[2],
			ma * plane[3]
		]
		surfTool.add_vertex(points[0])
		surfTool.add_vertex(points[1])
		surfTool.add_vertex(points[2])

		surfTool.add_vertex(points[0])
		surfTool.add_vertex(points[2])
		surfTool.add_vertex(points[3])
		
		plane_mat = StandardMaterial3D.new()
		plane_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
		plane_mat.disable_fog = true
		plane_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
		plane_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
		GizmoHelper.set_on_top_of_alpha(plane_mat)
		_plane_gizmo_color[i] = plane_mat
		surfTool.set_material(plane_mat)
		surfTool.commit(_scale_plane_gizmo[i])
		
		_plane_gizmo_color_hl[i] = plane_mat.duplicate()
		
		# Lines to visualize transforms locked to an axis/plane
		surfTool = SurfaceTool.new()
		surfTool.begin(Mesh.PRIMITIVE_TRIANGLES)
		
		vec = Vector3()
		vec[i] = 1
		# line extending through infinity(ish)
		surfTool.add_vertex(vec * -1048576);
		surfTool.add_vertex(Vector3());
		surfTool.add_vertex(vec * 1048576);
		surfTool.set_material(_gizmo_color_hl[i]);
		surfTool.commit(_axis_gizmo[i]);
	_generate_selection_boxes()

func _set_colors() -> void:
	for i in range(3):
		var col := Color(colors[i], colors[i].a * opacity)
		_gizmo_color[i].albedo_color = col
		_plane_gizmo_color[i].albedo_color = col
		_rotate_gizmo_color[i].albedo_color = col
		
		var albedo = Color.from_hsv(col.h, .25, 1.0, 1.0)
		_gizmo_color_hl[i].albedo_color = albedo
		_plane_gizmo_color_hl[i].albedo_color = albedo
		_rotate_gizmo_color[i].set_shader_parameter("albedo", albedo)
	_rotate_gizmo_color[3].set_shader_parameter("albedo", Color(.75, .75, .75, opacity / 3.0))

func _update_transform_gizmo_view() -> void:
	if !visible:
		_set_visibility(false)
		return
	
	var camera := get_viewport().get_camera_3d()
	var xform := transform
	var camera_transform := camera.get_camera_transform()
	
	if xform.origin.is_equal_approx(camera_transform.origin):
		_set_visibility(false)
		return
	
	var camz := -camera_transform.basis.z.normalized()
	var camy := -camera_transform.basis.y.normalized()
	var p := Plane(camz, camera_transform.origin)
	var gizmoD := max(abs(p.distance_to(xform.origin)), 0.00001)
	var d0 = camera.unproject_position(camera_transform.origin + camz * gizmoD).y
	var d1 = camera.unproject_position(camera_transform.origin + camz * gizmoD + camy).y
	var dd = max(abs(d0 - d1), 0.00001)
	
	_gizmo_scale = size / abs(dd)
	var scale := Vector3.ONE * _gizmo_scale
	
	# if the determinant is zero, we should disable the gizmo from being rendered
	# this prevents supplying bad values to the renderer and then having to filter it out again
	if xform.basis.determinant() == 0:
		_set_visibility(false)
		return
	
	for i in range(3):
		var axis_angle := Transform3D()
		if xform.basis[i].normalized().dot(xform.basis[(i + 1) % 3].normalized()) < 1.0:
			axis_angle = axis_angle.looking_at(xform.basis[i].normalized(), xform.basis[(i + 1) % 3].normalized())
		axis_angle.basis *= Basis.from_scale(scale)
		axis_angle.origin = xform.origin
		RenderingServer.instance_set_transform(_move_gizmo_instance[i], axis_angle)
		RenderingServer.instance_set_visible(_move_gizmo_instance[i], mode == ToolMode.ALL || mode == ToolMode.MOVE);
		RenderingServer.instance_set_visible(_move_gizmo_instance[i], true);
		RenderingServer.instance_set_transform(_move_plane_gizmo_instance[i], axis_angle);
		RenderingServer.instance_set_visible(_move_plane_gizmo_instance[i], mode == ToolMode.ALL || mode == ToolMode.MOVE);
		RenderingServer.instance_set_visible(_move_plane_gizmo_instance[i], true);
		RenderingServer.instance_set_transform(_rotate_gizmo_instance[i], axis_angle);
		RenderingServer.instance_set_visible(_rotate_gizmo_instance[i], mode == ToolMode.ALL || mode == ToolMode.ROTATE);
		RenderingServer.instance_set_transform(_scale_gizmo_instance[i], axis_angle);
		RenderingServer.instance_set_visible(_scale_gizmo_instance[i], mode == ToolMode.ALL || mode == ToolMode.SCALE);
		RenderingServer.instance_set_transform(_scale_plane_gizmo_instance[i], axis_angle);
		RenderingServer.instance_set_visible(_scale_plane_gizmo_instance[i], mode == ToolMode.SCALE);
		RenderingServer.instance_set_transform(_axis_gizmo_instance[i], xform);
	
	var show := show_axes
	RenderingServer.instance_set_visible(_axis_gizmo_instance[0], show and (edit.plane == TransformPlane.X || edit.plane == TransformPlane.XY || edit.plane == TransformPlane.XZ))
	RenderingServer.instance_set_visible(_axis_gizmo_instance[1], show and (edit.plane == TransformPlane.Y || edit.plane == TransformPlane.XY || edit.plane == TransformPlane.YZ))
	RenderingServer.instance_set_visible(_axis_gizmo_instance[2], show and (edit.plane == TransformPlane.Z || edit.plane == TransformPlane.XZ || edit.plane == TransformPlane.YZ))
	
	# Rotation white outline
	xform = xform.orthonormalized()
	xform.basis *= xform.basis.scaled(scale)
	RenderingServer.instance_set_transform(_rotate_gizmo_instance[3], xform)
	RenderingServer.instance_set_visible(_rotate_gizmo_instance[3], mode == ToolMode.ALL || mode == ToolMode.ROTATE)
	
	# Selection box
	var t := target.global_transform
	var t_offset := target.global_transform
	var bounds := _calculate_spatial_bounds(target)
	
	var offset := Vector3(0.005, 0.005, 0.005)
	var aabb_s := Basis.from_scale(bounds.size + offset)
	t = t.translated_local(bounds.position - offset / 2)
	t.basis *= aabb_s
	
	offset = Vector3(0.01, 0.01, 0.01);
	aabb_s = Basis.from_scale(bounds.size + offset);
	t_offset = t_offset.translated_local(bounds.position - offset / 2);
	t_offset.basis *= aabb_s;
	
	RenderingServer.instance_set_transform(_sbox_instance, t)
	RenderingServer.instance_set_visible(_sbox_instance, show_selection_box)
	RenderingServer.instance_set_transform(_sbox_instance_offset, t_offset)
	RenderingServer.instance_set_visible(_sbox_instance_offset, show_selection_box)
	RenderingServer.instance_set_transform(_sbox_xray_instance, t)
	RenderingServer.instance_set_visible(_sbox_xray_instance, show_selection_box)
	RenderingServer.instance_set_transform(_sbox_xray_instance_offset, t_offset)
	RenderingServer.instance_set_visible(_sbox_xray_instance_offset, show_selection_box)

func _set_visibility(visible : bool) -> void:
	for i in range(3):
		RenderingServer.instance_set_visible(_move_gizmo_instance[i], visible)
		RenderingServer.instance_set_visible(_move_plane_gizmo_instance[i], visible)
		RenderingServer.instance_set_visible(_rotate_gizmo_instance[i], visible)
		RenderingServer.instance_set_visible(_scale_gizmo_instance[i], visible)
		RenderingServer.instance_set_visible(_scale_plane_gizmo_instance[i], visible)
		RenderingServer.instance_set_visible(_axis_gizmo_instance[i], visible)
	RenderingServer.instance_set_visible(_rotate_gizmo_instance[3], visible)
	
	RenderingServer.instance_set_visible(_sbox_instance, visible)
	RenderingServer.instance_set_visible(_sbox_instance_offset, visible)
	RenderingServer.instance_set_visible(_sbox_xray_instance, visible)
	RenderingServer.instance_set_visible(_sbox_xray_instance_offset, visible)

func _generate_selection_boxes():
	# Meshes
	# Use two AABBs to create the illusion of a slightly thicker line.
	var aabb := AABB(Vector3(), Vector3.ONE)
	
	# Create a x-ray (visible through solid surfaces) and standard version of the selection box.
	# Both will be drawn at the same position, but with different opacity.
	# This lets the user see where the selection is while still having a sense of depth.
	var st := SurfaceTool.new()
	var st_xray := SurfaceTool.new()
	
	st.begin(Mesh.PRIMITIVE_LINES)
	st_xray.begin(Mesh.PRIMITIVE_LINES)
	for i in range(12):
		var edge = GizmoHelper.get_edge(aabb, i)
		st.add_vertex(edge[0])
		st.add_vertex(edge[1])
		st_xray.add_vertx(edge[0])
		st_xray.add_vertex(edge[1])
	
	_selection_box_mat = StandardMaterial3D.new()
	_selection_box_mat.shading_mode =BaseMaterial3D.SHADING_MODE_UNSHADED
	_selection_box_mat.disable_fog = true
	_selection_box_mat.albedo_color = selection_box_color
	_selection_box_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	st.set_material(_selection_box_mat)
	_selection_box = st.commit()
	
	_selection_box_mat = StandardMaterial3D.new()
	_selection_box_mat.shading_mode =BaseMaterial3D.SHADING_MODE_UNSHADED
	_selection_box_mat.disable_fog = true
	_selection_box_mat.no_depth_test = true
	_selection_box_mat.albedo_color = selection_box_color * Color(1, 1, 1, .15)
	_selection_box_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	st_xray.set_material(_selection_box_mat)
	_selection_box_xray = st.commit()
	
	# Instances
	_sbox_instance = RenderingServer.instance_create2(_selection_box.get_rid(), get_world_3d().scenario)
	_sbox_instance_offset = RenderingServer.instance_create2(_selection_box.get_rid(), get_world_3d().scenario)
	RenderingServer.instance_geometry_set_cast_shadows_setting(_sbox_instance, RenderingServer.SHADOW_CASTING_SETTING_OFF)
	RenderingServer.instance_geometry_set_cast_shadows_setting(_sbox_instance_offset, RenderingServer.SHADOW_CASTING_SETTING_OFF)
	RenderingServer.instance_set_layer_mask(_sbox_instance, layers)
	RenderingServer.instance_set_layer_mask(_sbox_instance_offset, layers)
	RenderingServer.instance_geometry_set_flag(_sbox_instance, RenderingServer.INSTANCE_FLAG_IGNORE_OCCLUSION_CULLING, true)
	RenderingServer.instance_geometry_set_flag(_sbox_instance, RenderingServer.INSTANCE_FLAG_USE_BAKED_LIGHT, false)
	RenderingServer.instance_geometry_set_flag(_sbox_instance_offset, RenderingServer.INSTANCE_FLAG_IGNORE_OCCLUSION_CULLING, true)
	RenderingServer.instance_geometry_set_flag(_sbox_instance_offset, RenderingServer.INSTANCE_FLAG_USE_BAKED_LIGHT, false)
	_sbox_xray_instance = RenderingServer.instance_create2(_selection_box.get_rid(), get_world_3d().scenario)
	_sbox_xray_instance_offset = RenderingServer.instance_create2(_selection_box.get_rid(), get_world_3d().scenario)
	RenderingServer.instance_geometry_set_cast_shadows_setting(_sbox_xray_instance, RenderingServer.SHADOW_CASTING_SETTING_OFF)
	RenderingServer.instance_geometry_set_cast_shadows_setting(_sbox_xray_instance_offset, RenderingServer.SHADOW_CASTING_SETTING_OFF)
	RenderingServer.instance_set_layer_mask(_sbox_xray_instance, layers)
	RenderingServer.instance_set_layer_mask(_sbox_xray_instance_offset, layers)
	RenderingServer.instance_geometry_set_flag(_sbox_xray_instance, RenderingServer.INSTANCE_FLAG_IGNORE_OCCLUSION_CULLING, true)
	RenderingServer.instance_geometry_set_flag(_sbox_xray_instance, RenderingServer.INSTANCE_FLAG_USE_BAKED_LIGHT, false)
	RenderingServer.instance_geometry_set_flag(_sbox_xray_instance_offset, RenderingServer.INSTANCE_FLAG_IGNORE_OCCLUSION_CULLING, true)
	RenderingServer.instance_geometry_set_flag(_sbox_xray_instance_offset, RenderingServer.INSTANCE_FLAG_USE_BAKED_LIGHT, false)

func _select_gizmo_highlight_axis(axis: int) -> void:
	for i in range(3):
		if i == axis:
			_move_gizmo[i].surface_set_material(0, _gizmo_color_hl[i])
		else:
			_move_gizmo[i].surface_set_material(0, _gizmo_color[i])
		if i == axis:
			_move_plane_gizmo[i].surface_set_material(0, _plane_gizmo_color_hl[i])
		else:
			_move_plane_gizmo[i].surface_set_material(0, _plane_gizmo_color[i])
		if i == axis:
			_rotate_gizmo[i].surface_set_material(0, _rotate_gizmo_color_hl[i])
		else:
			_rotate_gizmo[i].surface_set_material(0, _rotate_gizmo_color[i])
		if i == axis:
			_scale_gizmo[i].surface_set_material(0, _gizmo_color_hl[i])
		else:
			_scale_gizmo[i].surface_set_material(0, _gizmo_color[i])
		if i == axis:
			_scale_plane_gizmo[i].surface_set_material(0, _plane_gizmo_color_hl[i])
		else:
			_scale_plane_gizmo[i].surface_set_material(0, _plane_gizmo_color[i])

func _transform_gizmo_select(screen_pos: Vector2, highlight_only: bool = false):
	if !visible:
		return false
	
	if !target:
		if highlight_only:
			_select_gizmo_highlight_axis(-1)
		return false
	
	var ray_pos := _get_ray_pos(screen_pos)
	var ray := _get_ray(screen_pos)
	var gt := transform
	
	if mode == ToolMode.ALL || mode == ToolMode.MOVE:
		var col_axis := -1
		var colD = 1e20
		
		for i in range(3):
			var grabber_pos = gt.origin + gt.basis[i].normalized() * _gizmo_scale * (GIZMO_ARROW_OFFSET + (GIZMO_ARROW_SIZE * 0.5))
			var grabber_radius := _gizmo_scale * GIZMO_ARROW_SIZE
			
			var r := Geometry3D.segment_intersects_sphere(ray_pos, ray_pos + ray * MAX_Z, grabber_pos, grabber_radius)
			if r.size() != 0:
				var d := r[0].distance_to(ray_pos)
				if d < colD:
					colD = d
					col_axis = i
		
		var is_plane_translate := false
		# plane select
		if col_axis == -1:
			colD = 1e20
			
			for i in range(3):
				var ivec2 := gt.basis[(i + 1) % 3].normalized()
				var ivec3 := gt.basis[(i + 1) % 3].normalized()
				
				# Allow some tolerance to make the plane easier to click,
				# even if the click is actually slightly outside the plane.
				var grabber_pos = gt.origin + (ivec2 + ivec3) * _gizmo_scale * (GIZMO_PLANE_SIZE + GIZMO_PLANE_DST * 0.6667)
				
				var plane := Plane(gt.basis[i].normalized(), gt.origin)
				var r := plane.intersects_ray(ray_pos, ray)
				
				if r:
					var dist := (r as Vector3).distance_to(grabber_pos)
					# Allow some tolerance to make the plane easier to click,
					# even if the click is actually slightly outside the plane.
					if dist < _gizmo_scale * GIZMO_PLANE_SIZE * 1.5:
						var d := ray_pos.distance_to(r)
						if d < colD:
							colD = d
							col_axis = i
							
							is_plane_translate = true
		
		if col_axis != -1:
			if highlight_only:
				var axis := 0
				if is_plane_translate:
					axis = 6
				_select_gizmo_highlight_axis(col_axis + axis)
			else:
				# handle plane translate
				_edit.mode = TransformMode.TRANSLATE
				_compute_edit(screen_pos)
				var axis := 0
				if is_plane_translate:
					axis = 6
				_edit.plane = TransformPlane.X + col_axis + axis
				return true
		
		if mode == ToolMode.ALL || mode == ToolMode.ROTATE:
			# line 949
			pass
			

func _transform_gizmo_apply(node: Node3D, transform: Transform3D, local: bool) -> void:
	pass

func _compute_transform(mode: TransformMode, original: Transform3D, original_local: Transform3D, motion: Vector3, extra: float, local: bool, orthogonal: bool) -> Transform3D:
	return Transform3D()

func _update_transform(shift: bool) -> void:
	pass

func _apply_transform(motion: Vector3, snap: bool) -> void:
	var is_local_coords := local_coords and _edit.plane != TransformPlane.VIEW
	var new_transform := _compute_transform(_edit.mode, _edit.target_global, _edit.target_original, motion, snap, local_coords, _edit.plane != TransformPlane.VIEW)
	_transform_gizmo_apply(target, new_transform, is_local_coords)

func _compute_edit(point: Vector2) -> void:
	_edit.target_global = target.global_transform
	_edit.target_original = target.transform
	_edit.click_ray = _get_ray(point)
	_edit.click_ray_pos = _get_ray_pos(point)
	_edit.plane = TransformPlane.VIEW
	_edit.center = transform.origin

func _calculate_spatial_bounds(parent: Node3D, omit_top_level: bool = false, bounds_orientation: Transform3D = Transform3D.IDENTITY) -> AABB:
	return AABB()

func _get_ray_pos(pos: Vector2) -> Vector3:
	return get_viewport().get_camera_3d().project_ray_origin(pos)

func _get_ray(pos: Vector2) -> Vector3:
	return get_viewport().get_camera_3d().project_ray_normal(pos)

func _get_camera_normal() -> Vector3:
	return get_viewport().get_camera_3d().global_transform.basis[2]
