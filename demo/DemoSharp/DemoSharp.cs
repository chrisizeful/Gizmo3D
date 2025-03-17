using Godot;

namespace Gizmo3DPlugin;

public partial class DemoSharp : Node3D
{

	[Export]
	public Gizmo3D Gizmo { get; private set; }
	[Export]
	public CameraSharp Camera { get; private set; }
	[Export]
	public Label CustomLabel { get; private set; }
	bool add;

	readonly StringName useLocalSpace = "use_local_space";
	readonly StringName addTarget = "add_target";
	readonly StringName customGizmo = "custom_gizmo";

    public override void _Process(double delta)
    {
        add = Input.IsActionPressed(addTarget);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
		// Swap gizmo with custom gizmo or vice versa
		if (@event.IsActionPressed(customGizmo))
		{
			Node parent = Gizmo.GetParent();
			int index = Gizmo.GetIndex();
			Gizmo.QueueFree();
			Gizmo = Gizmo is CustomGizmo ? new Gizmo3D() : new CustomGizmo();
			Camera.Gizmo = Gizmo;
			parent.AddChild(Gizmo);
			parent.MoveChild(Gizmo, index);
			CustomLabel.Text = Gizmo is CustomGizmo ? "Default Gizmo: G" : "Custom Gizmo: G";
		}

		// Toggle between local and global space
		if (!Gizmo.Editing && @event.IsActionPressed(useLocalSpace))
			Gizmo.UseLocalSpace = !Gizmo.UseLocalSpace;
		// Prevent object picking if user is interacting with the gizmo
		if (Gizmo.Hovering || Gizmo.Editing)
			return;
        if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left && button.Pressed)
		{
			// Raycast from the camera
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
			// If shift is held, add/remove the node to/from the target list. Otherwise set the target to just that node.
			Node collider = (Node) result["collider"];
			Node3D node = collider.GetParent<Node3D>();
			if (!add)
			{
				Gizmo.ClearSelection();
				Gizmo.Select(node);
				return;
			}
			if (!Gizmo.Deselect(node))
				Gizmo.Select(node);
		}
    }
}
