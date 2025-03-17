## Example of extending Gizmo to override the default behavior.
## Additionally, connects to signals.
class_name CustomGizmo
extends Gizmo3D

func _ready() -> void:
	super._ready()
	transform_begin.connect(func(mode): print("Begin ", TransformMode.keys()[mode]))
	transform_changed.connect(func(mode, value): print("Change ", TransformMode.keys()[mode], ": ", value))
	transform_end.connect(func(mode): print("End ", TransformMode.keys()[mode]))

## Example of overriding translating to always snap to 2 units.
func _edit_translate(translation : Vector3) -> Vector3:
	return translation.snappedf(2)

## Example of overriding scaling to not allow the user to scale more than
## 2 units in any direction at one time.
func _edit_scale(scale : Vector3) -> Vector3:
	return scale.clampf(-2, 2)

## Example of overriding rotating to not allow the user to rotate more than
## Pi / 2 (90) degrees on any axis at one time.
func _edit_rotate(rotation : Vector3) -> Vector3:
	return rotation.clampf(-PI / 2, PI / 2)
