using Godot;

namespace Gizmo3DPlugin;

public partial class DemoSharp : Node3D
{

	[Export]
	public Gizmo3D Gizmo { get; private set; }

    public override void _UnhandledInput(InputEvent @event)
    {
		// Prevent object picking is user is interacting with the gizmo
		if (Gizmo.Hovering || Gizmo.Editing)
			return;
        if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left && button.Pressed)
		{
			Camera3D camera = GetViewport().GetCamera3D();
			Vector3 dir = camera.ProjectRayNormal(button.Position);
			Vector3 from = camera.ProjectRayOrigin(button.Position);
			var result = GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D()
			{
				From = from,
				To = from + dir * 1000.0f
			});
			if (result.Count == 0)
				return;
			Node collider = (Node) result["collider"];
			Gizmo.Target = collider.GetParent<Node3D>();
		}
    }
}
