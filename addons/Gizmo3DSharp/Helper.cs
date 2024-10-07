using Godot;

namespace Gizmo3DPlugin;

public static class Helper
{

    public static void SetOnTopOfAlpha(this BaseMaterial3D material, bool alpha = false)
    {
        material.Transparency = alpha ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled;
        material.RenderPriority = (int) Material.RenderPriorityMax;
        material.NoDepthTest = true;
    }

    public static void GetEdge(this Aabb aabb, int p_edge, out Vector3 r_from, out Vector3 r_to)
    {
        r_from = r_to = default;
        Vector3 position = aabb.Position;
        Vector3 size = aabb.Size;
        switch (p_edge)
        {
            case 0: {
                r_from = new(position.X + size.X, position.Y, position.Z);
                r_to = new(position.X, position.Y, position.Z);
                break;
            }
            case 1: {
                r_from = new(position.X + size.X, position.Y, position.Z + size.Z);
                r_to = new(position.X + size.X, position.Y, position.Z);
                break;
            }
            case 2: {
                r_from = new(position.X, position.Y, position.Z + size.Z);
                r_to = new(position.X + size.X, position.Y, position.Z + size.Z);
                break;
            }
            case 3: {
                r_from = new(position.X, position.Y, position.Z);
                r_to = new(position.X, position.Y, position.Z + size.Z);
                break;
            }
            case 4: {
                r_from = new(position.X, position.Y + size.Y, position.Z);
                r_to = new(position.X + size.X, position.Y + size.Y, position.Z);
                break;
            }
            case 5: {
                r_from = new(position.X + size.X, position.Y + size.Y, position.Z);
                r_to = new(position.X + size.X, position.Y + size.Y, position.Z + size.Z);
                break;
            }
            case 6: {
                r_from = new(position.X + size.X, position.Y + size.Y, position.Z + size.Z);
                r_to = new(position.X, position.Y + size.Y, position.Z + size.Z);
                break;
            }
            case 7: {
                r_from = new(position.X, position.Y + size.Y, position.Z + size.Z);
                r_to = new(position.X, position.Y + size.Y, position.Z);
                break;
            }
            case 8: {
                r_from = new(position.X, position.Y, position.Z + size.Z);
                r_to = new(position.X, position.Y + size.Y, position.Z + size.Z);
                break;
            }
            case 9: {
                r_from = new(position.X, position.Y, position.Z);
                r_to = new(position.X, position.Y + size.Y, position.Z);
                break;
            }
            case 10: {
                r_from = new(position.X + size.X, position.Y, position.Z);
                r_to = new(position.X + size.X, position.Y + size.Y, position.Z);
                break;
            }
            case 11: {
                r_from = new(position.X + size.X, position.Y, position.Z + size.Z);
                r_to = new(position.X + size.X, position.Y + size.Y, position.Z + size.Z);
                break;
            }
        }
    }

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