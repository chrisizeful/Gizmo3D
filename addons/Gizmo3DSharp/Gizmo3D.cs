using Godot;
using static Godot.RenderingServer;

namespace Gizmo3DPlugin;

// Translated from C++ to C# with alterations, source from:
// - https://github.com/godotengine/godot/blob/master/editor/plugins/node_3d_editor_plugin.h
// - https://github.com/godotengine/godot/blob/master/editor/plugins/node_3d_editor_plugin.cpp
public partial class Gizmo3D : Node3D
{

    const float DEFAULT_FLOAT_STEP = 0.001f;
    const float MAX_Z = 1000000.0f;

    const float GIZMO_ARROW_SIZE = 0.35f;
    const float GIZMO_RING_HALF_WIDTH = 0.1f;
    const float GIZMO_PLANE_SIZE = .2f;
    const float GIZMO_PLANE_DST = 0.3f;
    const float GIZMO_CIRCLE_SIZE = 1.1f;
    const float GIZMO_SCALE_OFFSET = GIZMO_CIRCLE_SIZE - 0.3f;
    const float GIZMO_ARROW_OFFSET = GIZMO_CIRCLE_SIZE + 0.15f;

    [Export]
    public ToolMode Mode { get; set; } = ToolMode.All;

    uint layers = 1;
    [Export(PropertyHint.Layers3DRender)]
    public uint Layers
    {
        get => layers;
        set
        {
            layers = value;
            if (!IsNodeReady())
                return;
            for (int i = 0; i < 3; i++)
            {
                InstanceSetLayerMask(MoveGizmoInstance[i], Layers);
                InstanceSetLayerMask(MovePlaneGizmoInstance[i], Layers);
                InstanceSetLayerMask(RotateGizmoInstance[i], Layers);
                InstanceSetLayerMask(ScaleGizmoInstance[i], Layers);
                InstanceSetLayerMask(ScalePlaneGizmoInstance[i], Layers);
                InstanceSetLayerMask(AxisGizmoInstance[i], Layers);
                InstanceSetLayerMask(RotateGizmoInstance[3], Layers);
            }
        }
    }

    [ExportGroup("Style")]
    [Export]
    public float Size { get; set; } = 80.0f;
    [Export]
    public bool ShowAxes { get; set; } = true;
    [Export(PropertyHint.Range, "0,1")]
    public float Opacity { get; set; } = .9f;
    [Export(PropertyHint.ColorNoAlpha)]
    public Color AxisXColor { get; set; } = new(0.96f, 0.20f, 0.32f);
    [Export(PropertyHint.ColorNoAlpha)]
    public Color AxisYColor { get; set; } = new(0.53f, 0.84f, 0.01f);
    [Export(PropertyHint.ColorNoAlpha)]
    public Color AxisZColor { get; set; } = new(0.16f, 0.55f, 0.96f);
    [Export(PropertyHint.ColorNoAlpha)]
    public Color SelectionBoxColor { get; set; } = new(1.0f, 0.5f, 0);

    [ExportGroup("Position")]
    [Export(PropertyHint.None, "Test")]
    public bool LocalCoords { get; set; }
    [Export(PropertyHint.Range, "0,360")]
    public float RotateSnap { get; set; } = 15.0f;
    [Export(PropertyHint.Range, "0,10")]
    public float TranslateSnap { get; set; } = 1.0f;
    [Export(PropertyHint.Range, "0,5")]
    public float ScaleSnap { get; set; } = .25f;

    public Node3D Target { get; set; }

    public bool Snapping { get; private set; }
    public bool Editing { get; private set; }
    public string Message { get; private set; }

    ArrayMesh[] MoveGizmo = new ArrayMesh[3];
    ArrayMesh[] MovePlaneGizmo = new ArrayMesh[3];
    ArrayMesh[] RotateGizmo = new ArrayMesh[4];
    ArrayMesh[] ScaleGizmo = new ArrayMesh[3];
    ArrayMesh[] ScalePlaneGizmo = new ArrayMesh[3];
    ArrayMesh[] AxisGizmo = new ArrayMesh[3];
    StandardMaterial3D[] GizmoColor = new StandardMaterial3D[3];
	StandardMaterial3D[] PlaneGizmoColor = new StandardMaterial3D[3];
	ShaderMaterial[] RotateGizmoColor = new ShaderMaterial[3];
	StandardMaterial3D[] GizmoColorHl = new StandardMaterial3D[3];
	StandardMaterial3D[] PlaneGizmoColorHl = new StandardMaterial3D[3];
	ShaderMaterial[] RotateGizmoColorHl = new ShaderMaterial[3];

    Rid[] MoveGizmoInstance = new Rid[3];
    Rid[] MovePlaneGizmoInstance = new Rid[3];
    Rid[] RotateGizmoInstance = new Rid[4];
    Rid[] ScaleGizmoInstance = new Rid[3];
    Rid[] ScalePlaneGizmoInstance = new Rid[3];
    Rid[] AxisGizmoInstance = new Rid[3];

    ArrayMesh selectionBox, selectionBoxXray;

    EditData edit = new();
    float gizmoScale = 1.0f;

    public enum ToolMode { All, Move, Rotate, Scale };
    enum TransformMode { None, Rotate, Translate, Scale };
    enum TransformPlane { View, X, Y, Z, YZ, XZ, XY, };

    struct EditData {
        public Transform3D TargetOriginal, TargetGlobal;
        public TransformMode Mode;
        public TransformPlane Plane;
        public Vector3 ClickRay, ClickRayPos;
        public Vector3 Center;
        public Vector2 MousePos;
    };

    public override void _Ready()
    {
        InitIndicators();
        InitGizmoInstance();
        UpdateTransformGizmoView();
        VisibilityChanged += () => SetVisibility(Visible);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible)
        {
            Editing = false;
            return;
        }
        if (@event is InputEventKey key && key.Keycode == Key.Ctrl)
        {
            Snapping = key.Pressed;
        }
        else if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Right)
        {
            Editing = button.Pressed;
            if (!Editing)
                return;
            edit.MousePos = button.Position;
            Editing = TransformGizmoSelect(button.Position);
        }
        else if (@event is InputEventMouseMotion motion)
        {
            if (Editing && motion.ButtonMask.HasFlag(MouseButtonMask.Right))
            {
                edit.MousePos = motion.Position;
                UpdateTransform(false);
                return;
            }
            TransformGizmoSelect(motion.Position, true);
        }
    }

    public override void _Process(double delta)
    {
        if (Target == null || !IsInstanceValid(Target) || Target.IsQueuedForDeletion())
            return;
        Position = Target.Position;
        UpdateTransformGizmoView();
    }

    public override void _ExitTree()
    {
        for (int i = 0; i < 3; i++)
        {
            FreeRid(MoveGizmoInstance[i]);
            FreeRid(MovePlaneGizmoInstance[i]);
            FreeRid(RotateGizmoInstance[i]);
            FreeRid(ScaleGizmoInstance[i]);
            FreeRid(ScalePlaneGizmoInstance[i]);
            FreeRid(AxisGizmoInstance[i]);
        }
        // Rotation white outline
        FreeRid(RotateGizmoInstance[3]);
    }

    void InitGizmoInstance()
    {
        for (int i = 0; i < 3; i++) {
            MoveGizmoInstance[i] = InstanceCreate();
            InstanceSetBase(MoveGizmoInstance[i], MoveGizmo[i].GetRid());
            InstanceSetScenario(MoveGizmoInstance[i], GetTree().Root.World3D.Scenario);
            InstanceSetVisible(MoveGizmoInstance[i], true);
            InstanceGeometrySetCastShadowsSetting(MoveGizmoInstance[i], ShadowCastingSetting.Off);
            InstanceSetLayerMask(MoveGizmoInstance[i], Layers);
            InstanceGeometrySetFlag(MoveGizmoInstance[i], InstanceFlags.IgnoreOcclusionCulling, true);
            InstanceGeometrySetFlag(MoveGizmoInstance[i], InstanceFlags.UseBakedLight, false);

            MovePlaneGizmoInstance[i] = InstanceCreate();
            InstanceSetBase(MovePlaneGizmoInstance[i], MovePlaneGizmo[i].GetRid());
            InstanceSetScenario(MovePlaneGizmoInstance[i], GetTree().Root.World3D.Scenario);
            InstanceSetVisible(MovePlaneGizmoInstance[i], true);
            InstanceGeometrySetCastShadowsSetting(MovePlaneGizmoInstance[i], ShadowCastingSetting.Off);
            InstanceSetLayerMask(MovePlaneGizmoInstance[i], Layers);
            InstanceGeometrySetFlag(MovePlaneGizmoInstance[i], InstanceFlags.IgnoreOcclusionCulling, true);
            InstanceGeometrySetFlag(MovePlaneGizmoInstance[i], InstanceFlags.UseBakedLight, false);

            RotateGizmoInstance[i] = InstanceCreate();
            InstanceSetBase(RotateGizmoInstance[i], RotateGizmo[i].GetRid());
            InstanceSetScenario(RotateGizmoInstance[i], GetTree().Root.World3D.Scenario);
            InstanceSetVisible(RotateGizmoInstance[i], true);
            InstanceGeometrySetCastShadowsSetting(RotateGizmoInstance[i], ShadowCastingSetting.Off);
            InstanceSetLayerMask(RotateGizmoInstance[i], Layers);
            InstanceGeometrySetFlag(RotateGizmoInstance[i], InstanceFlags.IgnoreOcclusionCulling, true);
            InstanceGeometrySetFlag(RotateGizmoInstance[i], InstanceFlags.UseBakedLight, false);

            ScaleGizmoInstance[i] = InstanceCreate();
            InstanceSetBase(ScaleGizmoInstance[i], ScaleGizmo[i].GetRid());
            InstanceSetScenario(ScaleGizmoInstance[i], GetTree().Root.World3D.Scenario);
            InstanceSetVisible(ScaleGizmoInstance[i], true);
            InstanceGeometrySetCastShadowsSetting(ScaleGizmoInstance[i], ShadowCastingSetting.Off);
            InstanceSetLayerMask(ScaleGizmoInstance[i], Layers);
            InstanceGeometrySetFlag(ScaleGizmoInstance[i], InstanceFlags.IgnoreOcclusionCulling, true);
            InstanceGeometrySetFlag(ScaleGizmoInstance[i], InstanceFlags.UseBakedLight, false);

            ScalePlaneGizmoInstance[i] = InstanceCreate();
            InstanceSetBase(ScalePlaneGizmoInstance[i], ScalePlaneGizmo[i].GetRid());
            InstanceSetScenario(ScalePlaneGizmoInstance[i], GetTree().Root.World3D.Scenario);
            InstanceSetVisible(ScalePlaneGizmoInstance[i], true);
            InstanceGeometrySetCastShadowsSetting(ScalePlaneGizmoInstance[i], ShadowCastingSetting.Off);
            InstanceSetLayerMask(ScalePlaneGizmoInstance[i], Layers);
            InstanceGeometrySetFlag(ScalePlaneGizmoInstance[i], InstanceFlags.IgnoreOcclusionCulling, true);
            InstanceGeometrySetFlag(ScalePlaneGizmoInstance[i], InstanceFlags.UseBakedLight, false);

            AxisGizmoInstance[i] = InstanceCreate();
            InstanceSetBase(AxisGizmoInstance[i], AxisGizmo[i].GetRid());
            InstanceSetScenario(AxisGizmoInstance[i], GetTree().Root.World3D.Scenario);
            InstanceSetVisible(AxisGizmoInstance[i], true);
            InstanceGeometrySetCastShadowsSetting(AxisGizmoInstance[i], ShadowCastingSetting.Off);
            InstanceSetLayerMask(AxisGizmoInstance[i], Layers);
            InstanceGeometrySetFlag(AxisGizmoInstance[i], InstanceFlags.IgnoreOcclusionCulling, true);
            InstanceGeometrySetFlag(AxisGizmoInstance[i], InstanceFlags.UseBakedLight, false);
        }

        // Rotation white outline
        RotateGizmoInstance[3] = InstanceCreate();
        InstanceSetBase(RotateGizmoInstance[3], RotateGizmo[3].GetRid());
        InstanceSetScenario(RotateGizmoInstance[3], GetTree().Root.World3D.Scenario);
        InstanceSetVisible(RotateGizmoInstance[3], true);
        InstanceGeometrySetCastShadowsSetting(RotateGizmoInstance[3], ShadowCastingSetting.Off);
        InstanceSetLayerMask(RotateGizmoInstance[3], Layers);
        InstanceGeometrySetFlag(RotateGizmoInstance[3], InstanceFlags.IgnoreOcclusionCulling, true);
        InstanceGeometrySetFlag(RotateGizmoInstance[3], InstanceFlags.UseBakedLight, false);
    }

    void InitIndicators()
    {
#region Move
        // Inverted zxy.
        Vector3 ivec = new(0, 0, -1);
        Vector3 nivec = new(-1, -1, 0);
        Vector3 ivec2 = new(-1, 0, 0);
        Vector3 ivec3 = new(0, -1, 0);

        for (int i = 0; i < 3; i++)
        {
            Color col;
            switch (i)
            {
                case 0:
                    col = AxisXColor;
                    break;
                case 1:
                    col = AxisYColor;
                    break;
                case 2:
                    col = AxisZColor;
                    break;
                default:
                    col = new();
                    break;
            }
            col.A = Opacity;

            MoveGizmo[i] = new ArrayMesh();
            MovePlaneGizmo[i] = new ArrayMesh();
            RotateGizmo[i] = new ArrayMesh();
            ScaleGizmo[i] = new ArrayMesh();
            ScalePlaneGizmo[i] = new ArrayMesh();
            AxisGizmo[i] = new ArrayMesh();

            StandardMaterial3D mat = new()
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                DisableFog = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = col,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
            mat.SetOnTopOfAlpha();
            GizmoColor[i] = mat;

            StandardMaterial3D matHl = (StandardMaterial3D) mat.Duplicate();
            Color albedo = Color.FromHsv(col.H, 0.25f, 1.0f, 1.0f);
            matHl.AlbedoColor = albedo;
            GizmoColorHl[i] = matHl;
#region Translate
            SurfaceTool surfTool = new();
            surfTool.Begin(Mesh.PrimitiveType.Triangles);

            // Arrow profile
            int arrowPoints = 5;
            Vector3[] arrow = {
                nivec * 0.0f + ivec * GIZMO_ARROW_OFFSET,
                nivec * 0.01f + ivec * GIZMO_ARROW_OFFSET,
                nivec * 0.01f + ivec * GIZMO_ARROW_OFFSET,
                nivec * 0.12f + ivec * GIZMO_ARROW_OFFSET,
                nivec * 0.0f + ivec * (GIZMO_ARROW_OFFSET + GIZMO_ARROW_SIZE),
            };

            int arrowSides = 16;
            float arrowSidesStep = Mathf.Tau / arrowSides;
            for (int k = 0; k < arrowSides; k++)
            {
                Basis maa = new(ivec, k * arrowSidesStep);
                Basis mbb = new(ivec, (k + 1) * arrowSidesStep);
                for (int j = 0; j < arrowPoints - 1; j++)
                {
                    Vector3[] apoints = {
                        maa * arrow[j],
                        mbb * arrow[j],
                        mbb * arrow[j + 1],
                        maa * arrow[j + 1],
                    };
                    surfTool.AddVertex(apoints[0]);
                    surfTool.AddVertex(apoints[1]);
                    surfTool.AddVertex(apoints[2]);

                    surfTool.AddVertex(apoints[0]);
                    surfTool.AddVertex(apoints[1]);
                    surfTool.AddVertex(apoints[2]);
                }
            }
            surfTool.SetMaterial(mat);
            surfTool.Commit(MoveGizmo[i]);
#endregion
#region Plane Translation
            surfTool = new SurfaceTool();
            surfTool.Begin(Mesh.PrimitiveType.Triangles);

            Vector3 vec = ivec2 - ivec3;
            Vector3[] plane = {
                vec * GIZMO_PLANE_DST,
                vec * GIZMO_PLANE_DST + ivec2 * GIZMO_PLANE_SIZE,
                vec * (GIZMO_PLANE_DST + GIZMO_PLANE_SIZE),
                vec * GIZMO_PLANE_DST - ivec3 * GIZMO_PLANE_SIZE
            };

            Basis ma = new(ivec, Mathf.Pi / 2);
            Vector3[] points = {
                ma * plane[0],
                ma * plane[1],
                ma * plane[2],
                ma * plane[3],
            };
            surfTool.AddVertex(points[0]);
            surfTool.AddVertex(points[1]);
            surfTool.AddVertex(points[2]);

            surfTool.AddVertex(points[0]);
            surfTool.AddVertex(points[2]);
            surfTool.AddVertex(points[3]);

            StandardMaterial3D planeMat = new()
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                DisableFog = true,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                AlbedoColor = col
            };
            planeMat.SetOnTopOfAlpha();
            PlaneGizmoColor[i] = planeMat; // needed, so we can draw planes from both sides
            surfTool.SetMaterial(planeMat);
            surfTool.Commit(MovePlaneGizmo[i]);

            StandardMaterial3D planeMatHl = (StandardMaterial3D) planeMat.Duplicate();
            planeMatHl.AlbedoColor = albedo;
            PlaneGizmoColorHl[i] = planeMatHl; // needed, so we can draw planes from both sides
#endregion
#region Rotation
            surfTool = new();
            surfTool.Begin(Mesh.PrimitiveType.Triangles);

            const int CIRCLE_SEGMENTS = 128;
            const int CIRCLE_SEGMENT_THICKNESS = 3;

            float step = Mathf.Tau / CIRCLE_SEGMENTS;
            for (int j = 0; j < CIRCLE_SEGMENTS; ++j)
            {
                Basis basis = new(ivec, j * step);
                Vector3 vertex = basis * (ivec2 * GIZMO_CIRCLE_SIZE);
                for (int k = 0; k < CIRCLE_SEGMENT_THICKNESS; ++k) {
                    Vector2 ofs = new(Mathf.Cos((Mathf.Tau * k) / CIRCLE_SEGMENT_THICKNESS), Mathf.Sin((Mathf.Tau * k) / CIRCLE_SEGMENT_THICKNESS));
                    Vector3 normal = ivec * ofs.X + ivec2 * ofs.Y;
                    surfTool.SetNormal(basis * normal);
                    surfTool.AddVertex(vertex);
                }
            }

            for (int j = 0; j < CIRCLE_SEGMENTS; ++j)
            {
                for (int k = 0; k < CIRCLE_SEGMENT_THICKNESS; ++k)
                {
                    int currentRing = j * CIRCLE_SEGMENT_THICKNESS;
                    int nextRing = ((j + 1) % CIRCLE_SEGMENTS) * CIRCLE_SEGMENT_THICKNESS;
                    int currentSegment = k;
                    int nextSegment = (k + 1) % CIRCLE_SEGMENT_THICKNESS;

                    surfTool.AddIndex(currentRing + nextSegment);
                    surfTool.AddIndex(currentRing + currentSegment);
                    surfTool.AddIndex(nextRing + currentSegment);

                    surfTool.AddIndex(nextRing + currentSegment);
                    surfTool.AddIndex(nextRing + nextSegment);
                    surfTool.AddIndex(currentRing + nextSegment);
                }
            }
#endregion
            Shader rotateShader = new()
            {
                Code = @"
// 3D editor rotation manipulator gizmo shader.

shader_type spatial;

render_mode unshaded, depth_test_disabled, fog_disabled;

uniform vec4 albedo;

mat3 orthonormalize(mat3 m) {
	vec3 x = normalize(m[0]);
	vec3 y = normalize(m[1] - x * dot(x, m[1]));
	vec3 z = m[2] - x * dot(x, m[2]);
	z = normalize(z - y * (dot(y, m[2])));
	return mat3(x,y,z);
}

void vertex() {
	mat3 mv = orthonormalize(mat3(MODELVIEW_MATRIX));
	vec3 n = mv * VERTEX;
	float orientation = dot(vec3(0.0, 0.0, -1.0), n);
	if (orientation <= 0.005) {
		VERTEX += NORMAL * 0.02;
	}
}

void fragment() {
	ALBEDO = albedo.rgb;
	ALPHA = albedo.a;
}"
            };

            ShaderMaterial rotateMat = new()
            {
                RenderPriority = (int) Material.RenderPriorityMax,
                Shader = rotateShader
            };
            rotateMat.SetShaderParameter("albedo", col);
            RotateGizmoColor[i] = rotateMat;

            var arrays = surfTool.CommitToArrays();
            RotateGizmo[i].AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            RotateGizmo[i].SurfaceSetMaterial(0, rotateMat);

            ShaderMaterial rotateMatHl = (ShaderMaterial) rotateMat.Duplicate();
            rotateMatHl.SetShaderParameter("albedo", albedo);
            RotateGizmoColorHl[i] = rotateMatHl;

            if (i == 2) // Rotation white outline
            {
                ShaderMaterial borderMat = (ShaderMaterial) rotateMat.Duplicate();

                Shader borderShader = new()
                {
                    Code = @"
// 3D editor rotation manipulator gizmo shader (white outline).

shader_type spatial;

render_mode unshaded, depth_test_disabled, fog_disabled;

uniform vec4 albedo;

mat3 orthonormalize(mat3 m) {
	vec3 x = normalize(m[0]);
	vec3 y = normalize(m[1] - x * dot(x, m[1]));
	vec3 z = m[2] - x * dot(x, m[2]);
	z = normalize(z - y * (dot(y, m[2])));
	return mat3(x, y, z);
}

void vertex() {
	mat3 mv = orthonormalize(mat3(MODELVIEW_MATRIX));
	mv = inverse(mv);
	VERTEX += NORMAL * 0.008;
	vec3 camera_dir_local = mv * vec3(0.0, 0.0, 1.0);
	vec3 camera_up_local = mv * vec3(0.0, 1.0, 0.0);
	mat3 rotation_matrix = mat3(cross(camera_dir_local, camera_up_local), camera_up_local, camera_dir_local);
	VERTEX = rotation_matrix * VERTEX;
}

void fragment() {
	ALBEDO = albedo.rgb;
	ALPHA = albedo.a;
}"
                    };

                    borderMat.Shader = borderShader;
                    borderMat.SetShaderParameter("albedo", new Color(0.75f, 0.75f, 0.75f, col.A / 3.0f));

                    RotateGizmo[3] = new ArrayMesh();
                    RotateGizmo[3].AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
                    RotateGizmo[3].SurfaceSetMaterial(0, borderMat);
                }
#region Scale
                surfTool = new();
                surfTool.Begin(Mesh.PrimitiveType.Triangles);

                // Cube arrow profile
                arrowPoints = 6;
                arrow = new[] {
                    nivec * 0.0f + ivec * 0.0f,
                    nivec * 0.01f + ivec * 0.0f,
                    nivec * 0.01f + ivec * 1.0f * GIZMO_SCALE_OFFSET,
                    nivec * 0.07f + ivec * 1.0f * GIZMO_SCALE_OFFSET,
                    nivec * 0.07f + ivec * 1.2f * GIZMO_SCALE_OFFSET,
                    nivec * 0.0f + ivec * 1.2f * GIZMO_SCALE_OFFSET,
                };

                arrowSides = 4;
                arrowSidesStep = Mathf.Tau / arrowSides;
                for (int k = 0; k < 4; k++)
                {
                    Basis maa = new(ivec, k * arrowSidesStep);
                    Basis mbb = new (ivec, (k + 1) * arrowSidesStep);
                    for (int j = 0; j < arrowPoints - 1; j++)
                    {
                        Vector3[] apoints = {
                            maa * arrow[j],
                            mbb * arrow[j],
                            mbb * arrow[j + 1],
                            maa * arrow[j + 1],
                        };
                        surfTool.AddVertex(apoints[0]);
                        surfTool.AddVertex(apoints[1]);
                        surfTool.AddVertex(apoints[2]);

                        surfTool.AddVertex(apoints[0]);
                        surfTool.AddVertex(apoints[2]);
                        surfTool.AddVertex(apoints[3]);
                    }
                }

                surfTool.SetMaterial(mat);
                surfTool.Commit(ScaleGizmo[i]);
#endregion
#region Plane Scale
                surfTool = new SurfaceTool();
                surfTool.Begin(Mesh.PrimitiveType.Triangles);

                vec = ivec2 - ivec3;
                plane = new[] {
                    vec * GIZMO_PLANE_DST,
                    vec * GIZMO_PLANE_DST + ivec2 * GIZMO_PLANE_SIZE,
                    vec * (GIZMO_PLANE_DST + GIZMO_PLANE_SIZE),
                    vec * GIZMO_PLANE_DST - ivec3 * GIZMO_PLANE_SIZE
                };

                ma = new(ivec, Mathf.Pi / 2);

                points = new[] {
                    ma * plane[0],
                    ma * plane[1],
                    ma * plane[2],
                    ma * plane[3],
                };
                surfTool.AddVertex(points[0]);
                surfTool.AddVertex(points[1]);
                surfTool.AddVertex(points[2]);

                surfTool.AddVertex(points[0]);
                surfTool.AddVertex(points[2]);
                surfTool.AddVertex(points[3]);

                planeMat = new()
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    DisableFog = true,
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                    AlbedoColor = col
                };
                planeMat.SetOnTopOfAlpha();
                PlaneGizmoColor[i] = planeMat; // needed, so we can draw planes from both sides
                surfTool.SetMaterial(planeMat);
                surfTool.Commit(ScalePlaneGizmo[i]);

                planeMatHl = (StandardMaterial3D) planeMat.Duplicate();
                planeMatHl.AlbedoColor = Color.FromHsv(col.H, 0.25f, 1.0f, 1.0f);
                PlaneGizmoColorHl[i] = planeMatHl; // needed, so we can draw planes from both sides
#endregion
#region Lines to visualize transforms locked to an axis/plane
                surfTool = new SurfaceTool();
                surfTool.Begin(Mesh.PrimitiveType.LineStrip);

                vec = new();
                vec[i] = 1;
                // line extending through infinity(ish)
                surfTool.AddVertex(vec * -1048576);
                surfTool.AddVertex(new());
                surfTool.AddVertex(vec * 1048576);
                surfTool.SetMaterial(matHl);
                surfTool.Commit(AxisGizmo[i]);
        }
#endregion
#endregion
	    GenerateSelectionBoxes();
    }

    void UpdateTransformGizmoView()
    {
        if (!Visible)
        {
            SetVisibility(false);
            return;
        }

        Camera3D camera = GetViewport().GetCamera3D();
        Transform3D xform = Transform;
        Transform3D cameraTransform = camera.GetCameraTransform();

        if (xform.Origin.IsEqualApprox(cameraTransform.Origin))
        {
            SetVisibility(false);
            return;
        }

        Vector3 camz = -cameraTransform.Basis.Column2.Normalized();
        Vector3 camy = -cameraTransform.Basis.Column1.Normalized();
        Plane p = new(camz, cameraTransform.Origin);
        float gizmoD = Mathf.Max(Mathf.Abs(p.DistanceTo(xform.Origin)), Mathf.Epsilon);
        float d0 = camera.UnprojectPosition(cameraTransform.Origin + camz * gizmoD).Y;
        float d1 = camera.UnprojectPosition(cameraTransform.Origin + camz * gizmoD + camy).Y;
        float dd = Mathf.Max(Mathf.Abs(d0 - d1), Mathf.Epsilon);

        gizmoScale = Size / Mathf.Abs(dd);
        Vector3 scale = Vector3.One * gizmoScale;

        // if the determinant is zero, we should disable the gizmo from being rendered
        // this prevents supplying bad values to the renderer and then having to filter it out again
        if (xform.Basis.Determinant() == 0)
        {
            SetVisibility(false);
            return;
        }

        for (int i = 0; i < 3; i++)
        {
            Transform3D axisAngle = Transform3D.Identity;
            if (xform.Basis[i].Normalized().Dot(xform.Basis[(i + 1) % 3].Normalized()) < 1.0f)
                axisAngle = axisAngle.LookingAt(xform.Basis[i].Normalized(), xform.Basis[(i + 1) % 3].Normalized());
            axisAngle.Basis *= Basis.Scaled(scale);
            axisAngle.Origin = xform.Origin;
            InstanceSetTransform(MoveGizmoInstance[i], axisAngle);
            InstanceSetVisible(MoveGizmoInstance[i], Mode == ToolMode.All || Mode == ToolMode.Move);
            InstanceSetVisible(MoveGizmoInstance[i], true);
            InstanceSetTransform(MovePlaneGizmoInstance[i], axisAngle);
            InstanceSetVisible(MovePlaneGizmoInstance[i], Mode == ToolMode.All || Mode == ToolMode.Move);
            InstanceSetVisible(MovePlaneGizmoInstance[i], true);
            InstanceSetTransform(RotateGizmoInstance[i], axisAngle);
            InstanceSetVisible(RotateGizmoInstance[i], Mode == ToolMode.All || Mode == ToolMode.Rotate);
            InstanceSetTransform(ScaleGizmoInstance[i], axisAngle);
            InstanceSetVisible(ScaleGizmoInstance[i], Mode == ToolMode.All || Mode == ToolMode.Scale);
            InstanceSetTransform(ScalePlaneGizmoInstance[i], axisAngle);
            InstanceSetVisible(ScalePlaneGizmoInstance[i], Mode == ToolMode.Scale);
            InstanceSetTransform(AxisGizmoInstance[i], xform);
        }

        bool showAxes = ShowAxes && Editing;
        InstanceSetVisible(AxisGizmoInstance[0], showAxes && (edit.Plane == TransformPlane.X || edit.Plane == TransformPlane.XY || edit.Plane == TransformPlane.XZ));
        InstanceSetVisible(AxisGizmoInstance[1], showAxes && (edit.Plane == TransformPlane.Y || edit.Plane == TransformPlane.XY || edit.Plane == TransformPlane.YZ));
        InstanceSetVisible(AxisGizmoInstance[2], showAxes && (edit.Plane == TransformPlane.Z || edit.Plane == TransformPlane.XZ || edit.Plane == TransformPlane.YZ));

        // Rotation white outline
        xform = xform.Orthonormalized();
        xform.Basis *= xform.Basis.Scaled(scale);
        InstanceSetTransform(RotateGizmoInstance[3], xform);
        InstanceSetVisible(RotateGizmoInstance[3], Mode == ToolMode.All || Mode == ToolMode.Rotate);
    }

    void SetVisibility(bool visible)
    {
        for (int i = 0; i < 3; i++)
        {
            InstanceSetVisible(MoveGizmoInstance[i], visible);
            InstanceSetVisible(MovePlaneGizmoInstance[i], visible);
            InstanceSetVisible(RotateGizmoInstance[i], visible);
            InstanceSetVisible(ScaleGizmoInstance[i], visible);
            InstanceSetVisible(ScalePlaneGizmoInstance[i], visible);
            InstanceSetVisible(AxisGizmoInstance[i], visible);
        }
        // Rotation white outline
        InstanceSetVisible(RotateGizmoInstance[3], visible);
    }

    void GenerateSelectionBoxes()
    {
        // Use two AABBs to create the illusion of a slightly thicker line.
        Aabb aabb = new(new(), Vector3.One);

        // Create a x-ray (visible through solid surfaces) and standard version of the selection box.
        // Both will be drawn at the same position, but with different opacity.
        // This lets the user see where the selection is while still having a sense of depth.
        SurfaceTool st = new();
        SurfaceTool stXray = new();

        st.Begin(Mesh.PrimitiveType.Lines);
        stXray.Begin(Mesh.PrimitiveType.Lines);
        for (int i = 0; i < 12; i++)
        {
            aabb.GetEdge(i, out var a, out var b);
            st.AddVertex(a);
            st.AddVertex(b);
            stXray.AddVertex(a);
            stXray.AddVertex(b);
        }

        StandardMaterial3D mat = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            DisableFog = true,
            AlbedoColor = SelectionBoxColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        st.SetMaterial(mat);
        selectionBox = st.Commit();

        StandardMaterial3D matXray = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            DisableFog = true,
            NoDepthTest = true,
            AlbedoColor = SelectionBoxColor * new Color(1, 1, 1, 0.15f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        stXray.SetMaterial(matXray);
        selectionBoxXray = stXray.Commit();
    }

    void SelectGizmoHighlightAxis(int axis)
    {
        for (int i = 0; i < 3; i++) {
            MoveGizmo[i].SurfaceSetMaterial(0, i == axis ? GizmoColorHl[i] : GizmoColor[i]);
            MovePlaneGizmo[i].SurfaceSetMaterial(0, (i + 6) == axis ? PlaneGizmoColorHl[i] : PlaneGizmoColor[i]);
            RotateGizmo[i].SurfaceSetMaterial(0, (i + 3) == axis ? RotateGizmoColorHl[i] : RotateGizmoColor[i]);
            ScaleGizmo[i].SurfaceSetMaterial(0, (i + 9) == axis ? GizmoColorHl[i] : GizmoColor[i]);
            ScalePlaneGizmo[i].SurfaceSetMaterial(0, (i + 12) == axis ? PlaneGizmoColorHl[i] : PlaneGizmoColor[i]);
        }
    }

    bool TransformGizmoSelect(Vector2 screenpos, bool highlightOnly = false)
    {
        if (!Visible)
            return false;

        if (Target == null)
        {
            if (highlightOnly)
                SelectGizmoHighlightAxis(-1);
            return false;
        }

        Vector3 rayPos = GetRayPos(screenpos);
        Vector3 ray = GetRay(screenpos);
        Transform3D gt = Transform;

        if (Mode == ToolMode.All || Mode == ToolMode.Move)
        {
            int colAxis = -1;
            float colD = 1e20F;

            for (int i = 0; i < 3; i++)
            {
                Vector3 grabberPos = gt.Origin + gt.Basis[i].Normalized() * gizmoScale * (GIZMO_ARROW_OFFSET + (GIZMO_ARROW_SIZE * 0.5f));
                float grabberRadius = gizmoScale * GIZMO_ARROW_SIZE;

                Vector3[] r = Geometry3D.SegmentIntersectsSphere(rayPos, rayPos + ray * MAX_Z, grabberPos, grabberRadius);
                if (r.Length != 0)
                {
                    float d = r[0].DistanceTo(rayPos);
                    if (d < colD)
                    {
                        colD = d;
                        colAxis = i;
                    }
                }
            }

            bool isPlaneTranslate = false;
            // plane select
            if (colAxis == -1)
            {
                colD = 1e20F;

                for (int i = 0; i < 3; i++)
                {
                    Vector3 ivec2 = gt.Basis[(i + 1) % 3].Normalized();
                    Vector3 ivec3 = gt.Basis[(i + 2) % 3].Normalized();

                    // Allow some tolerance to make the plane easier to click,
                    // even if the click is actually slightly outside the plane.
                    Vector3 grabberPos = gt.Origin + (ivec2 + ivec3) * gizmoScale * (GIZMO_PLANE_SIZE + GIZMO_PLANE_DST * 0.6667f);

                    Plane plane = new(gt.Basis[i].Normalized(), gt.Origin);
                    Vector3? r = plane.IntersectsRay(rayPos, ray);

                    if (r != null)
                    {
                        float dist = r.Value.DistanceTo(grabberPos);
                        // Allow some tolerance to make the plane easier to click,
                        // even if the click is actually slightly outside the plane.
                        if (dist < (gizmoScale * GIZMO_PLANE_SIZE * 1.5f))
                        {
                            float d = rayPos.DistanceTo(r.Value);
                            if (d < colD) {
                                colD = d;
                                colAxis = i;

                                isPlaneTranslate = true;
                            }
                        }
                    }
                }
            }

            if (colAxis != -1)
            {
                if (highlightOnly)
                {
                    SelectGizmoHighlightAxis(colAxis + (isPlaneTranslate ? 6 : 0));
                }
                else
                {
                    //handle plane translate
                    edit.Mode = TransformMode.Translate;
                    ComputeEdit(screenpos);
                    edit.Plane = TransformPlane.X + colAxis + (isPlaneTranslate ? 3 : 0);
                }
                return true;
            }
        }

        if (Mode == ToolMode.All || Mode == ToolMode.Rotate)
        {
            int colAxis = -1;

            float rayLength = gt.Origin.DistanceTo(rayPos) + (GIZMO_CIRCLE_SIZE * gizmoScale) * 4.0f;
            Vector3[] result = Geometry3D.SegmentIntersectsSphere(rayPos, rayPos + ray * rayLength, gt.Origin, gizmoScale * GIZMO_CIRCLE_SIZE);
            if (result.Length != 0)
            {
                Vector3 hitPosition = result[0];
                Vector3 hitNormal = result[1];
                if (hitNormal.Dot(GetCameraNormal()) < 0.05f)
                {
                    hitPosition = (hitPosition * gt).Abs();
                    int minAxis = (int) hitPosition.MinAxisIndex();
                    if (hitPosition[minAxis] < gizmoScale * GIZMO_RING_HALF_WIDTH)
                        colAxis = minAxis;
                }
            }

            if (colAxis == -1)
            {
                float colD = 1e20F;

                for (int i = 0; i < 3; i++)
                {
                    Plane plane = new(gt.Basis[i].Normalized(), gt.Origin);
                    Vector3? r = plane.IntersectsRay(rayPos, ray);
                    if (r == null)
                        continue;

                    float dist = r.Value.DistanceTo(gt.Origin);
                    Vector3 rDir = (r.Value - gt.Origin).Normalized();

                    if (GetCameraNormal().Dot(rDir) <= 0.005f) {
                        if (dist > gizmoScale * (GIZMO_CIRCLE_SIZE - GIZMO_RING_HALF_WIDTH) && dist < gizmoScale * (GIZMO_CIRCLE_SIZE + GIZMO_RING_HALF_WIDTH))
                        {
                            float d = rayPos.DistanceTo(r.Value);
                            if (d < colD)
                            {
                                colD = d;
                                colAxis = i;
                            }
                        }
                    }
                }
            }

            if (colAxis != -1)
            {
                if (highlightOnly)
                {
                    SelectGizmoHighlightAxis(colAxis + 3);
                }
                else
                {
                    //handle rotate
                    edit.Mode = TransformMode.Rotate;
                    ComputeEdit(screenpos);
                    edit.Plane = TransformPlane.X + colAxis;
                }
                return true;
            }
        }

        if (Mode == ToolMode.All || Mode == ToolMode.Scale)
        {
            int colAxis = -1;
            float colD = 1e20F;

            for (int i = 0; i < 3; i++) {
                Vector3 grabberPos = gt.Origin + gt.Basis[i].Normalized() * gizmoScale * GIZMO_SCALE_OFFSET;
                float grabberRadius = gizmoScale * GIZMO_ARROW_SIZE;

                Vector3[] r = Geometry3D.SegmentIntersectsSphere(rayPos, rayPos + ray * MAX_Z, grabberPos, grabberRadius);
                if (r.Length != 0)
                {
                    float d = r[0].DistanceTo(rayPos);
                    if (d < colD)
                    {
                        colD = d;
                        colAxis = i;
                    }
                }
            }

            bool isPlaneScale = false;
            // plane select
            if (colAxis == -1)
            {
                colD = 1e20F;

                for (int i = 0; i < 3; i++)
                {
                    Vector3 ivec2 = gt.Basis[(i + 1) % 3].Normalized();
                    Vector3 ivec3 = gt.Basis[(i + 2) % 3].Normalized();

                    // Allow some tolerance to make the plane easier to click,
                    // even if the click is actually slightly outside the plane.
                    Vector3 grabberPos = gt.Origin + (ivec2 + ivec3) * gizmoScale * (GIZMO_PLANE_SIZE + GIZMO_PLANE_DST * 0.6667f);

                    Plane plane = new(gt.Basis[i].Normalized(), gt.Origin);
                    Vector3? r = plane.IntersectsRay(rayPos, ray);

                    if (r != null)
                    {
                        float dist = r.Value.DistanceTo(grabberPos);
                        // Allow some tolerance to make the plane easier to click,
                        // even if the click is actually slightly outside the plane.
                        if (dist < (gizmoScale * GIZMO_PLANE_SIZE * 1.5f))
                        {
                            float d = rayPos.DistanceTo(r.Value);
                            if (d < colD)
                            {
                                colD = d;
                                colAxis = i;

                                isPlaneScale = true;
                            }
                        }
                    }
                }
            }

            if (colAxis != -1)
            {
                if (highlightOnly)
                {
                    SelectGizmoHighlightAxis(colAxis + (isPlaneScale ? 12 : 9));
                }
                else
                {
                    //handle scale
                    edit.Mode = TransformMode.Scale;
                    ComputeEdit(screenpos);
                    edit.Plane = TransformPlane.X + colAxis + (isPlaneScale ? 3 : 0);
                }
                return true;
            }
        }

        if (highlightOnly)
            SelectGizmoHighlightAxis(-1);
        return false;
    }

    void TransformGizmoApply(Node3D node, Transform3D transform, bool local)
    {
        if (transform.Basis.Determinant() == 0)
            return;
        if (local) node.Transform = transform;
        else node.GlobalTransform = transform;
    }

    Transform3D ComputeTransform(TransformMode mode, Transform3D original, Transform3D originalLocal, Vector3 motion, float extra, bool local, bool orthogonal)
    {
        switch (mode)
        {
            case TransformMode.Scale:
                if (Snapping)
                    motion = motion.Snapped(extra);
                Transform3D s = Transform3D.Identity;
                if (local)
                {
                    s.Basis = original.Basis * Basis.Scaled(motion + new Vector3(1, 1, 1));
                    s.Origin = originalLocal.Origin;
                }
                else
                {
                    s.Basis = s.Basis.Scaled(motion + new Vector3(1, 1, 1));
                    Transform3D @base = new(Basis.Identity, edit.Center);
                    s = @base * (s * (@base.Inverse() * original));
                    // Recalculate orthogonalized scale without moving origin.
                    if (orthogonal)
                        s.Basis = original.Basis.ScaledOrthogonal(motion + new Vector3(1, 1, 1));
                }
                return s;
            case TransformMode.Translate:
                if (Snapping)
                    motion = motion.Snapped(extra);
                if (local)
                    return originalLocal.TranslatedLocal(motion);
                return original.Translated(motion);
            case TransformMode.Rotate:
                Transform3D r;
                if (local)
                {
                    Vector3 axis = originalLocal.Basis * motion;
                    r.Basis = new Basis(axis.Normalized(), extra) * originalLocal.Basis;
                    r.Origin = originalLocal.Origin;
                }
                else
                {
                    Basis blocal = original.Basis * originalLocal.Basis.Inverse();
                    Vector3 axis = motion * blocal;
                    r.Basis = blocal * new Basis(axis.Normalized(), extra) * originalLocal.Basis;
                    r.Origin = new Basis(motion, extra) * (original.Origin - edit.Center) + edit.Center;
                }
                return r;
            default:
                GD.PushError("Gizmo3D#ComputeTransform: Invalid mode");
                return default;
        }
    }

    void UpdateTransform(bool shift)
    {
        Vector3 rayPos = GetRayPos(edit.MousePos);
        Vector3 ray = GetRay(edit.MousePos);
        float snap = DEFAULT_FLOAT_STEP;

        switch (edit.Mode)
        {
            case TransformMode.Scale:
                Vector3 smotionMask = Vector3.Zero;
                Plane splane = Godot.Plane.PlaneXY;
                bool splaneMv = false;

                switch (edit.Plane)
                {
                    case TransformPlane.View:
                        smotionMask = Vector3.Zero;
                        splane = new(GetCameraNormal(), edit.Center);
                        break;
                    case TransformPlane.X:
                        smotionMask = Transform.Basis[0].Normalized();
                        splane = new(smotionMask.Cross(smotionMask.Cross(GetCameraNormal())).Normalized(), edit.Center);
                        break;
                    case TransformPlane.Y:
                        smotionMask = Transform.Basis[1].Normalized();
                        splane = new(smotionMask.Cross(smotionMask.Cross(GetCameraNormal())).Normalized(), edit.Center);
                        break;
                    case TransformPlane.Z:
                        smotionMask = Transform.Basis[2].Normalized();
                        splane = new(smotionMask.Cross(smotionMask.Cross(GetCameraNormal())).Normalized(), edit.Center);
                        break;
                    case TransformPlane.YZ:
                        smotionMask = Transform.Basis[2].Normalized() + Transform.Basis[1].Normalized();
                        splane = new(Transform.Basis[0].Normalized(), edit.Center);
                        splaneMv = true;
                        break;
                    case TransformPlane.XZ:
                        smotionMask = Transform.Basis[2].Normalized() + Transform.Basis[0].Normalized();
                        splane = new(Transform.Basis[1].Normalized(), edit.Center);
                        splaneMv = true;
                        break;
                    case TransformPlane.XY:
                        smotionMask = Transform.Basis[0].Normalized() + Transform.Basis[1].Normalized();
                        splane = new(Transform.Basis[2].Normalized(), edit.Center);
                        splaneMv = true;
                        break;
                }

                Vector3? sintersection = splane.IntersectsRay(rayPos, ray);
                if (sintersection == null)
                    break;

                Vector3? sclick = splane.IntersectsRay(edit.ClickRayPos, edit.ClickRay);
                if (sclick == null)
                    break;

                Vector3 smotion = sintersection.Value - sclick.Value;
                if (edit.Plane != TransformPlane.View)
                {
                    if (!splaneMv)
                        smotion = smotionMask.Dot(smotion) * smotionMask;
                    else if (shift) // Alternative planar scaling mode
                        smotion = smotionMask.Dot(smotion) * smotionMask;

                } else {
                    float centerClickDist = sclick.Value.DistanceTo(edit.Center);
                    float centerIntersDist = sintersection.Value.DistanceTo(edit.Center);
                    if (centerClickDist == 0)
                        break;
                    float scale = centerIntersDist - centerClickDist;
                    smotion = new(scale, scale, scale);
                }

                smotion /= sclick.Value.DistanceTo(edit.Center);

                // Disable local transformation for TRANSFORM_VIEW
                bool slocalCoords = LocalCoords && edit.Plane != TransformPlane.View;

                if (Snapping)
                    snap = ScaleSnap;

                Vector3 smotionSnapped = smotion;
                smotionSnapped = smotionSnapped.Snapped(snap);
                Message = TranslationServer.Translate("Scaling") + $": ({smotionSnapped.X:0.###}, {smotionSnapped.Y:0.###}, {smotionSnapped.Z:0.###})";
                if (slocalCoords)
                    smotion = edit.TargetOriginal.Basis.Inverse() * smotion; // TODO: needed?

                ApplyTransform(smotion, snap);
                break;

            case TransformMode.Translate:
                Vector3 tmotionMask = Vector3.Zero;
                Plane tplane = Godot.Plane.PlaneXY;
                bool tplaneMv = false;

                switch (edit.Plane)
                {
                    case TransformPlane.View:
                        tplane = new(GetCameraNormal(), edit.Center);
                        break;
                    case TransformPlane.X:
                        tmotionMask = Transform.Basis[0].Normalized();
                        tplane = new(tmotionMask.Cross(tmotionMask.Cross(GetCameraNormal())).Normalized(), edit.Center);
                        break;
                    case TransformPlane.Y:
                        tmotionMask = Transform.Basis[1].Normalized();
                        tplane = new(tmotionMask.Cross(tmotionMask.Cross(GetCameraNormal())).Normalized(), edit.Center);
                        break;
                    case TransformPlane.Z:
                        tmotionMask = Transform.Basis[2].Normalized();
                        tplane = new(tmotionMask.Cross(tmotionMask.Cross(GetCameraNormal())).Normalized(), edit.Center);
                        break;
                    case TransformPlane.YZ:
                        tplane = new(Transform.Basis[0].Normalized(), edit.Center);
                        tplaneMv = true;
                        break;
                    case TransformPlane.XZ:
                        tplane = new(Transform.Basis[1].Normalized(), edit.Center);
                        tplaneMv = true;
                        break;
                    case TransformPlane.XY:
                        tplane = new(Transform.Basis[2].Normalized(), edit.Center);
                        tplaneMv = true;
                        break;
                }

                Vector3? tintersection = tplane.IntersectsRay(rayPos, ray);
                if (tintersection == null)
                    break;

                Vector3? tclick = tplane.IntersectsRay(edit.ClickRayPos, edit.ClickRay);
                if (tclick == null)
                    break;

                Vector3 tmotion = tintersection.Value - tclick.Value;
                if (edit.Plane != TransformPlane.View && !tplaneMv)
                    tmotion = tmotionMask.Dot(tmotion) * tmotionMask;

                // Disable local transformation for TRANSFORM_VIEW
                bool tlocalCoords = LocalCoords && edit.Plane != TransformPlane.View;

                if (Snapping)
                    snap = TranslateSnap;

                Vector3 tmotionSnapped = tmotion;
                tmotionSnapped = tmotionSnapped.Snapped(snap);
                Message = TranslationServer.Translate("Translating") + $": ({tmotionSnapped.X:0.###}, {tmotionSnapped.Y:0.###}, {tmotionSnapped.Z:0.###})";
                if (tlocalCoords)
                    tmotion = Transform.Basis.Inverse() * tmotion;

                ApplyTransform(tmotion, snap);
                break;

            case TransformMode.Rotate:
                Plane rplane;
                Camera3D camera = GetViewport().GetCamera3D();
                if (camera.Projection == Camera3D.ProjectionType.Perspective)
                {
                    Vector3 camToObj = edit.Center - camera.GlobalTransform.Origin;
                    if (!camToObj.IsZeroApprox())
                        rplane = new(camToObj.Normalized(), edit.Center);
                    else
                        rplane = new(GetCameraNormal(), edit.Center);
                }
                else
                {
                    rplane = new(GetCameraNormal(), edit.Center);
                }

                Vector3 localAxis = Vector3.Zero;
                Vector3 globalAxis = Vector3.Zero;
                switch (edit.Plane)
                {
                    case TransformPlane.View:
                        // localAxis unused
                        globalAxis = rplane.Normal;
                        break;
                    case TransformPlane.X:
                        localAxis = new(1, 0, 0);
                        break;
                    case TransformPlane.Y:
                        localAxis = new(0, 1, 0);
                        break;
                    case TransformPlane.Z:
                        localAxis = new(0, 0, 1);
                        break;
                    case TransformPlane.YZ:
                    case TransformPlane.XZ:
                    case TransformPlane.XY:
                        break;
                }

                if (edit.Plane != TransformPlane.View)
                    globalAxis = (Transform.Basis * localAxis).Normalized();

                Vector3? rintersection = rplane.IntersectsRay(rayPos, ray);
                if (rintersection == null)
                    break;

                Vector3? rclick = rplane.IntersectsRay(edit.ClickRayPos, edit.ClickRay);
                if (rclick == null)
                    break;

                float orthogonalThreshold = Mathf.Cos(Mathf.DegToRad(85.0f));
                bool AxisIsOrthogonal = Mathf.Abs(rplane.Normal.Dot(globalAxis)) < orthogonalThreshold;

                float angle;
                if (AxisIsOrthogonal)
                {
                    Vector3 projectionAxis = rplane.Normal.Cross(globalAxis);
                    Vector3 delta = rintersection.Value - rclick.Value;
                    float projection = delta.Dot(projectionAxis);
                    angle = (projection * (Mathf.Pi / 2.0f)) / (gizmoScale * GIZMO_CIRCLE_SIZE);
                }
                else
                {
                    Vector3 clickAxis = (rclick.Value - edit.Center).Normalized();
                    Vector3 currentAxis = (rintersection.Value - edit.Center).Normalized();
                    angle = clickAxis.SignedAngleTo(currentAxis, globalAxis);
                }

                if (Snapping)
                    snap = RotateSnap;

                angle = Mathf.Snapped(Mathf.RadToDeg(angle), snap);
                Message = TranslationServer.Translate("Rotating") + $": {angle:0.###} " + TranslationServer.Translate("degrees");
                angle = Mathf.DegToRad(angle);

                bool rlocalCoords = LocalCoords && edit.Plane != TransformPlane.View; // Disable local transformation for TRANSFORM_VIEW
                Vector3 computeAxis = rlocalCoords ? localAxis : globalAxis;
                ApplyTransform(computeAxis, angle);
                break;
            default:
                break;
        }
	}

    void ApplyTransform(Vector3 motion, float snap)
    {
        bool localCoords = LocalCoords && edit.Plane != TransformPlane.View;
        Transform3D newTransform = ComputeTransform(edit.Mode, edit.TargetGlobal, edit.TargetOriginal, motion, snap, localCoords, edit.Plane != TransformPlane.View);
        TransformGizmoApply(Target, newTransform, localCoords);
    }

    void ComputeEdit(Vector2 point)
    {
        edit.TargetGlobal = Target.GlobalTransform;
        edit.TargetOriginal = Target.Transform;
        edit.ClickRay = GetRay(point);
        edit.ClickRayPos = GetRayPos(point);
        edit.Plane = TransformPlane.View;
        edit.Center = Transform.Origin;
    }

    Vector3 GetRayPos(Vector2 pos) => GetViewport().GetCamera3D().ProjectRayOrigin(pos);
    Vector3 GetRay(Vector2 pos) => GetViewport().GetCamera3D().ProjectRayNormal(pos);
    Vector3 GetCameraNormal() => -GetViewport().GetCamera3D().GlobalTransform.Basis[2];
}
