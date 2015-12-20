using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlanetGenerator : MonoBehaviour
{
    
    

    CubePlanet planet;

    // Use this for initialization
    void Start()
    {
        planet = new CubePlanet("Planet of Mistaken Delivery", 32, 512.0f);
    }

    // Update is called once per frame
    void Update()
    {
        for(int f = 0; f < 6; f++)
            StartCoroutine(planet.Update(f));

        
    }
}
