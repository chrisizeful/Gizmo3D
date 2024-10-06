using Godot;

namespace Gizmo3DPlugin;

public partial class CameraSharp : Camera3D
{

	const float MOVE_SPEED = 20.0f;
	const float MOUSE_SENS = .25f;

	readonly StringName left = "move_left", right = "move_right";
	readonly StringName forward = "move_forward", backward = "move_backward";

	[Export]
	public Gizmo3D Gizmo { get; private set; }
	[Export]
	public Label Message { get; private set; }

	public override void _Process(double delta)
	{
		Vector2 input = Input.GetVector(left, right, forward, backward);
		Vector3 move = (Basis * new Vector3(input.X, 0, input.Y)).Normalized();
		Position += move * MOVE_SPEED * (float) delta;

		Message.Visible = Gizmo.Editing;
		if (!Gizmo.Editing)
			return;
		Message.Position = GetViewport().GetMousePosition() + new Vector2(16, 16);
		Message.Text = Gizmo.Message;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Right)
		{
			Input.MouseMode = button.Pressed ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
			Gizmo.SetProcessUnhandledInput(!button.Pressed);
		}
		else if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
            float pitch = Mathf.Clamp(motion.Relative.Y * MOUSE_SENS, -90, 90);
            RotateY(Mathf.DegToRad(-motion.Relative.X * MOUSE_SENS));
            RotateObjectLocal(new Vector3(1.0f, 0.0f, 0.0f), Mathf.DegToRad(-pitch));
		}
    }
}
