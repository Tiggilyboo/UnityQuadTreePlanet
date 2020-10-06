using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlanetGenerator : MonoBehaviour
{
    public GameObject planetObject;
    public CubePlanet planet;
    public int scale;
    public float size;

    // Use this for initialization
    void Start()
    {
        planet = new CubePlanet(planetObject, scale, size);
    }

    // Update is called once per frame
    void Update()
    {
        for(int f = 0; f < 6; f++)
            StartCoroutine(planet.Update(f));
    }
}
