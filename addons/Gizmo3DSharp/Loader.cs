#if TOOLS
using Godot;

namespace Gizmo3DPlugin;

[Tool]
public partial class Loader : EditorPlugin
{

	public override void _EnablePlugin()
	{
		AddCustomType("Gizmo3D", "Node3D", ResourceLoader.Load<CSharpScript>("res://addons/Gizmo3DSharp/Gizmo3D.cs"), null);
	}

	public override void _DisablePlugin()
	{
		RemoveCustomType("Gizmo3D");
	}
}
#endif
