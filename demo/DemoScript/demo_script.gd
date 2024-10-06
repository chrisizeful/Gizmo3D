extends Node3D

@export
var gizmo : Gizmo3D

func _input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		var camera := get_viewport().get_camera_3d()
		var dir := camera.project_ray_normal(event.position)
		var from := camera.project_ray_origin(event.position)
		var params = PhysicsRayQueryParameters3D.new()
		params.from = from
		params.to = from + dir * 1000.0
		var result = get_world_3d().direct_space_state.intersect_ray(params)
		if result.size() == 0:
			return
		var collider = result["collider"] as Node3D;
		gizmo.target = collider.get_parent();
