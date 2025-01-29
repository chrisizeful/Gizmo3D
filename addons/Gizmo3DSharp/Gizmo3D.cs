using System;
using Godot;
using System.Collections.Generic;
using static Godot.RenderingServer;

namespace Gizmo3DPlugin;

/// <summary>
/// Translated from C++ to C# with alterations, from:
/// - https://github.com/godotengine/godot/blob/master/editor/plugins/node_3d_editor_plugin.h
/// - https://github.com/godotengine/godot/blob/master/editor/plugins/node_3d_editor_plugin.cpp
/// </summary>
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

    /// <summary>
    /// Used to limit which transformations are being edited.
    /// </summary>
    [Export(PropertyHint.Flags)]
    public ToolMode Mode { get; set; } = ToolMode.Move | ToolMode.Scale | ToolMode.Rotate;

    uint layers = 1;
    /// <summary>
    /// The 3D render layers this gizmo is visible on.
    /// </summary>
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
            }
            InstanceSetLayerMask(RotateGizmoInstance[3], Layers);
            foreach (var item in Selections)
            {
                InstanceSetLayerMask(item.Value.SboxInstance, Layers);
                InstanceSetLayerMask(item.Value.SboxInstanceOffset, Layers);
                InstanceSetLayerMask(item.Value.SboxXrayInstance, Layers);
                InstanceSetLayerMask(item.Value.SboxXrayInstanceOffset, Layers);
            }
        }
    }

    /// <summary>
    /// The nodes this gizmo will apply transformations to.
    /// </summary>
    Dictionary<Node3D, SelectedItem> Selections = new();
    /// <summary>
    /// Whether or not transformations will be snapped to RotateSnap, ScaleSnap, and/or TranslateSnap.
    /// </summary>
    public bool Snapping { get; private set; }
    /// <summary>
    /// A displayable message describing the current transformation being applied, for example "Rotating: {60.000} degrees".
    /// </summary>
    public string Message { get; private set; }

    bool editing;
    /// <summary>
    /// If the user is currently interacting with is gizmo.
    /// </summary>
    public bool Editing
    {
        get => editing;
        private set
        {
            editing = value;
            if (!value)
                Message = "";
        }
    }

    /// <summary>
    /// If the user is currently hovering over a gizmo.
    /// </summary>
    public bool Hovering { get; private set; }

    [ExportGroup("Style")]
    /// <summary>
    /// The size of the gizmo before distance based scaling is applied.
    /// </summary>
    [Export(PropertyHint.Range, "30,200")]
    public float Size { get; set; } = 80.0f;
    // If the X/Y/Z axes extending to infinity are drawn.
    [Export]
    public bool ShowAxes { get; set; } = true;
    /// <summary>
    /// If the box encapsulating the target nodes is drawn.
    /// </summary>
    [Export]
    public bool ShowSelectionBox { get; set; } = true;

    float opacity = .9f;
    /// <summary>
    /// Alpha value for all gizmos and the selection box.
    /// </summary>
    [Export(PropertyHint.Range, "0,1")]
    public float Opacity
    {
        get => opacity;
        set
        {
            if (IsNodeReady())
                SetColors();
            opacity = value;
        }
    }

    Color[] colors = new Color[]
    {
        new(0.96f, 0.20f, 0.32f),
        new(0.53f, 0.84f, 0.01f),
        new(0.16f, 0.55f, 0.96f)
    };
    /// <summary>
    /// The colors of the gizmos. 0 is the X axis, 1 is the Y axis, and 2 is the Z axis.
    /// </summary>
    [Export(PropertyHint.ColorNoAlpha)]
    public Color[] Colors
    {
        get => colors;
        set
        {
            if (IsNodeReady())
                SetColors();
            colors = value;
        }
    }

    Color selectionBoxColor = new(1.0f, 0.5f, 0);
    /// <summary>
    /// The color of the AABB surrounding the target nodes.
    /// </summary>
    [Export(PropertyHint.ColorNoAlpha)]
    public Color SelectionBoxColor
    {
        get => selectionBoxColor;
        set
        {
            if (IsNodeReady())
            {
                SelectionBoxMat.AlbedoColor = new(value, value.A * Opacity);
                SelectionBoxXrayMat.AlbedoColor = value * new Color(1, 1, 1, 0.15f * Opacity);
            }
            selectionBoxColor = value;
        }
    }

    [ExportGroup("Position")]
    /// <summary>
    /// Whether the gizmo is displayed using the targets local coordinate space, or the global space.
    /// </summary>
    [Export]
    public bool UseLocalSpace { get; set; }
    /// <summary>
    /// Value to snap rotations to, if enabled.
    /// </summary>
    [Export(PropertyHint.Range, "0,360")]
    public float RotateSnap { get; set; } = 15.0f;
    /// <summary>
    /// Value to snap translations to, if enabled.
    /// </summary>
    [Export(PropertyHint.Range, "0,10")]
    public float TranslateSnap { get; set; } = 1.0f;
    /// <summary>
    /// Value to snap scaling to, if enabled.
    /// </summary>
    [Export(PropertyHint.Range, "0,5")]
    public float ScaleSnap { get; set; } = .25f;

    ArrayMesh[] MoveGizmo = new ArrayMesh[3];
    ArrayMesh[] MovePlaneGizmo = new ArrayMesh[3];
    ArrayMesh[] RotateGizmo = new ArrayMesh[4];
    ArrayMesh[] ScaleGizmo = new ArrayMesh[3];
    ArrayMesh[] ScalePlaneGizmo = new ArrayMesh[3];
    ArrayMesh[] AxisGizmo = new ArrayMesh[3];
    StandardMaterial3D[] GizmoColor = new StandardMaterial3D[3];
    StandardMaterial3D[] PlaneGizmoColor = new StandardMaterial3D[3];
    ShaderMaterial[] RotateGizmoColor = new ShaderMaterial[4];
    StandardMaterial3D[] GizmoColorHl = new StandardMaterial3D[3];
    StandardMaterial3D[] PlaneGizmoColorHl = new StandardMaterial3D[3];
    ShaderMaterial[] RotateGizmoColorHl = new ShaderMaterial[3];

    Rid[] MoveGizmoInstance = new Rid[3];
    Rid[] MovePlaneGizmoInstance = new Rid[3];
    Rid[] RotateGizmoInstance = new Rid[4];
    Rid[] ScaleGizmoInstance = new Rid[3];
    Rid[] ScalePlaneGizmoInstance = new Rid[3];
    Rid[] AxisGizmoInstance = new Rid[3];

    ArrayMesh SelectionBox, SelectionBoxXray;
    StandardMaterial3D SelectionBoxMat, SelectionBoxXrayMat;

    EditData Edit = new();
    float GizmoScale = 1.0f;

    [Flags]
    public enum ToolMode { Move = 1, Rotate = 2, Scale = 4, All = 7 };
    enum TransformMode { None, Rotate, Translate, Scale };
    enum TransformPlane { View, X, Y, Z, YZ, XZ, XY, };

    struct EditData
    {
        public Transform3D Original;
        public TransformMode Mode;
        public TransformPlane Plane;
        public Vector3 ClickRay, ClickRayPos;
        public Vector3 Center;
        public Vector2 MousePos;
    }

    struct SelectedItem
    {
        public Transform3D TargetOriginal, TargetGlobal;
        public Rid SboxInstance, SboxInstanceOffset;
        public Rid SboxXrayInstance, SboxXrayInstanceOffset;
    };

    public override void _Ready()
    {
        InitIndicators();
        SetColors();
        InitGizmoInstance();
        UpdateTransformGizmo();
        VisibilityChanged += () => SetVisibility(Visible);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        Hovering = false;
        if (!Visible)
        {
            Editing = false;
        }
        else if (@event is InputEventKey key && key.Keycode == Key.Ctrl)
        {
            Snapping = key.Pressed;
        }
        else if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left)
        {
            Editing = button.Pressed;
            if (!Editing)
            {
                UpdateTransformGizmo();
                return;
            }
            Edit.MousePos = button.Position;
            Editing = TransformGizmoSelect(button.Position);
        }
        else if (@event is InputEventMouseMotion motion)
        {
            if (Editing)
            {
                if (motion.ButtonMask.HasFlag(MouseButtonMask.Left))
                {
                    Edit.MousePos = motion.Position;
                    UpdateTransform(false);
                }
                return;
            }
            Hovering = TransformGizmoSelect(motion.Position, true);
        }
    }

#region Selection

    /// <summary>
    /// Add a node to the list of nodes currently being edited.
    /// </summary>
    /// <param name="target">The node to add.</param>
    public void Select(Node3D target)
    {
        Selections[target] = GetEditorData();
    }

    /// <summary>
    /// Remove a node from the list of nodes currently being edited.
    /// </summary>
    /// <param name="target">The node to remove.</param>
    public bool Deselect(Node3D target)
    {
        if (!Selections.TryGetValue(target, out var item))
            return false;
        Selections.Remove(target);
        FreeRid(item.SboxInstance);
        FreeRid(item.SboxInstanceOffset);
        FreeRid(item.SboxXrayInstance);
        FreeRid(item.SboxXrayInstanceOffset);
        return true;
    }

    /// <summary>
    /// Check if a node is currently selected.
    /// </summary>
    /// <param name="target">The node in question.</param>
    /// <returns>If the node is being edited.</returns>
    public bool IsSelected(Node3D target)
    {
        return Selections.ContainsKey(target);
    }

    /// <summary>
    /// Remove all nodes from the list of nodes currently being edited.
    /// </summary>
    public void ClearSelection()
    {
        foreach (var item in Selections)
            Deselect(item.Key);
    }

    /// <summary>
    /// Get the number of nodes currently being edited.
    /// </summary>
    /// <returns></returns>
    public int GetSelectedCount()
    {
        return Selections.Count;
    }

#endregion

    public override void _Process(double delta)
    {
        UpdateTransformGizmo();
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
        ClearSelection();
    }

    void InitGizmoInstance()
    {
        for (int i = 0; i < 3; i++)
        {
            MoveGizmoInstance[i] = InstanceCreate();
            InstanceSetBase(MoveGizmoInstance[i], MoveGizmo[i].GetRid());
            InstanceSetScenario(MoveGizmoInstance[i], GetWorld3D().Scenario);
            InstanceGeometrySetCastShadowsSetting(MoveGizmoInstance[i], ShadowCastingSetting.Off);
            InstanceSetLayerMask(MoveGizmoInstance[i], Layers);
            InstanceGeometrySetFlag(MoveGizmoInstance[i], InstanceFlags.IgnoreOcclusionCulling, true);
            InstanceGeometrySetFlag(MoveGizmoInstance[i], InstanceFlags.UseBakedLight, false);

            MovePlaneGizmoInstance[i] = InstanceCreate();
            InstanceSetBase(MovePlaneGizmoInstance[i], MovePlaneGizmo[i].GetRid());
            InstanceSetScenario(MovePlaneGizmoInstance[i], GetWorld3D().Scenario);
            InstanceGeometrySetCastShadowsSetting(MovePlaneGizmoInstance[i], ShadowCastingSetting.Off);
            InstanceSetLayerMask(MovePlaneGizmoInstance[i], Layers);
            InstanceGeometrySetFlag(MovePlaneGizmoInstance[i], InstanceFlags.IgnoreOcclusionCulling, true);
            InstanceGeometrySetFlag(MovePlaneGizmoInstance[i], InstanceFlags.UseBakedLight, false);

            RotateGizmoInstance[i] = InstanceCreate();
            InstanceSetBase(RotateGizmoInstance[i], RotateGizmo[i].GetRid());
            InstanceSetScenario(RotateGizmoInstance[i], GetWorld3D().Scenario);
            InstanceGeometrySetCastShadowsSetting(RotateGizmoInstance[i], ShadowCastingSetting.Off);
            InstanceSetLayerMask(RotateGizmoInstance[i], Layers);
            InstanceGeometrySetFlag(RotateGizmoInstance[i], InstanceFlags.IgnoreOcclusionCulling, true);
            InstanceGeometrySetFlag(RotateGizmoInstance[i], InstanceFlags.UseBakedLight, false);

            ScaleGizmoInstance[i] = InstanceCreate();
            InstanceSetBase(ScaleGizmoInstance[i], ScaleGizmo[i].GetRid());
            InstanceSetScenario(ScaleGizmoInstance[i], GetWorld3D().Scenario);
            InstanceGeometrySetCastShadowsSetting(ScaleGizmoInstance[i], ShadowCastingSetting.Off);
            InstanceSetLayerMask(ScaleGizmoInstance[i], Layers);
            InstanceGeometrySetFlag(ScaleGizmoInstance[i], InstanceFlags.IgnoreOcclusionCulling, true);
            InstanceGeometrySetFlag(ScaleGizmoInstance[i], InstanceFlags.UseBakedLight, false);

            ScalePlaneGizmoInstance[i] = InstanceCreate();
            InstanceSetBase(ScalePlaneGizmoInstance[i], ScalePlaneGizmo[i].GetRid());
            InstanceSetScenario(ScalePlaneGizmoInstance[i], GetWorld3D().Scenario);
            InstanceGeometrySetCastShadowsSetting(ScalePlaneGizmoInstance[i], ShadowCastingSetting.Off);
            InstanceSetLayerMask(ScalePlaneGizmoInstance[i], Layers);
            InstanceGeometrySetFlag(ScalePlaneGizmoInstance[i], InstanceFlags.IgnoreOcclusionCulling, true);
            InstanceGeometrySetFlag(ScalePlaneGizmoInstance[i], InstanceFlags.UseBakedLight, false);

            AxisGizmoInstance[i] = InstanceCreate();
            InstanceSetBase(AxisGizmoInstance[i], AxisGizmo[i].GetRid());
            InstanceSetScenario(AxisGizmoInstance[i], GetWorld3D().Scenario);
            InstanceGeometrySetCastShadowsSetting(AxisGizmoInstance[i], ShadowCastingSetting.Off);
            InstanceSetLayerMask(AxisGizmoInstance[i], Layers);
            InstanceGeometrySetFlag(AxisGizmoInstance[i], InstanceFlags.IgnoreOcclusionCulling, true);
            InstanceGeometrySetFlag(AxisGizmoInstance[i], InstanceFlags.UseBakedLight, false);
        }

        // Rotation white outline
        RotateGizmoInstance[3] = InstanceCreate();
        InstanceSetBase(RotateGizmoInstance[3], RotateGizmo[3].GetRid());
        InstanceSetScenario(RotateGizmoInstance[3], GetWorld3D().Scenario);
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
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
            mat.SetOnTopOfAlpha(true);
            GizmoColor[i] = mat;
            GizmoColorHl[i] = (StandardMaterial3D) mat.Duplicate();
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
                nivec * 0.0f + ivec * (GIZMO_ARROW_OFFSET + GIZMO_ARROW_SIZE)
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
                        maa * arrow[j + 1]
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
                ma * plane[3]
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
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
            planeMat.SetOnTopOfAlpha(true);
            PlaneGizmoColor[i] = planeMat;
            surfTool.SetMaterial(planeMat);
            surfTool.Commit(MovePlaneGizmo[i]);
            PlaneGizmoColorHl[i] = (StandardMaterial3D) planeMat.Duplicate();
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
                for (int k = 0; k < CIRCLE_SEGMENT_THICKNESS; ++k)
                {
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
            RotateGizmoColor[i] = rotateMat;

            var arrays = surfTool.CommitToArrays();
            RotateGizmo[i].AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            RotateGizmo[i].SurfaceSetMaterial(0, rotateMat);

            RotateGizmoColorHl[i] = (ShaderMaterial) rotateMat.Duplicate();

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
                    RotateGizmoColor[3] = borderMat;

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
                    nivec * 0.0f + ivec * 1.2f * GIZMO_SCALE_OFFSET
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
                            maa * arrow[j + 1]
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
                    ma * plane[3]
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
                    CullMode = BaseMaterial3D.CullModeEnum.Disabled
                };
                planeMat.SetOnTopOfAlpha(true);
                PlaneGizmoColor[i] = planeMat;
                surfTool.SetMaterial(planeMat);
                surfTool.Commit(ScalePlaneGizmo[i]);

                PlaneGizmoColorHl[i] = (StandardMaterial3D) planeMat.Duplicate();
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
                surfTool.SetMaterial(GizmoColorHl[i]);
                surfTool.Commit(AxisGizmo[i]);
        }
#endregion
#endregion
	    GenerateSelectionBoxes();
    }

    void SetColors()
    {
        for (int i = 0; i < 3; i++)
        {
            Color col = new(colors[i], colors[i].A * Opacity);
            GizmoColor[i].AlbedoColor = col;
            PlaneGizmoColor[i].AlbedoColor = col;
            RotateGizmoColor[i].SetShaderParameter("albedo", col);

            Color albedo = Color.FromHsv(col.H, 0.25f, 1.0f, 1.0f);
            GizmoColorHl[i].AlbedoColor = albedo;
            PlaneGizmoColorHl[i].AlbedoColor = albedo;
            RotateGizmoColorHl[i].SetShaderParameter("albedo", albedo);
        }
        RotateGizmoColor[3].SetShaderParameter("albedo", new Color(0.75f, 0.75f, 0.75f, Opacity / 3.0f));
        SelectGizmoHighlightAxis(-1);
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
        Transform3D cameraTransform = camera.GlobalTransform;

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

        GizmoScale = Size / Mathf.Abs(dd);
        Vector3 scale = Vector3.One * GizmoScale;

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
            axisAngle.Basis *= Basis.FromScale(scale);
            axisAngle.Origin = xform.Origin;
            InstanceSetTransform(MoveGizmoInstance[i], axisAngle);
            InstanceSetVisible(MoveGizmoInstance[i], (Mode & ToolMode.Move) == ToolMode.Move);
            InstanceSetTransform(MovePlaneGizmoInstance[i], axisAngle);
            InstanceSetVisible(MovePlaneGizmoInstance[i], (Mode & ToolMode.Move) == ToolMode.Move);
            InstanceSetTransform(RotateGizmoInstance[i], axisAngle);
            InstanceSetVisible(RotateGizmoInstance[i], (Mode & ToolMode.Rotate) == ToolMode.Rotate);
            InstanceSetTransform(ScaleGizmoInstance[i], axisAngle);
            InstanceSetVisible(ScaleGizmoInstance[i], (Mode & ToolMode.Scale) == ToolMode.Scale);
            InstanceSetTransform(ScalePlaneGizmoInstance[i], axisAngle);
            InstanceSetVisible(ScalePlaneGizmoInstance[i], Mode == ToolMode.Scale);
            InstanceSetTransform(AxisGizmoInstance[i], xform);
        }

        bool showAxes = ShowAxes && Editing;
        InstanceSetVisible(AxisGizmoInstance[0], showAxes && (Edit.Plane == TransformPlane.X || Edit.Plane == TransformPlane.XY || Edit.Plane == TransformPlane.XZ));
        InstanceSetVisible(AxisGizmoInstance[1], showAxes && (Edit.Plane == TransformPlane.Y || Edit.Plane == TransformPlane.XY || Edit.Plane == TransformPlane.YZ));
        InstanceSetVisible(AxisGizmoInstance[2], showAxes && (Edit.Plane == TransformPlane.Z || Edit.Plane == TransformPlane.XZ || Edit.Plane == TransformPlane.YZ));

        // Rotation white outline
        xform = xform.Orthonormalized();
        xform.Basis *= xform.Basis.Scaled(scale);
        InstanceSetTransform(RotateGizmoInstance[3], xform);
        InstanceSetVisible(RotateGizmoInstance[3], (Mode & ToolMode.Rotate) == ToolMode.Rotate);

        // Selection box
        foreach (var item in Selections)
        {
            Aabb bounds = CalculateSpatialBounds(item.Key);

            Vector3 offset = new(0.005f, 0.005f, 0.005f);
            Basis aabbS = Basis.FromScale(bounds.Size + offset);
            Transform3D t = item.Key.GlobalTransform.TranslatedLocal(bounds.Position - offset / 2);
            t.Basis *= aabbS;

            offset = new(0.01f, 0.01f, 0.01f);
            aabbS = Basis.FromScale(bounds.Size + offset);
            Transform3D tOffset = item.Key.GlobalTransform.TranslatedLocal(bounds.Position - offset / 2);
            tOffset.Basis *= aabbS;

            InstanceSetTransform(item.Value.SboxInstance, t);
            InstanceSetVisible(item.Value.SboxInstance, ShowSelectionBox);
            InstanceSetTransform(item.Value.SboxInstanceOffset, tOffset);
            InstanceSetVisible(item.Value.SboxInstanceOffset, ShowSelectionBox);
            InstanceSetTransform(item.Value.SboxXrayInstance, t);
            InstanceSetVisible(item.Value.SboxXrayInstance, ShowSelectionBox);
            InstanceSetTransform(item.Value.SboxXrayInstanceOffset, tOffset);
            InstanceSetVisible(item.Value.SboxXrayInstanceOffset, ShowSelectionBox);
        }
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
        foreach (var item in Selections)
        {
            InstanceSetVisible(item.Value.SboxInstance, visible);
            InstanceSetVisible(item.Value.SboxInstanceOffset, visible);
            InstanceSetVisible(item.Value.SboxXrayInstance, visible);
            InstanceSetVisible(item.Value.SboxXrayInstanceOffset, visible);
        }
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

        st.SetMaterial(SelectionBoxMat = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            DisableFog = true,
            AlbedoColor = SelectionBoxColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        });
        SelectionBox = st.Commit();

        stXray.SetMaterial(SelectionBoxXrayMat = new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            DisableFog = true,
            NoDepthTest = true,
            AlbedoColor = SelectionBoxColor * new Color(1, 1, 1, 0.15f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        });
        SelectionBoxXray = stXray.Commit();
    }

    void SelectGizmoHighlightAxis(int axis)
    {
        for (int i = 0; i < 3; i++)
        {
            MoveGizmo[i].SurfaceSetMaterial(0, i == axis ? GizmoColorHl[i] : GizmoColor[i]);
            MovePlaneGizmo[i].SurfaceSetMaterial(0, (i + 6) == axis ? PlaneGizmoColorHl[i] : PlaneGizmoColor[i]);
            RotateGizmo[i].SurfaceSetMaterial(0, (i + 3) == axis ? RotateGizmoColorHl[i] : RotateGizmoColor[i]);
            ScaleGizmo[i].SurfaceSetMaterial(0, (i + 9) == axis ? GizmoColorHl[i] : GizmoColor[i]);
            ScalePlaneGizmo[i].SurfaceSetMaterial(0, (i + 12) == axis ? PlaneGizmoColorHl[i] : PlaneGizmoColor[i]);
        }
    }

    void UpdateTransformGizmo()
    {
        int count = 0;
        Vector3 gizmoCenter = default;
        Basis gizmoBasis = Basis.Identity;

        foreach (var item in Selections)
        {
            Transform3D xf = item.Key.GlobalTransform;
            gizmoCenter += xf.Origin;
            if (count == 0 && UseLocalSpace)
                gizmoBasis = xf.Basis;
            count++;
        }

        Visible = count > 0;
        Transform = new()
        {
            Origin = (count > 0) ? gizmoCenter / count : default,
            Basis = (count == 1) ? gizmoBasis : Basis.Identity
        };

        UpdateTransformGizmoView();
    }

    SelectedItem GetEditorData()
    {
        SelectedItem item = new();
        item.SboxInstance = InstanceCreate2(SelectionBox.GetRid(), GetWorld3D().Scenario);
        item.SboxInstanceOffset = InstanceCreate2(SelectionBox.GetRid(), GetWorld3D().Scenario);
        InstanceGeometrySetCastShadowsSetting(item.SboxInstance, ShadowCastingSetting.Off);
        InstanceGeometrySetCastShadowsSetting(item.SboxInstanceOffset, ShadowCastingSetting.Off);
        InstanceSetLayerMask(item.SboxInstance, Layers);
        InstanceSetLayerMask(item.SboxInstanceOffset, Layers);
        InstanceGeometrySetFlag(item.SboxInstance, InstanceFlags.IgnoreOcclusionCulling, true);
        InstanceGeometrySetFlag(item.SboxInstance, InstanceFlags.UseBakedLight, false);
        InstanceGeometrySetFlag(item.SboxInstanceOffset, InstanceFlags.IgnoreOcclusionCulling, true);
        InstanceGeometrySetFlag(item.SboxInstanceOffset, InstanceFlags.UseBakedLight, false);
        item.SboxXrayInstance = InstanceCreate2(SelectionBoxXray.GetRid(), GetWorld3D().Scenario);
        item.SboxXrayInstanceOffset = InstanceCreate2(SelectionBoxXray.GetRid(), GetWorld3D().Scenario);
        InstanceGeometrySetCastShadowsSetting(item.SboxXrayInstance, ShadowCastingSetting.Off);
        InstanceGeometrySetCastShadowsSetting(item.SboxXrayInstanceOffset, ShadowCastingSetting.Off);
        InstanceSetLayerMask(item.SboxXrayInstance, Layers);
        InstanceSetLayerMask(item.SboxXrayInstanceOffset, Layers);
        InstanceGeometrySetFlag(item.SboxXrayInstance, InstanceFlags.IgnoreOcclusionCulling, true);
        InstanceGeometrySetFlag(item.SboxXrayInstance, InstanceFlags.UseBakedLight, false);
        InstanceGeometrySetFlag(item.SboxXrayInstanceOffset, InstanceFlags.IgnoreOcclusionCulling, true);
        InstanceGeometrySetFlag(item.SboxXrayInstanceOffset, InstanceFlags.UseBakedLight, false);
        return item;
    }

    bool TransformGizmoSelect(Vector2 screenpos, bool highlightOnly = false)
    {
        if (!Visible)
            return false;

        if (Selections.Count == 0)
        {
            if (highlightOnly)
                SelectGizmoHighlightAxis(-1);
            return false;
        }

        Vector3 rayPos = GetRayPos(screenpos);
        Vector3 ray = GetRay(screenpos);
        Transform3D gt = Transform;

        if ((Mode & ToolMode.Move) == ToolMode.Move)
        {
            int colAxis = -1;
            float colD = 1e20F;

            for (int i = 0; i < 3; i++)
            {
                Vector3 grabberPos = gt.Origin + gt.Basis[i].Normalized() * GizmoScale * (GIZMO_ARROW_OFFSET + (GIZMO_ARROW_SIZE * 0.5f));
                float grabberRadius = GizmoScale * GIZMO_ARROW_SIZE;

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
                    Vector3 grabberPos = gt.Origin + (ivec2 + ivec3) * GizmoScale * (GIZMO_PLANE_SIZE + GIZMO_PLANE_DST * 0.6667f);

                    Plane plane = new(gt.Basis[i].Normalized(), gt.Origin);
                    Vector3? r = plane.IntersectsRay(rayPos, ray);

                    if (r != null)
                    {
                        float dist = r.Value.DistanceTo(grabberPos);
                        // Allow some tolerance to make the plane easier to click,
                        // even if the click is actually slightly outside the plane.
                        if (dist < GizmoScale * GIZMO_PLANE_SIZE * 1.5f)
                        {
                            float d = rayPos.DistanceTo(r.Value);
                            if (d < colD)
                            {
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
                    // handle plane translate
                    Edit.Mode = TransformMode.Translate;
                    ComputeEdit(screenpos);
                    Edit.Plane = TransformPlane.X + colAxis + (isPlaneTranslate ? 3 : 0);
                }
                return true;
            }
        }

        if ((Mode & ToolMode.Rotate) == ToolMode.Rotate)
        {
            int colAxis = -1;

            float rayLength = gt.Origin.DistanceTo(rayPos) + (GIZMO_CIRCLE_SIZE * GizmoScale) * 4.0f;
            Vector3[] result = Geometry3D.SegmentIntersectsSphere(rayPos, rayPos + ray * rayLength, gt.Origin, GizmoScale * GIZMO_CIRCLE_SIZE);
            if (result.Length != 0)
            {
                Vector3 hitPosition = result[0];
                Vector3 hitNormal = result[1];
                if (hitNormal.Dot(GetCameraNormal()) < 0.05f)
                {
                    hitPosition = (hitPosition * gt).Abs();
                    int minAxis = (int) hitPosition.MinAxisIndex();
                    if (hitPosition[minAxis] < GizmoScale * GIZMO_RING_HALF_WIDTH)
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

                    if (GetCameraNormal().Dot(rDir) <= 0.005f)
                    {
                        if (dist > GizmoScale * (GIZMO_CIRCLE_SIZE - GIZMO_RING_HALF_WIDTH) && dist < GizmoScale * (GIZMO_CIRCLE_SIZE + GIZMO_RING_HALF_WIDTH))
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
                    // handle rotate
                    Edit.Mode = TransformMode.Rotate;
                    ComputeEdit(screenpos);
                    Edit.Plane = TransformPlane.X + colAxis;
                }
                return true;
            }
        }

        if ((Mode & ToolMode.Scale) == ToolMode.Scale)
        {
            int colAxis = -1;
            float colD = 1e20F;

            for (int i = 0; i < 3; i++)
            {
                Vector3 grabberPos = gt.Origin + gt.Basis[i].Normalized() * GizmoScale * GIZMO_SCALE_OFFSET;
                float grabberRadius = GizmoScale * GIZMO_ARROW_SIZE;

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
                    Vector3 grabberPos = gt.Origin + (ivec2 + ivec3) * GizmoScale * (GIZMO_PLANE_SIZE + GIZMO_PLANE_DST * 0.6667f);

                    Plane plane = new(gt.Basis[i].Normalized(), gt.Origin);
                    Vector3? r = plane.IntersectsRay(rayPos, ray);

                    if (r != null)
                    {
                        float dist = r.Value.DistanceTo(grabberPos);
                        // Allow some tolerance to make the plane easier to click,
                        // even if the click is actually slightly outside the plane.
                        if (dist < (GizmoScale * GIZMO_PLANE_SIZE * 1.5f))
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
                    // handle scale
                    Edit.Mode = TransformMode.Scale;
                    ComputeEdit(screenpos);
                    Edit.Plane = TransformPlane.X + colAxis + (isPlaneScale ? 3 : 0);
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
                    s.Basis = originalLocal.Basis * Basis.FromScale(motion + Vector3.One);
                    s.Origin = originalLocal.Origin;
                }
                else
                {
                    s.Basis = s.Basis.Scaled(motion + Vector3.One);
                    Transform3D @base = new(Basis.Identity, Edit.Center);
                    s = @base * (s * (@base.Inverse() * original));
                    // Recalculate orthogonalized scale without moving origin.
                    if (orthogonal)
                        s.Basis = original.Basis.ScaledOrthogonal(motion + Vector3.One);
                }
                return s;
            case TransformMode.Translate:
                if (Snapping)
                    motion = motion.Snapped(extra);
                if (local)
                    return originalLocal.TranslatedLocal(motion);
                return original.Translated(motion);
            case TransformMode.Rotate:
                if (local)
                {
                    Vector3 axis = originalLocal.Basis * motion;
                    return new Transform3D(
                        new Basis(axis.Normalized(), extra) * originalLocal.Basis,
                        originalLocal.Origin);
                }
                else
                {
                    Basis blocal = original.Basis * originalLocal.Basis.Inverse();
                    Vector3 axis = motion * blocal;
                    return new Transform3D(
                        blocal * new Basis(axis.Normalized(), extra) * originalLocal.Basis,
                        new Basis(motion, extra) * (original.Origin - Edit.Center) + Edit.Center);
                }
            default:
                GD.PushError("Gizmo3D#ComputeTransform: Invalid mode");
                return default;
        }
    }

    void UpdateTransform(bool shift)
    {
        Vector3 rayPos = GetRayPos(Edit.MousePos);
        Vector3 ray = GetRay(Edit.MousePos);
        float snap = DEFAULT_FLOAT_STEP;

        switch (Edit.Mode)
        {
            case TransformMode.Scale:
                Vector3 smotionMask = default;
                Plane splane = default;
                bool splaneMv = false;

                switch (Edit.Plane)
                {
                    case TransformPlane.View:
                        smotionMask = Vector3.Zero;
                        splane = new(GetCameraNormal(), Edit.Center);
                        break;
                    case TransformPlane.X:
                        smotionMask = Transform.Basis[0].Normalized();
                        splane = new(smotionMask.Cross(smotionMask.Cross(GetCameraNormal())).Normalized(), Edit.Center);
                        break;
                    case TransformPlane.Y:
                        smotionMask = Transform.Basis[1].Normalized();
                        splane = new(smotionMask.Cross(smotionMask.Cross(GetCameraNormal())).Normalized(), Edit.Center);
                        break;
                    case TransformPlane.Z:
                        smotionMask = Transform.Basis[2].Normalized();
                        splane = new(smotionMask.Cross(smotionMask.Cross(GetCameraNormal())).Normalized(), Edit.Center);
                        break;
                    case TransformPlane.YZ:
                        smotionMask = Transform.Basis[2].Normalized() + Transform.Basis[1].Normalized();
                        splane = new(Transform.Basis[0].Normalized(), Edit.Center);
                        splaneMv = true;
                        break;
                    case TransformPlane.XZ:
                        smotionMask = Transform.Basis[2].Normalized() + Transform.Basis[0].Normalized();
                        splane = new(Transform.Basis[1].Normalized(), Edit.Center);
                        splaneMv = true;
                        break;
                    case TransformPlane.XY:
                        smotionMask = Transform.Basis[0].Normalized() + Transform.Basis[1].Normalized();
                        splane = new(Transform.Basis[2].Normalized(), Edit.Center);
                        splaneMv = true;
                        break;
                }

                Vector3? sintersection = splane.IntersectsRay(rayPos, ray);
                if (sintersection == null)
                    break;

                Vector3? sclick = splane.IntersectsRay(Edit.ClickRayPos, Edit.ClickRay);
                if (sclick == null)
                    break;

                Vector3 smotion = sintersection.Value - sclick.Value;
                if (Edit.Plane != TransformPlane.View)
                {
                    if (!splaneMv)
                        smotion = smotionMask.Dot(smotion) * smotionMask;
                    else if (shift) // Alternative planar scaling mode
                        smotion = smotionMask.Dot(smotion) * smotionMask;
                }
                else
                {
                    float centerClickDist = sclick.Value.DistanceTo(Edit.Center);
                    float centerIntersDist = sintersection.Value.DistanceTo(Edit.Center);
                    if (centerClickDist == 0)
                        break;
                    float scale = centerIntersDist - centerClickDist;
                    smotion = new(scale, scale, scale);
                }

                smotion /= sclick.Value.DistanceTo(Edit.Center);

                // Disable local transformation for TRANSFORM_VIEW
                bool slocalCoords = UseLocalSpace && Edit.Plane != TransformPlane.View;

                if (Snapping)
                    snap = ScaleSnap;

                Vector3 smotionSnapped = smotion.Snapped(snap);
                Message = TranslationServer.Translate("Scaling") + $": ({smotionSnapped.X:0.###}, {smotionSnapped.Y:0.###}, {smotionSnapped.Z:0.###})";
                if (slocalCoords)
                    smotion = Edit.Original.Basis.Inverse() * smotion;

                ApplyTransform(smotion, snap);
                break;

            case TransformMode.Translate:
                Vector3 tmotionMask = default;
                Plane tplane = default;
                bool tplaneMv = false;

                switch (Edit.Plane)
                {
                    case TransformPlane.View:
                        tplane = new(GetCameraNormal(), Edit.Center);
                        break;
                    case TransformPlane.X:
                        tmotionMask = Transform.Basis[0].Normalized();
                        tplane = new(tmotionMask.Cross(tmotionMask.Cross(GetCameraNormal())).Normalized(), Edit.Center);
                        break;
                    case TransformPlane.Y:
                        tmotionMask = Transform.Basis[1].Normalized();
                        tplane = new(tmotionMask.Cross(tmotionMask.Cross(GetCameraNormal())).Normalized(), Edit.Center);
                        break;
                    case TransformPlane.Z:
                        tmotionMask = Transform.Basis[2].Normalized();
                        tplane = new(tmotionMask.Cross(tmotionMask.Cross(GetCameraNormal())).Normalized(), Edit.Center);
                        break;
                    case TransformPlane.YZ:
                        tplane = new(Transform.Basis[0].Normalized(), Edit.Center);
                        tplaneMv = true;
                        break;
                    case TransformPlane.XZ:
                        tplane = new(Transform.Basis[1].Normalized(), Edit.Center);
                        tplaneMv = true;
                        break;
                    case TransformPlane.XY:
                        tplane = new(Transform.Basis[2].Normalized(), Edit.Center);
                        tplaneMv = true;
                        break;
                }

                Vector3? tintersection = tplane.IntersectsRay(rayPos, ray);
                if (tintersection == null)
                    break;

                Vector3? tclick = tplane.IntersectsRay(Edit.ClickRayPos, Edit.ClickRay);
                if (tclick == null)
                    break;

                Vector3 tmotion = tintersection.Value - tclick.Value;
                if (Edit.Plane != TransformPlane.View && !tplaneMv)
                    tmotion = tmotionMask.Dot(tmotion) * tmotionMask;

                // Disable local transformation for TRANSFORM_VIEW
                bool tlocalCoords = UseLocalSpace && Edit.Plane != TransformPlane.View;

                if (Snapping)
                    snap = TranslateSnap;

                Vector3 tmotionSnapped = tmotion.Snapped(snap);
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
                    Vector3 camToObj = Edit.Center - camera.GlobalTransform.Origin;
                    if (!camToObj.IsZeroApprox())
                        rplane = new(camToObj.Normalized(), Edit.Center);
                    else
                        rplane = new(GetCameraNormal(), Edit.Center);
                }
                else
                {
                    rplane = new(GetCameraNormal(), Edit.Center);
                }

                Vector3 localAxis = default;
                Vector3 globalAxis = default;
                switch (Edit.Plane)
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

                if (Edit.Plane != TransformPlane.View)
                    globalAxis = (Transform.Basis * localAxis).Normalized();

                Vector3? rintersection = rplane.IntersectsRay(rayPos, ray);
                if (rintersection == null)
                    break;

                Vector3? rclick = rplane.IntersectsRay(Edit.ClickRayPos, Edit.ClickRay);
                if (rclick == null)
                    break;

                float orthogonalThreshold = Mathf.Cos(Mathf.DegToRad(85.0f));
                bool axisIsOrthogonal = Mathf.Abs(rplane.Normal.Dot(globalAxis)) < orthogonalThreshold;

                float angle;
                if (axisIsOrthogonal)
                {
                    Vector3 projectionAxis = rplane.Normal.Cross(globalAxis);
                    Vector3 delta = rintersection.Value - rclick.Value;
                    float projection = delta.Dot(projectionAxis);
                    angle = (projection * (Mathf.Pi / 2.0f)) / (GizmoScale * GIZMO_CIRCLE_SIZE);
                }
                else
                {
                    Vector3 clickAxis = (rclick.Value - Edit.Center).Normalized();
                    Vector3 currentAxis = (rintersection.Value - Edit.Center).Normalized();
                    angle = clickAxis.SignedAngleTo(currentAxis, globalAxis);
                }

                if (Snapping)
                    snap = RotateSnap;

                angle = Mathf.Snapped(Mathf.RadToDeg(angle), snap);
                Message = TranslationServer.Translate("Rotating") + $": {angle:0.###} " + TranslationServer.Translate("degrees");
                angle = Mathf.DegToRad(angle);

                bool rlocalCoords = UseLocalSpace && Edit.Plane != TransformPlane.View; // Disable local transformation for TRANSFORM_VIEW
                Vector3 computeAxis = rlocalCoords ? localAxis : globalAxis;
                ApplyTransform(computeAxis, angle);
                break;
            default:
                break;
        }
	}

    void ApplyTransform(Vector3 motion, float snap)
    {
        bool localCoords = UseLocalSpace && Edit.Plane != TransformPlane.View;
        foreach (var item in Selections)
        {
            Transform3D newTransform = ComputeTransform(Edit.Mode, item.Value.TargetGlobal, item.Value.TargetOriginal, motion, snap, localCoords, Edit.Plane != TransformPlane.View);
            TransformGizmoApply(item.Key, newTransform, localCoords);
            UpdateTransformGizmo();
        }
    }

    void ComputeEdit(Vector2 point)
    {
        Edit.ClickRay = GetRay(point);
        Edit.ClickRayPos = GetRayPos(point);
        Edit.Plane = TransformPlane.View;
        UpdateTransformGizmo();
        Edit.Center = Transform.Origin;
        Edit.Original = Transform;
        foreach (var key in Selections.Keys)
        {
            SelectedItem item = Selections[key];
            item.TargetGlobal = key.GlobalTransform;
            item.TargetOriginal = key.Transform;
            Selections[key] = item;
        }
    }

    Aabb CalculateSpatialBounds(Node3D parent, bool omitTopLevel = false, Transform3D boundsOrientation = default)
    {
        Aabb bounds;

        Transform3D tBoundsOrientation;
        if (boundsOrientation != default)
            tBoundsOrientation = boundsOrientation;
        else
            tBoundsOrientation = parent.GlobalTransform;

        if (parent == null)
            return new Aabb(new(-0.2f, -0.2f, -0.2f), new(0.4f, 0.4f, 0.4f));

        Transform3D xfomToTopLevelParentSpace = tBoundsOrientation.AffineInverse() * parent.GlobalTransform;

        if (parent is VisualInstance3D vi)
            bounds = vi.GetAabb();
        else
            bounds = new();
        bounds = xfomToTopLevelParentSpace * bounds;

        foreach (Node child in parent.GetChildren())
        {
            if (child is not Node3D n3d)
                continue;
            if (!(omitTopLevel && n3d.TopLevel))
            {
                Aabb child_bounds = CalculateSpatialBounds(n3d, omitTopLevel, tBoundsOrientation);
                bounds = bounds.Merge(child_bounds);
            }
        }

        return bounds;
    }

    Vector3 GetRayPos(Vector2 pos) => GetViewport().GetCamera3D().ProjectRayOrigin(pos);
    Vector3 GetRay(Vector2 pos) => GetViewport().GetCamera3D().ProjectRayNormal(pos);
    Vector3 GetCameraNormal() => -GetViewport().GetCamera3D().GlobalTransform.Basis[2];
}
