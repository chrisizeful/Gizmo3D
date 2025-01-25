using Godot;

namespace Gizmo3DPlugin;

/// <summary>
/// A collection of helper methods translated from the C++ Godot source.
/// They're required by <see cref="Gizmo3D"/> but lack binds to GDScript and C#.
/// </summary>
public static class GizmoHelper
{

    /// <summary>
    /// Port of https://github.com/godotengine/godot/blob/master/scene/resources/material.cpp#L2856
    /// </summary>
    /// <param name="material">The material to alter.</param>
    /// <param name="alpha">If the material supports transparency.</param>
    public static void SetOnTopOfAlpha(this BaseMaterial3D material, bool alpha = false)
    {
        material.Transparency = alpha ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled;
        material.RenderPriority = (int) Material.RenderPriorityMax;
        material.NoDepthTest = true;
    }

    /// <summary>
    /// Port of https://github.com/godotengine/godot/blob/master/core/math/aabb.cpp#L361
    /// </summary>
    /// <param name="aabb">The AABB to operate on.</param>
    /// <param name="edge"></param>
    /// <param name="from"></param>
    /// <param name="to"></param>
    public static void GetEdge(this Aabb aabb, int edge, out Vector3 from, out Vector3 to)
    {
        from = to = default;
        Vector3 position = aabb.Position;
        Vector3 size = aabb.Size;
        switch (edge)
        {
            case 0: {
                from = new(position.X + size.X, position.Y, position.Z);
                to = new(position.X, position.Y, position.Z);
                break;
            }
            case 1: {
                from = new(position.X + size.X, position.Y, position.Z + size.Z);
                to = new(position.X + size.X, position.Y, position.Z);
                break;
            }
            case 2: {
                from = new(position.X, position.Y, position.Z + size.Z);
                to = new(position.X + size.X, position.Y, position.Z + size.Z);
                break;
            }
            case 3: {
                from = new(position.X, position.Y, position.Z);
                to = new(position.X, position.Y, position.Z + size.Z);
                break;
            }
            case 4: {
                from = new(position.X, position.Y + size.Y, position.Z);
                to = new(position.X + size.X, position.Y + size.Y, position.Z);
                break;
            }
            case 5: {
                from = new(position.X + size.X, position.Y + size.Y, position.Z);
                to = new(position.X + size.X, position.Y + size.Y, position.Z + size.Z);
                break;
            }
            case 6: {
                from = new(position.X + size.X, position.Y + size.Y, position.Z + size.Z);
                to = new(position.X, position.Y + size.Y, position.Z + size.Z);
                break;
            }
            case 7: {
                from = new(position.X, position.Y + size.Y, position.Z + size.Z);
                to = new(position.X, position.Y + size.Y, position.Z);
                break;
            }
            case 8: {
                from = new(position.X, position.Y, position.Z + size.Z);
                to = new(position.X, position.Y + size.Y, position.Z + size.Z);
                break;
            }
            case 9: {
                from = new(position.X, position.Y, position.Z);
                to = new(position.X, position.Y + size.Y, position.Z);
                break;
            }
            case 10: {
                from = new(position.X + size.X, position.Y, position.Z);
                to = new(position.X + size.X, position.Y + size.Y, position.Z);
                break;
            }
            case 11: {
                from = new(position.X + size.X, position.Y, position.Z + size.Z);
                to = new(position.X + size.X, position.Y + size.Y, position.Z + size.Z);
                break;
            }
        }
    }

    /// <summary>
    /// Port of https://github.com/godotengine/godot/blob/master/core/math/basis.cpp#L262
    /// </summary>
    /// <param name="basis">The basis to operate on.</param>
    /// <param name="scale">The orthogonal scale.</param>
    /// <returns></returns>
    public static Basis ScaledOrthogonal(this Basis basis, Vector3 scale)
    {
        Vector3 s = new Vector3(-1, -1, -1) + scale;
        bool sign = (s.X + s.Y + s.Z) < 0;
        Basis b = basis.Orthonormalized();
        s *= b;
        Vector3 dots = Vector3.Zero;
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                dots[j] += s[i] * Mathf.Abs(basis[i].Normalized().Dot(b[j]));
        if (sign != ((dots.X + dots.Y + dots.Z) < 0))
            dots = -dots;
        basis *= Basis.FromScale(Vector3.One + dots);
        return basis;
    }
}