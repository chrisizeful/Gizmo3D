class_name CameraScript
extends Camera3D

const MOVE_SPEED := 20.0
const MOUSE_SENS := .25

@export
var gizmo : Gizmo3D
@export
var message : Label

func _process(delta: float) -> void:
	var input := Input.get_vector("move_left", "move_right", "move_forward", "move_backward")
	var move := (basis * Vector3(input.x, 0, input.y)).normalized()
	position += move * MOVE_SPEED * delta

	message.visible = gizmo.editing
	if !gizmo.editing:
		return
	message.position = get_viewport().get_mouse_position() + Vector2(16, 16)
	message.text = gizmo.message

func _input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_RIGHT:
		if event.pressed:
			Input.mouse_mode = Input.MOUSE_MODE_CAPTURED
		else:
			Input.mouse_mode = Input.MOUSE_MODE_VISIBLE
		gizmo.set_process_unhandled_input(!event.pressed)
	elif event is InputEventMouseMotion and Input.mouse_mode == Input.MOUSE_MODE_CAPTURED:
		var pitch = clamp(event.relative.y * MOUSE_SENS, -90, 90)
		rotate_y(deg_to_rad(-event.relative.x * MOUSE_SENS))
		rotate_object_local(Vector3(1.0, 0.0, 0.0), deg_to_rad(-pitch))
