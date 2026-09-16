using UnityEngine;

[ExecuteAlways]
public sealed class CppCollision : MonoBehaviour
{
    public enum CollisionMode
    {
        Auto,
        Box,
        Sphere,
        Capsule,
        Manual
    }

    public enum ManualShape
    {
        Box,
        Sphere,
        Capsule
    }

    [Header("Collision")]

    [SerializeField]
    private CollisionMode mode =
        CollisionMode.Auto;

    [SerializeField]
    private ManualShape manualShape =
        ManualShape.Box;

    [Header("Manual Box")]

    [SerializeField]
    private Vector3 manualCenter =
        Vector3.zero;

    [SerializeField]
    private Vector3 manualSize =
        Vector3.one;

    [Header("Manual Sphere")]

    [SerializeField]
    private float manualRadius =
        0.5f;

    [Header("Manual Capsule")]

    [SerializeField]
    private float manualHeight =
        1.0f;

    [Header("Debug")]

    [SerializeField]
    private bool showGizmo =
        true;

    public CollisionMode Mode =>
        mode;

    [field: SerializeField]
    public CollisionMode ResolvedMode {
        get;
        private set;
    }

    [field: SerializeField]
    public Vector3 WorldCenter {
        get;
        private set;
    }

    [field: SerializeField]
    public Vector3 WorldSize {
        get;
        private set;
    }

    [field: SerializeField]
    public float Radius {
        get;
        private set;
    }

    [field: SerializeField]
    public float Height {
        get;
        private set;
    }

    [Header("Diagnostics (read-only)")]

    [SerializeField]
    private float debugHorizontalRatio;

    [SerializeField]
    private bool debugIsRoundHorizontal;

    private void Awake()
    {
        Recalculate();
    }

    private void OnEnable()
    {
        Recalculate();
    }

    private void OnValidate()
    {
        Recalculate();
    }

    public void Recalculate()
    {
        Bounds bounds;

        if (!TryGetRendererBounds(
                out bounds)) {

            bounds =
                new Bounds(
                    transform.position,
                    Vector3.one);
        }

        if (mode ==
            CollisionMode.Manual) {

            ApplyManual();

            return;
        }

        WorldCenter =
            bounds.center;

        WorldSize =
            bounds.size;

        ResolvedMode =
            ResolveAutoMode(
                bounds);

        switch (ResolvedMode)
        {
            case CollisionMode.Box:

                Radius = 0.0f;

                Height =
                    bounds.size.y;

                break;

            case CollisionMode.Sphere:

                // Horizontal radius.
                //
                // Sphere mode is only chosen after the mesh
                // has passed the roundness check, so the
                // object is genuinely round and the max
                // horizontal extent IS the actual radius.
                //
                // The old half-diagonal formula was a
                // conservative bound for irregular meshes.
                // It massively overestimated for real
                // spheres and produced an oversized
                // collision footprint.
                Radius =
                    Mathf.Max(
                        bounds.extents.x,
                        bounds.extents.z);

                Height =
                    bounds.size.y;

                break;

            case CollisionMode.Capsule:

                Radius =
                    Mathf.Max(
                        bounds.extents.x,
                        bounds.extents.z);

                Height =
                    Mathf.Max(
                        bounds.size.y,
                        Radius * 2.0f);

                break;
        }
    }

    private CollisionMode ResolveAutoMode(
        Bounds bounds)
    {
        float width =
            Mathf.Max(
                bounds.size.x,
                0.0001f);

        float depth =
            Mathf.Max(
                bounds.size.z,
                0.0001f);

        float height =
            Mathf.Max(
                bounds.size.y,
                0.0001f);

        float horizontalMax =
            Mathf.Max(
                width,
                depth);

        float horizontalMin =
            Mathf.Min(
                width,
                depth);

        float horizontalRatio =
            horizontalMin /
            horizontalMax;

        debugHorizontalRatio =
            horizontalRatio;

        // Determine whether the horizontal cross-section is
        // actually round, rather than just tall-and-narrow.
        //
        // A tall rectangular column (e.g. a scaled Unity Cube)
        // must NOT become a Capsule, because a Capsule's
        // horizontal footprint is a circle inscribed in the
        // box, which does not match the visible object.
        bool isRoundHorizontal =
            HasRoundHorizontalFootprint();

        debugIsRoundHorizontal =
            isRoundHorizontal;

        bool isTallAndNarrow =
            height > horizontalMax * 1.6f;

        // Tall objects:
        //
        // - Round horizontal cross-section -> Capsule.
        // - Rectangular horizontal cross-section -> Box.
        if (isTallAndNarrow) {

            if (horizontalRatio >= 0.80f &&
                isRoundHorizontal) {

                return CollisionMode.Capsule;
            }

            return CollisionMode.Box;
        }

        // Compact objects:
        //
        // Only choose Sphere if the mesh is genuinely round
        // horizontally AND roughly equal in all dimensions.
        if (horizontalRatio >= 0.80f &&
            height <= horizontalMax * 1.35f &&
            isRoundHorizontal) {

            return CollisionMode.Sphere;
        }

        return CollisionMode.Box;
    }

    private bool HasRoundHorizontalFootprint()
    {
        // Compute the maximum XZ radius of any mesh vertex and
        // compare it to the inscribed radius of the mesh's
        // local bounding box.
        //
        //   Cylinder / capsule:
        //       all vertices sit at ~ the inscribed radius.
        //       maxRadius / inscribedRadius ~= 1.00
        //
        //   Box:
        //       corner vertices sit at sqrt(2) x inscribed radius.
        //       maxRadius / inscribedRadius ~= 1.41
        //
        // So a ratio below ~1.15 means "round".

        Renderer[] renderers =
            GetComponentsInChildren<
                Renderer>();

        float maxRadiusSq = 0.0f;
        float halfExtentX = 0.0f;
        float halfExtentZ = 0.0f;
        bool found = false;

        foreach (Renderer renderer
                 in renderers)
        {
            if (renderer == null ||
                !renderer.enabled) {

                continue;
            }

            MeshFilter filter =
                renderer.GetComponent<
                    MeshFilter>();

            if (filter == null ||
                filter.sharedMesh == null) {

                continue;
            }

            Mesh mesh =
                filter.sharedMesh;

            if (!mesh.isReadable) {
                continue;
            }

            Bounds meshBounds =
                mesh.bounds;

            Vector3[] verts =
                mesh.vertices;

            foreach (Vector3 v in verts)
            {
                float rsq =
                    v.x * v.x +
                    v.z * v.z;

                if (rsq > maxRadiusSq) {
                    maxRadiusSq = rsq;
                }
            }

            halfExtentX =
                Mathf.Max(
                    halfExtentX,
                    meshBounds.extents.x);

            halfExtentZ =
                Mathf.Max(
                    halfExtentZ,
                    meshBounds.extents.z);

            found = true;
        }

        if (!found ||
            maxRadiusSq <= 0.0f) {

            return false;
        }

        float maxRadius =
            Mathf.Sqrt(
                maxRadiusSq);

        float inscribedRadius =
            Mathf.Min(
                halfExtentX,
                halfExtentZ);

        if (inscribedRadius <= 0.0f) {
            return false;
        }

        return maxRadius <
            inscribedRadius * 1.15f;
    }

    private void ApplyManual()
    {
        ResolvedMode =
            mode;

        switch (manualShape)
        {
            case ManualShape.Box:

                WorldCenter =
                    transform.TransformPoint(
                        manualCenter);

                WorldSize =
                    Vector3.Scale(
                        manualSize,
                        AbsVector(
                            transform.lossyScale));

                Radius = 0.0f;

                Height =
                    WorldSize.y;

                ResolvedMode =
                    CollisionMode.Box;

                break;

            case ManualShape.Sphere:

                WorldCenter =
                    transform.TransformPoint(
                        manualCenter);

                Radius =
                    Mathf.Abs(
                        manualRadius) *
                    MaxAbsScale(
                        transform.lossyScale);

                WorldSize =
                    Vector3.one *
                    Radius *
                    2.0f;

                Height =
                    Radius * 2.0f;

                ResolvedMode =
                    CollisionMode.Sphere;

                break;

            case ManualShape.Capsule:

                WorldCenter =
                    transform.TransformPoint(
                        manualCenter);

                Radius =
                    Mathf.Abs(
                        manualRadius) *
                    Mathf.Max(
                        Mathf.Abs(
                            transform.lossyScale.x),
                        Mathf.Abs(
                            transform.lossyScale.z));

                Height =
                    Mathf.Max(
                        Mathf.Abs(
                            manualHeight *
                            transform.lossyScale.y),
                        Radius * 2.0f);

                WorldSize =
                    new Vector3(
                        Radius * 2.0f,
                        Height,
                        Radius * 2.0f);

                ResolvedMode =
                    CollisionMode.Capsule;

                break;
        }
    }

    private static Vector3 AbsVector(
        Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z));
    }

    private static float MaxAbsScale(
        Vector3 value)
    {
        return Mathf.Max(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z));
    }

    private bool TryGetRendererBounds(
        out Bounds combined)
    {
        Renderer[] renderers =
            GetComponentsInChildren<
                Renderer>();

        bool found =
            false;

        combined =
            new Bounds();

        foreach (Renderer renderer
                 in renderers)
        {
            if (renderer == null ||
                !renderer.enabled) {

                continue;
            }

            if (!found)
            {
                combined =
                    renderer.bounds;

                found = true;
            }
            else
            {
                combined.Encapsulate(
                    renderer.bounds);
            }
        }

        return found;
    }

    public void SendToNative()
    {
        Recalculate();

        switch (ResolvedMode)
        {
            case CollisionMode.Box:

                NativeCollision.AddBox(
                    WorldCenter,
                    WorldSize * 0.5f);

                break;

            case CollisionMode.Sphere:

                NativeCollision.AddSphere(
                    WorldCenter,
                    Radius);

                break;

            case CollisionMode.Capsule:

                NativeCollision.AddCapsule(
                    WorldCenter,
                    Radius,
                    Height);

                break;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo)
            return;

        Recalculate();

        switch (ResolvedMode)
        {
            case CollisionMode.Box:

                Gizmos.DrawWireCube(
                    WorldCenter,
                    WorldSize);

                break;

            case CollisionMode.Sphere:

                Gizmos.DrawWireSphere(
                    WorldCenter,
                    Radius);

                break;

            case CollisionMode.Capsule:

                DrawCapsuleGizmo();

                break;
        }
    }

    private void DrawCapsuleGizmo()
    {
        float cylinderHeight =
            Mathf.Max(
                0.0f,
                Height -
                Radius * 2.0f);

        Vector3 top =
            WorldCenter +
            Vector3.up *
            (cylinderHeight * 0.5f);

        Vector3 bottom =
            WorldCenter -
            Vector3.up *
            (cylinderHeight * 0.5f);

        Gizmos.DrawWireSphere(
            top,
            Radius);

        Gizmos.DrawWireSphere(
            bottom,
            Radius);

        Vector3 right =
            Vector3.right *
            Radius;

        Vector3 forward =
            Vector3.forward *
            Radius;

        Gizmos.DrawLine(
            top + right,
            bottom + right);

        Gizmos.DrawLine(
            top - right,
            bottom - right);

        Gizmos.DrawLine(
            top + forward,
            bottom + forward);

        Gizmos.DrawLine(
            top - forward,
            bottom - forward);
    }
}
