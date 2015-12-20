using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ResourceGenerator : MonoBehaviour
{
    public int MaximumRecources = 2;
    public float ResourceRespawnTime = 1.0f;
    private List<KeyValuePair<Resource, float>> resources;

    private void NewResource(GameObject face)
    {
        var filter = face.GetComponent<MeshFilter>();
        if (filter == null)
            return;

        var mesh = filter.sharedMesh;
        if (mesh == null)
            return;

        // Take 10 attempts to find a normal that is relatively flat (y > x && y > z)
        int vertIndex = -1;
        for(int n = 0; n < 10; n++) {
            vertIndex = Mathf.RoundToInt(Random.value * mesh.vertexCount);

            var normal = mesh.normals[vertIndex];
            if (normal.y > (normal.x + normal.z))
                break;
            else
                vertIndex = -1;
        }
        if (vertIndex == -1)
            return;

        var resourceObject = new GameObject("Resource_" + resources.Count + 1);
        resourceObject.tag = "Resource";
        resourceObject.transform.parent = face.transform;

        // Probably should put this on a face not a vertex...
        resourceObject.transform.position = mesh.vertices[vertIndex];

        var res = new Resource(resourceObject) {
            RespawnTime = ResourceRespawnTime,
        };

        resources.Add(new KeyValuePair<Resource, float>(res, Time.time));
    }

    private IEnumerator SpawnResources()
    {
        // Find all planet meshes
        // TODO needs to be more dynamic naming derived by root object?
        var faces = GameObject.FindGameObjectsWithTag("Planet");
        if (faces.Length == 0)
            yield return null;

        if (resources.Count == faces.Length * MaximumRecources)
            yield return null;

        for (int f = 0; f < faces.Length; f++)
        {
            int numResources = 0;
            foreach (Transform child in faces[f].transform)
                if (child.tag == "Resource")
                    numResources++;

            // Spawn any new resources
            for (int r = numResources; r < MaximumRecources; r++)
            {
                NewResource(faces[f]);
                yield return 1;
            }

            // Check respawn
            for (int r = 0; r < resources.Count; r++)
            {
                if (resources[r].Key.Depleted && resources[r].Value < Time.time)
                {
                    resources.RemoveAt(r);
                    NewResource(faces[f]);
                    yield return 1;
                }
            }

            yield return null;
        }
    }

	// Use this for initialization
	void Start () {
        resources = new List<KeyValuePair<Resource, float>>(MaximumRecources);
    }
	
	// Update is called once per frame
	void Update () {
        StartCoroutine(SpawnResources());
	}
}
