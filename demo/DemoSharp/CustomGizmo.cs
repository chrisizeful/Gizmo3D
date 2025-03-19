using Godot;

namespace Gizmo3DPlugin;

/// <summary>
/// Example of extending Gizmo to override the default behavior.
/// Additionally, connects to signals.
/// </summary>
public partial class CustomGizmo : Gizmo3D
{

    public override void _Ready()
    {
        base._Ready();
        TransformBegin += (mode) => GD.Print($"Begin {(TransformMode) mode}");
        TransformChanged += (mode, value) => GD.Print($"Change {(TransformMode) mode}: {value}");
        TransformEnd += (mode) => GD.Print($"End {(TransformMode) mode}");
    }

    /// <summary>
    /// Example of overriding translating to always snap to 2 units.
    /// </summary>
    protected override Vector3 EditTranslate(Vector3 translation)
    {
        return translation.Snapped(2);
    }
    
    /// <summary>
    /// Example of overriding scaling to maintain ratio on all axes.
    /// </summary>
    protected override Vector3 EditScale(Vector3 scale)
    {
        // Find the largest value on any axis being scaled and use that for all axes.
        float max = 0;
        for (int i = 0; i < 3; i++)
            if (scale[i] != 0 && Mathf.Abs(scale[i]) > max)
                max = scale[i];
        return new(max, max, max);
    }
    
    /// <summary>
    /// Example of overriding rotating to not allow the user to rotate more than
    /// Pi / 2 (90) degrees on any axis at one time.
    /// </summary>
    protected override Vector3 EditRotate(Vector3 rotation)
    {
        return rotation.Clamp(-Mathf.Pi / 2, Mathf.Pi / 2);
    }
}