# UnityQuadTreePlanet


(Old screenshot)
![Screenshot](https://raw.githubusercontent.com/Tiggilyboo/UnityQuadTreePlanet/master/Screenshot.jpg)

Unity3D Quad-tree planet renderer with terrain generation and LOD levels and perlin noise.

## Planet Generator Attributes

- Scale: Each quad tree leaf's scale, for example 16x16 mesh, input 16.
- Size: The scale of the entire planet. Must be greater than or equal to scale.

## Quad-Tree LOD?

You got here somehow, so I expect you know a little about this. Nonetheless, what this mesh generator does is generate varying levels of details of planet terrain by distance to the camera. When the detail needs to increase when we get closer, we replace the a segment of the planet into 4 (hence quad). Each of these new segments are the same amount of detail as the parent segment. Inversely, if we move away from the segment, we merge the 4 generated segments back into one. 

Since we don't want to recurse into infinity, we check that the distance to the object is smaller than the scale specified in the generator, in most configurations we have a tree of about 3 or 4 deep.

Quad-Tree LOD
![QuadTreeLODScreenshot](
https://raw.githubusercontent.com/Tiggilyboo/UnityQuadTreePlanet/master/QuadtreeScreenshot.jpg)

See left hand side for the mesh tree of the LOD's...

## Terrain Generation Configuration

See CubeFace.cs' noise function, snippet below:

```cs
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
```

This example above uses two overlapping noise generators, one named `planes` which is a low quality smaller larger scale terrain generation (using standard `Perlin`) for the majority of the planet details. The other noise generator is `RidgedMultifractal` which emulates mountains and smaller details.

How do we combine these generators? We use a method called [fractional brownian motion](https://en.wikipedia.org/wiki/Fractional_Brownian_motion) (or fbm for short), as seen in the above noise function, we can alter the lucanarity (altering frequency scale), gain (altering amplitude scale) at a single point in `Perlin`, then take a sample at the point and apply warp & octaves to it giving a final combined noise.

Have fun playing around with the noise generators - It's quite a bit of fun!

