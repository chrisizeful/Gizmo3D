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
var show_axes := true

@export
var opacity := .9
@export
var colors := [
	Color(0.96, 0.20, 0.32),
	Color(0.53, 0.84, 0.01),
	Color(0.16, 0.55, 0.96)
]
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

enum ToolMode { ALL, MOVE, ROTATE, SCALE }
enum TransformMode { NONE, ROTATE, TRANSLATE, SCALE }
enum TransformPlane { VIEW, X, Y, Z, YZ, XZ, XY }

func _ready() -> void:
	_init_indicators()
	_set_colors()
	_init_gizmo_instance()
	_update_transform_gizmo_view()

func _unhandled_input(event: InputEvent) -> void:
	pass

func _process(delta: float) -> void:
	pass

func _exit_tree() -> void:
	pass

func _init_gizmo_instance() -> void:
	pass

func _init_indicators() -> void:
	pass

func _set_colors() -> void:
	pass

func _update_transform_gizmo_view() -> void:
	pass

func _set_visibility(visible : bool) -> void:
	pass

func _generate_selection_boxes():
	pass

func _select_gizmo_highlight_axis(axis: int) -> void:
	pass

func _transform_gizmo_select(screen_pos: Vector2, highlight_only: bool = false):
	pass

func _transform_gizmo_apply(node: Node3D, transform: Transform3D, local: bool) -> void:
	pass

func _compute_transform(mode: TransformMode, original: Transform3D, original_local: Transform3D, motion: Vector3, extra: float, local: bool, orthogonal: bool) -> Transform3D:
	return Transform3D()

func _update_transform(shift: bool) -> void:
	pass

func _apply_transform(motion: Vector3, snap: bool) -> void:
	pass

func _compute_edit(point: Vector2) -> void:
	pass

func _get_ray_pos(pos: Vector2) -> Vector3:
	return Vector3()

func _get_ray(pos: Vector2) -> Vector3:
	return Vector3()

func _get_camera_normal() -> Vector3:
	return Vector3()
