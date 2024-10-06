class_name GizmoHelper;
extends Node;

static func set_on_top_of_alpha(material : BaseMaterial3D):
	material.transparency = BaseMaterial3D.Transparency.TRANSPARENCY_DISABLED
	material.render_priority = Material.RENDER_PRIORITY_MAX
	material.no_depth_test = true

static func get_edge(aabb : AABB, p_edge : int) -> Array:
	var result := [2]
	var position = aabb.position
	var size = aabb.size
	match (p_edge):
		0:
			result[0] = (Vector3(position.X + size.X, position.Y, position.Z))
			result[1] = (Vector3(position.X, position.Y, position.Z))
		1:
			result[0] = (Vector3(position.X + size.X, position.Y, position.Z + size.Z))
			result[1] = (Vector3(position.X + size.X, position.Y, position.Z))
		2:
			result[0] = (Vector3(position.X, position.Y, position.Z + size.Z))
			result[1] = (Vector3(position.X + size.X, position.Y, position.Z + size.Z))
		3:
			result[0] = (Vector3(position.X, position.Y, position.Z))
			result[1] = (Vector3(position.X, position.Y, position.Z + size.Z))
		4:
			result[0] = (Vector3(position.X, position.Y + size.Y, position.Z))
			result[1] = (Vector3(position.X + size.X, position.Y + size.Y, position.Z))
		5:
			result[0] = (Vector3(position.X + size.X, position.Y + size.Y, position.Z))
			result[1] = (Vector3(position.X + size.X, position.Y + size.Y, position.Z + size.Z))
		6:
			result[0] = (Vector3(position.X + size.X, position.Y + size.Y, position.Z + size.Z))
			result[1] = (Vector3(position.X, position.Y + size.Y, position.Z + size.Z))
		7:
			result[0] = (Vector3(position.X, position.Y + size.Y, position.Z + size.Z))
			result[1] = (Vector3(position.X, position.Y + size.Y, position.Z))
		8:
			result[0] = (Vector3(position.X, position.Y, position.Z + size.Z))
			result[1] = (Vector3(position.X, position.Y + size.Y, position.Z + size.Z))
		9:
			result[0] = (Vector3(position.X, position.Y, position.Z))
			result[1] = (Vector3(position.X, position.Y + size.Y, position.Z))
		10:
			result[0] = (Vector3(position.X + size.X, position.Y, position.Z))
			result[1] = (Vector3(position.X + size.X, position.Y + size.Y, position.Z))
		11:
			result[0] = (Vector3(position.X + size.X, position.Y, position.Z + size.Z))
			result[1] = (Vector3(position.X + size.X, position.Y + size.Y, position.Z + size.Z))
	return result;

static func scaled_orthogonal(basis : Basis, scale : Vector3) -> Basis:
	var s = Vector3(-1, -1, -1) + scale
	var sign = (s.X + s.Y + s.Z) < 0
	var b = basis.orthonormalized()
	s *= b
	var dots = Vector3.ZERO
	for i in range(3):
		for j in range(3):
			dots[j] += s[i] * abs(basis[i].normalized().dot(b[j]))
	if sign != ((dots.x + dots.y + dots.z) < 0):
		dots = -dots
	basis *= Basis.from_scale(Vector3.ONE + dots)
	return basis
