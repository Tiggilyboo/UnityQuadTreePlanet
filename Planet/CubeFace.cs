using UnityEngine;
using System.Collections;

/// <summary>
/// A single plane mesh, rendered with accompanying collider and renderer
/// Contains references to a parent (if any), and 0 or 4 children (Quadtree nodes)
/// </summary>
public class CubeFace
{
    public CubeFace parent;
    public CubeFace[] tree;
    public bool[] neighbours;

    public Material material { get; private set; }
    public Mesh mesh { get; private set; }

    public MeshFilter filter { get; private set; }
    public MeshRenderer renderer { get; private set; }
    public MeshCollider collider { get; private set; }
    public GameObject gameObject { get; private set; }

    private Vector3 position, left, forward;
    public int size { get; private set; }
    public float scale { get; private set; }
    public bool generated = false;
    public bool finalized = false;
    public bool textured = false;

    /// Initialize a sub-Cubeface with a parent referece
    public CubeFace(CubeFace parent, Vector3 pos, Vector3 left, Vector3 forward, float scale, int size)
    {
        Initialize(parent, pos, left, forward, scale, size);
    }

    // Initialize a parent Cubeface with nulled parent reference
    public CubeFace(Vector3 pos, Vector3 left, Vector3 forward, float scale, int size)
    {
        Initialize(null, pos, left, forward, scale, size);
    }

    /// <summary> Creates a CubeFace </summary>
    /// <param name="parent">Associated parent reference as a QuadTree sibling</param>
    /// <param name="pos">Position vector to render the cubeface at</param>
    /// <param name="left">The x axis of the plane</param>
    /// <param name="forward">The z axis of the plane</param>
    /// <param name="scale">The scale of the plane</param>
    /// <param name="size">The number of vertices in length/width to create (segments)</param>
    private void Initialize(CubeFace parent, Vector3 pos, Vector3 left, Vector3 forward, float scale, int size)
    {
        this.parent = parent;
        this.size = size;
        this.scale = scale;
        this.position = pos;
        this.left = left;
        this.forward = forward;

        // Centre the plane!
        position -= left * (scale / 2);
        position -= forward * (scale / 2);

        gameObject = new GameObject("CubePlane_" + scale + "_" + size + "_" + pos.ToString());
        gameObject.isStatic = true;
        gameObject.layer = 8; // To Raycast neighbours

        material = new Material(Shader.Find("Standard"));
        filter = gameObject.AddComponent<MeshFilter>();
        renderer = gameObject.AddComponent<MeshRenderer>();
        collider = gameObject.AddComponent<MeshCollider>();


        // Ensure hierarchy
        if (parent != null)
            gameObject.transform.parent = parent.gameObject.transform;

        Generate();
    }

    /// <summary>
    /// Used to first generate the plane's verts, uvs and tris
    /// If already generated, turns on this CubeFace's renderer and collider (makes it active on the planet)
    /// </summary>
    public void Generate()
    {
        if (generated)
        {
            renderer.enabled = true;
            if (parent != null)
                parent.collider.enabled = false;
            collider.enabled = true;
            return;
        }

        mesh = new Mesh();
        mesh.name = "Mesh_" + gameObject.name;

        var verts = new Vector3[(size + 1) * (size + 1)];
        var tris = new int[size * size * 6];

        var uvs = new Vector2[(size + 1) * (size + 1)];
        var uvFactor = 1.0f / size;

        // Calculate verts & uv coords for cube face
        for (int z = 0, i = 0; z <= size; z++)
        {
            for (int x = 0; x <= size; x++, i++)
            {
                var px = (float)x / size;
                var pz = (float)z / size;
                var vx = left * px * scale;
                var vz = forward * pz * scale;

                uvs[i] = new Vector2(x * uvFactor, z * uvFactor);
                verts[i] = position + vx + vz;
            }
        }

        mesh.vertices = verts;
        mesh.uv = uvs;

        // Calculate tris
        for (int ti = 0, vi = 0, y = 0; y < size; y++, vi++)
        {
            for (int x = 0; x < size; x++, ti += 6, vi++)
            {
                tris[ti] = vi;
                tris[ti + 3] = tris[ti + 2] = vi + 1;
                tris[ti + 4] = tris[ti + 1] = vi + size + 1;
                tris[ti + 5] = vi + size + 2;
            }
        }
        mesh.triangles = tris;

        if (parent != null)
            parent.Dispose();

        generated = true;
    }

    /// <summary>
    /// Must be called after mesh.RecalculateNormals (needs normal values)
    /// Generates normal map and applies grayscale texture to CubeFace's material
    /// </summary>
    public void Texturize()
    {
        if (textured)
            return;

        // Create normal map from mesh normals
        var normalMap = new Texture2D(size + 1, size + 1);
        var mainTex = new Texture2D(size + 1, size + 1);

        for (int y = 0, n = 0; y <= size; y++)
        {
            for (int x = 0; x <= size; x++, n++)
            {
                var v = mesh.normals[n].x + mesh.normals[n].y + mesh.normals[n].z / 3.0f;
                normalMap.SetPixel(x, y, new Color(mesh.normals[n].x, mesh.normals[n].y, mesh.normals[n].z));
                mainTex.SetPixel(x, y, new Color(v, v, v));
            }
        }

        material.SetTexture("_MainTex", mainTex);
        material.SetTexture("_NormalMap", normalMap);
        material.SetFloat("_Glossiness", 0.1f);
        material.SetFloat("_BumpScale", 1.0f);
        material.color = Color.gray;

        // Mesh complete, set render/physics objects
        renderer.material = material;   // If we keep the same material use sharedMaterial for batching

        textured = true;
    }

    /// <summary>
    /// Quadtree merge of any children
    /// </summary>
    public void Merge()
    {
        if (tree == null)
            return;

        for (int t = 0; t < tree.Length; t++)
        {
            tree[t].Merge();
            tree[t].Dispose();
        }

        tree = null;
    }

    public void Dispose()
    {
        renderer.enabled = false;
        generated = false;
    }

    /// <summary>
    /// Quadtree subdivide
    /// Creates 4 sub-CubeFaces taking same space as current mesh, doubling the detail.
    /// </summary>
    public void SubDivide()
    {
        var subPos = position;
        subPos += left * (scale / 2);
        subPos += forward * (scale / 2);

        var stepLeft = (left * scale / 4);
        var stepForward = (forward * scale / 4);
        var hs = scale / 2;

        tree = new CubeFace[] {
                new CubeFace(this, subPos - stepLeft + stepForward, left, forward, hs, size),
                new CubeFace(this, subPos + stepLeft + stepForward, left, forward, hs, size),
                new CubeFace(this, subPos - stepLeft - stepForward, left, forward, hs, size),
                new CubeFace(this, subPos + stepLeft - stepForward, left, forward, hs, size)
            };

        Dispose();
    }
}
