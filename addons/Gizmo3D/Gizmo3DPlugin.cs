#if TOOLS
using Godot;

[Tool]
public partial class Gizmo3DPlugin : EditorPlugin
{

	public override void _EnterTree()
	{
		AddCustomType("Gizmo3D", "Node3D", ResourceLoader.Load<CSharpScript>("res://addons/Gizmo3D/Gizmo3D.cs"), null);
	}

	public override void _ExitTree()
	{
		RemoveCustomType("Gizmo3D");
	}
}
#endif
