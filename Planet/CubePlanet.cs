using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 
/// </summary>
public class CubePlanet
{
    static Vector3[] directions = new Vector3[] {
            Vector3.left,
            Vector3.forward,
            Vector3.right,
            Vector3.back
        };

    GameObject planet;
    CubeFace[] faces;

    private int size;
    private float scale;
    private LibNoise.Generator.Perlin planes = new LibNoise.Generator.Perlin(0.03, 2.7, 0.5, 6, 1337, LibNoise.QualityMode.Low);
    private LibNoise.Generator.RidgedMultifractal mountains = new LibNoise.Generator.RidgedMultifractal(0.003, 6.5, 2, 1337, LibNoise.QualityMode.Medium);

    private float noise(Vector3 point, int octaves,
        float lucanarity = 2.0f, float gain = 0.5f, float warp = 0.25f)
    {
        float sum = 0.0f, freq = 1.0f, amp = 1.0f;

        for (int o = 0; o < octaves; o++)
        {
            sum += amp * (float)planes.GetValue(point);
            freq *= lucanarity;
            amp *= gain;
        }

        sum *= (float)mountains.GetValue(point * freq) * warp * octaves;

        return sum;
    }

    /// <summary>
    /// Creates a Planet
    ///     using 6 planes normalized around the origin
    /// </summary>
    /// <param name="name">The planet name (duh)</param>
    /// <param name="size">Number of vertices in length and width for each CubeFace</param>
    /// <param name="scale">The scale of the planet (radius)</param>
    public CubePlanet(string name, int size, float scale)
    {
        this.size = size;
        this.scale = scale;

        var hs = scale / 2;
        faces = new CubeFace[]
        {
                new CubeFace(new Vector3(0,0,-hs), new Vector3(-1,0,0), new Vector3(0,-1,0), scale, size),    // front
                new CubeFace(new Vector3(0,0, hs), new Vector3(1,0,0), new Vector3(0,-1,0), scale, size),     // back

                new CubeFace(new Vector3(-hs,0, 0), new Vector3(0,0,1), new Vector3(0,-1,0), scale, size),    // left
                new CubeFace(new Vector3(hs,0, 0), new Vector3(0,0,-1), new Vector3(0,-1,0), scale, size),    // right

                new CubeFace(new Vector3(0,hs,0), new Vector3(0,0,1), new Vector3(-1,0,0), scale, size),      // top
                new CubeFace(new Vector3(0,-hs,0), new Vector3(0,0,-1), new Vector3(-1,0,0), scale, size),    // bottom
        };

        // Chuck it into a single game object for neatness
        planet = new GameObject(name);
        for (int f = 0; f < faces.Length; f++)
            faces[f].gameObject.transform.parent = planet.transform;
    }

    // Weld face verts with neighbours verts (no distance calculation needed)
    private void WeldMeshes(CubeFace face, Mesh neighbour, int neighbourDir)
    {
        int xBegin = 0, xEnd = 0, zBegin = 0, zEnd = 0;
        var verts = face.mesh.vertices;
        var norms = face.mesh.normals;
        if (norms.Length == 0)
        {

            face.mesh.RecalculateNormals();
            norms = face.mesh.normals;
            Debug.Assert(norms.Length == 0);
        }

        var nverts = neighbour.vertices;
        var nnorms = neighbour.normals;
        if (nnorms.Length == 0)
        {
            neighbour.RecalculateNormals();
            nnorms = neighbour.normals;
            Debug.Assert(nnorms.Length == 0);
        }

        switch (neighbourDir)
        {
            case 0:         // Left
                xEnd = xBegin = 0;
                zBegin = 0;
                zEnd = size + 1;
                break;
            case 1:         // Forward
                xBegin = 0;
                xEnd = size + 1;
                zBegin = zEnd = 0;
                break;
            case 2:         // Right
                xEnd = xBegin = size + 1;
                zBegin = 0;
                zEnd = size + 1;
                break;
            case 3:         // Back
                xBegin = 0;
                xEnd = size + 1;
                zBegin = zEnd = size + 1;
                break;
        }

        for (int z = zBegin; z < zEnd; z++)
        {
            for (int x = 0; x <= size; x++)
            {
                var i = z * (size + 1) + x;
                norms[i] = Vector3.Cross(verts[i], nverts[i]).normalized;
                verts[i] = nverts[i];
            }
        }

        face.mesh.vertices = verts;
        face.mesh.normals = norms;
    }

    private void ProcessNeighbours(CubeFace face)
    {
        if (face.neighbours == null)
            face.neighbours = new bool[directions.Length];

        for (int n = 0; n < directions.Length; n++)
        {
            if (face.neighbours[n])
                continue;

            RaycastHit hit;
            if (Physics.Raycast(face.mesh.bounds.center, directions[n], out hit, face.scale, 1 << 8))
            {
                if (hit.transform.gameObject.tag == "Planet")
                {
                    face.neighbours[n] = hit.transform.gameObject;

                    var filter = hit.transform.gameObject.GetComponent<MeshFilter>();
                    if (filter == null) continue;
                    var mesh = filter.sharedMesh;
                    if (mesh == null) continue;

                    WeldMeshes(face, mesh, n);
                }
            }
        }

        face.Texturize();
    }

    /// <summary>
    /// Turns a plane's vertices into a segment of a sphere
    ///  - Processes neighbour faces in order to remove gaps/tears
    /// </summary>
    /// <param name="face">The face in question (aint it an ugly one?)</param>
    private void FinalizeFace(CubeFace face)
    {
        if (face.finalized)
            return;

        var verts = face.mesh.vertices;

        for (int i = 0; i < verts.Length; i++)
            verts[i] = verts[i].normalized * (scale + noise(verts[i], 2, 1.7f, 0.1f, scale / size));

        face.mesh.vertices = verts;

        face.mesh.RecalculateNormals();
        face.mesh.RecalculateBounds();

        ProcessNeighbours(face);

        face.filter.sharedMesh = face.mesh;
        face.collider.sharedMesh = face.mesh;

        face.finalized = true;
    }

    /// <summary>
    /// Accumulates recursively all faces that have been created in a planet face
    /// </summary>
    /// <param name="face">1 of 6 sides of the planet to recurse into</param>
    /// <param name="active">The recursive bag to collect into</param>
    private void GetActiveQuadTreeFaces(CubeFace face, ref List<CubeFace> active)
    {
        if (face.tree == null)
            active.Add(face);
        else
            for (int f = 0; f < face.tree.Length; f++)
                GetActiveQuadTreeFaces(face.tree[f], ref active);
    }

    /// <summary>
    /// The coroutine planet update process
    /// Keeps track of LOD according to player distance to each CubeFace
    /// Processes a single face per call, subdivision delays coroutine for smoothing.
    /// </summary>
    /// <param name="f">The face index of the cube [0, 5]</param>
    /// <returns></returns>
    public IEnumerator Update(int f)
    {
        var player = GameObject.Find("Cube");
        if (player == null)
            yield break;

        var activeTree = new List<CubeFace>();
        GetActiveQuadTreeFaces(faces[f], ref activeTree);

        for (int a = 0; a < activeTree.Count; a++)
        {
            var dist = Vector3.Distance(player.transform.position, activeTree[a].mesh.bounds.ClosestPoint(player.transform.position));

            if (dist > activeTree[a].scale)
            {
                if (dist <= scale * 3.0f || activeTree[a].parent == null)
                {
                    activeTree[a].Merge();
                    activeTree[a].Generate();
                    FinalizeFace(activeTree[a]);
                }
                else
                {
                    activeTree[a].Merge();
                    activeTree[a].parent.Merge();
                    activeTree[a].parent.Generate();
                    FinalizeFace(activeTree[a].parent);
                }
            }
            else if (activeTree[a].scale > size)
            {
                activeTree[a].SubDivide();
            }
            else // Highest detail, tag object to place resources and activate pathfinding
            {
                if (activeTree[a].gameObject.tag != "Planet")
                    activeTree[a].gameObject.tag = "Planet";
            }
        }
    }
}