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

## Example of overriding scaling to maintain ratio on all axes.
func _edit_scale(scale : Vector3) -> Vector3:
	# Find the largest value on any axis being scaled and use that for all axes.
	var max := 0.0
	for i in range(3):
		if scale[i] != 0 and abs(scale[i]) > max:
			max = scale[i]
	return Vector3(max, max, max)

## Example of overriding rotating to not allow the user to rotate more than
## Pi / 2 (90) degrees on any axis at one time.
func _edit_rotate(rotation : Vector3) -> Vector3:
	return rotation.clampf(-PI / 2, PI / 2)
