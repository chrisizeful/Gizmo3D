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

func _ready() -> void:
	pass

func _process(delta: float) -> void:
	pass
