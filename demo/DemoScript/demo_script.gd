extends Node3D

@export
var gizmo : Gizmo3D
@export
var camera : CameraScript
@export
var custom_label : Label
var _add : bool

func _process(delta):
	_add = Input.is_action_pressed("add_target")

func _input(event: InputEvent) -> void:
	# Swap gizmo with custom gizmo or vice versa
	if event.is_action_pressed("custom_gizmo"):
		var parent := gizmo.get_parent()
		var index := gizmo.get_index()
		gizmo.queue_free()
		if gizmo is CustomGizmo:
			gizmo = Gizmo3D.new()
			custom_label.text = "Custom Gizmo: G"
		else:
			gizmo = CustomGizmo.new()
			custom_label.text = "Default Gizmo: G"
		camera.gizmo = gizmo
		parent.add_child(gizmo)
		parent.move_child(gizmo, index)
	
	# Toggle modes
	if event.is_action_pressed("move_mode"):
		gizmo.mode ^= Gizmo3D.ToolMode.MOVE
	if event.is_action_pressed("scale_mode"):
		gizmo.mode ^= Gizmo3D.ToolMode.SCALE
	if event.is_action_pressed("rotate_mode"):
		gizmo.mode ^= Gizmo3D.ToolMode.ROTATE
	
	# Toggle between local and global space
	if !gizmo.editing and event.is_action_pressed("use_local_space"):
		gizmo.use_local_space = !gizmo.use_local_space
	# Prevent object picking if user is interacting with the gizmo
	if gizmo.hovering || gizmo.editing:
		return;
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and event.pressed:
		# Raycast from the camera
		var camera := get_viewport().get_camera_3d()
		var dir := camera.project_ray_normal(event.position)
		var from := camera.project_ray_origin(event.position)
		var params = PhysicsRayQueryParameters3D.new()
		params.from = from
		params.to = from + dir * 1000.0
		var result = get_world_3d().direct_space_state.intersect_ray(params)
		if result.size() == 0:
			return
		# If shift is held, add/remove the node to/from the target list. Otherwise set the target to just that node.
		var collider = result["collider"] as Node3D
		var node = collider.get_parent()
		if !_add:
			gizmo.clear_selection()
			gizmo.select(node)
			return
		if !gizmo.deselect(node):
			gizmo.select(node)
