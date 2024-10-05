@tool
extends EditorPlugin

func _enter_tree() -> void:
	add_custom_type("Gizmo3D", "Node3D", ResourceLoader.load("res://addons/Gizmo3DScript/loader.gd"), null)

func _exit_tree() -> void:
	remove_custom_type("Gizmo3D")
