using System.Collections;
using System.Collections.Generic;
using UnityEngine;


//Noise Generation stuff courtesy Sebastian Lague: https://www.youtube.com/watch?v=MRNFcywkUSA&t=614s
public class WorldGenerator : MonoBehaviour
{
    public int width, height;
    public float scale;

    public int octaveCount;
    public float lacunarityValue;
    public float persistanceValue;

    public Vector2 samplePoint;

    public bool banding = false;
    public float threshold = .5f;

    Renderer render;
    Material m = null;
    void Awake()
    {
        render = this.GetComponent<Renderer>();    
    }

    void OnValidate()
    {
        if(m == null)
        {
            m = new Material(Shader.Find("Standard"));
            render.material = m;
        }

        render = this.GetComponent<Renderer>();
        RenderNoiseMap();    
    }

    public void RenderNoiseMap()
    {
        float[,] noise = GenerateNoiseMap(width,height, samplePoint, 24, scale, octaveCount, persistanceValue, lacunarityValue, banding, threshold);

        Texture2D tex = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];

        for (int x = 0; x < width; x++)
        {
            for(int y = 0; y < height; y++)
            {
                
                pixels[y * width + x] = Color.Lerp(Color.black, Color.white, noise[x, y]);
            }
        }

        
        tex.SetPixels(pixels);
        tex.Apply();
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;
        render.material.mainTexture = tex;
    }

    public static float[,] GenerateNoiseMap(int width, int height, Vector2 samplePos, int seed, float scale, int octaves, float persistance, float lacunarity, bool banding = false, float threshold = .5f)
    {
        float[,] noiseMap = new float[width, height];

        float highest = float.MinValue;
        float lowest = float.MaxValue;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float amplitude = 1;
                float frequency = 1;
                float noiseHeight = 0;

                for(int i = 0; i <octaves; i++)
                {
                    float xPoint = (x + samplePos.x) / scale * frequency + seed * 5000;
                    float yPoint = (y + samplePos.y) / scale * frequency + seed * 5000;

                    float perlinSample = Mathf.PerlinNoise(xPoint , yPoint);
                    noiseHeight += perlinSample * amplitude;

                    amplitude *= persistance;
                    frequency *= lacunarity;
                }

                if (banding)
                {
                    noiseHeight -= threshold;
                    noiseHeight = Remap(noiseHeight, -threshold, 1 - threshold, 0, 1);

                    Mathf.SmoothStep(0, 1, noiseHeight);
                }

                if (noiseHeight > highest)
                    highest = noiseHeight;

                if (noiseHeight < lowest)
                    lowest = noiseHeight;

                noiseMap[x, y] = noiseHeight;

            }
        }

        

        return noiseMap;
    }

    public static float Remap(float value, float from1, float to1, float from2, float to2)
    {
        return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    }

}
