using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Collections.Concurrent;

//This class is in charge of storing all information about our infinite world.
//It also stores settings for the world, as well as tons of helper functions for converting
//between different coordinate systems.

public class World : MonoBehaviour
{
    public static float blockSize = 1f; //Size in units that our voxels should be. Due to the way things are coded right now, this needs to be 1...
    public static bool forceShowChunkEdges = false; //A debug bool that changes whether or not we force our meshing function to render occluded faces if they fall at the edge of chunks.
    public Tree tree; 
    public List<Structure> trees = new List<Structure>();

    //Dictionary storing all of the terrain chunks loaded in this world.
    //Allows us to easily reference chunks by their coord in the infinite grid.
    //Used to reload deloaded chunks without needing to regenerate them and lose player edits.
    public Dictionary<Vector2Int, Chunk> chunks = new Dictionary<Vector2Int, Chunk>();

    float highestValue = float.MinValue;
    float lowestValue = float.MaxValue;

    //There can only ever be one world, so I chose to create a singleton for referencing the World.
    #region Singleton
    public static World instance;
    void Awake()
    {
        if (instance == null)
            instance = this;

        Random.InitState(0);

        for (int i = 0; i < 70; i++)
        {
            trees.Add(tree.GenerateTree());
        }

        float[,] sampler = WorldGenerator.GenerateNoiseMap(1000, 1000, Vector2.zero, 0, 1.232342f, 1, 1, 1);

        for (int x = 0; x < 1000; x++)
        {
            for (int y = 0; y < 1000; y++)
            {
                if (sampler[x, y] < lowestValue)
                    lowestValue = sampler[x, y];

                if (sampler[x, y] > highestValue)
                    highestValue = sampler[x, y];
            }
        }
    }
    #endregion

    //Called when we need to generate new chunks.
    public void UpdateWorldGeneration(Vector2Int pos, int renderDistance)
    {
        Vector2Int updateArea = new Vector2Int(-(Mathf.FloorToInt(renderDistance / 2) + 3), Mathf.FloorToInt(renderDistance / 2) + 3);
        
        for (int y = updateArea.x; y <= updateArea.y; y++)
        {
            for (int x = updateArea.x; x <= updateArea.y; x++)
            {
                Vector2Int coord = new Vector2Int(pos.x + x, pos.y + y);
                
                if (!chunks.ContainsKey(coord))
                {
                    lock (chunks)
                    {
                        chunks.Add(coord, new Chunk());
                    }
                        chunks[coord].position = coord;
                    
                }

                Chunk c = GetChunk(coord);

                if ((int)c.state < 1)
                {
                    c = GenerateChunkHeightmap(coord, c);
                }
            }
        }

        for (int y = updateArea.x; y <= updateArea.y; y++)
        {
            for (int x = updateArea.x; x <= updateArea.y; x++)
            {
                Vector2Int coord = new Vector2Int(pos.x + x, pos.y + y);

                Chunk c = chunks[coord];

                if ((x > updateArea.x + 1 && x < updateArea.y - 1) && (y > updateArea.x + 1 && y < updateArea.y - 1))
                {
                    if ((int)c.state < 2)
                        c = PopulateChunk(coord, c);
                }
            }
        }
    }

    //Given a Vector2Int representing world coords, returns a chunk.
    public Chunk GetChunk(Vector2Int coord)
    {
        return chunks[coord];
    }

    //Helper function for remapping a value in one range, into a value in another range.
    public static float Remap(float value, float from1, float to1, float from2, float to2)
    {
        return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    }

    //Easing function used to smooth out terrain generation.
    public float easeInSin(float x)
    {
        return 1 - Mathf.Cos((x * Mathf.PI) / 2);
    }

    //Function in charge of generating basic heightmap terrain of a new chunk.
    public Chunk GenerateChunkHeightmap(Vector2Int coord, Chunk c)
    {
        //Calls our WorldGenerator class to get a noisemap for this section of terrain.
        //Passing in various settings that control the look of that noise.
        float[,] noise = WorldGenerator.GenerateNoiseMap(16, 16, new Vector2(16 * coord.x, 16 * coord.y), 24, 22.16f*2, 4, .3f, 2f);

        //Loops through every index in this chunk's block data, setting it to the appropriate block.
        for (int x = 0; x < 16; x++)
        {
            for (int y = 0; y < 16; y++)
            {
                float sample = Remap(noise[x,y], highestValue, lowestValue, 0, 1);

                ////Below is some code I'm not using right now that creates a differnt looking terrain.
                //sample = sample * 2 - 1;
                //sample = Mathf.Abs(sample);
                //float surfaceLevel = Mathf.SmoothStep(30, 48, sample);

                float surfaceLevel = Mathf.Lerp(30, 48, easeInSin(sample));

                for (int z = 0; z <= 48; z++)
                {
                    if (z < surfaceLevel)
                    {
                        c.blockData[CoordToBlockIndex(x, y, z)] = 1; //Sets that are beneath our generated surface level to stone.
                    }
                }
            }
        }

        //Loop through all blocks in this chunk's block data again, if we find a stone block that has air above it, set that block to grass.
        for (int x = 0; x < 16; x++)
        {
            for(int y = 0; y < 16; y++)
            {
                for(int z = 0; z <64; z++)
                {
                    if (ChunkLoader.CheckForAirAtIndex(x, y, z + 1, c) && c.blockData[CoordToBlockIndex(x,y,z)] == 1)
                    {
                        c.blockData[CoordToBlockIndex(x, y, z)] = 4;
                    }
                }
            }
        }

        //Set our chunk's state as heightmapped.
        c.state = ChunkState.HEIGHTMAPPED;

        //Return the heightmapped chunk.
        return (c);
    }

    //Later step in chunk generation, spawns structures (right now just trees) in the world.
    //Like everything else, this is done in a seeded fashion, so the same seed results in (mostly) the same tree pattern.
    public Chunk PopulateChunk(Vector2Int coord, Chunk c)
    {
        //Get the tree seed of this given chunk, reed it to our rng.
        //Generate a value between 1 and 0 representing how dense with trees this chunk should be.
        //This is done with a perlin noise map too, giving us a somewhat smooth distribution of trees.
        System.Random rng = new System.Random(PointToSeed(coord));
        float val = WorldGenerator.GenerateNoiseMap(1, 1, coord, 0, 2.342352f, 1, 1, 1)[0, 0];
        float dif = val - 1;

        if(dif > 0)
        {
            val -= dif;
        }

        //Given a value between 0 and 1, decide how many trees to try and place in this chunk.
        //Max of 7, min of 0.
        int numTrees = (int)Mathf.Lerp(0,7,val);

        //Somewhat brute force tree placement. 
        //Foreach tree we want to place, randomly choose a block in this chunk, see if you can fit a tree there.
        //If you can't, try another random spot. Continually set forward the rng. Do this up to 5 times, before giving up.
        for(int i = 0; i < numTrees; i++)
        {
            int attempt = 0;
            bool success = false;
            
            while (!success && attempt < 5)
            {
                attempt++;
                Vector2Int chosenBlock = new Vector2Int(rng.Next(0, 15), rng.Next(0, 15));

                for (int z = 63; z >= 0; z--)
                {
                    int blockCheck = c.blockData[CoordToBlockIndex(chosenBlock.x, chosenBlock.y, z)];

                    if (blockCheck == 4)
                    {
                        success = GenerateStructure(new Vector3Int(chosenBlock.x, chosenBlock.y, z + 1), trees[rng.Next(0,trees.Count-1)], c);
                        break;
                    }
                }
            }
            
        }
        
        //Mark chunk as populated and return.
        c.state = ChunkState.POPULATED;
        return c;
    }

    //Creates a unique int given a chunk coord, used to seed rng in that chunk.
    public int PointToSeed(Vector2Int point)
    {
        if(point.x >= 0)
        {
            point.x *= 2;
        } else
        {
            point.x = (point.x * -2) - 1;
        }

        if (point.y >= 0)
        {
            point.y *= 2;
        }
        else
        {
            point.y = (point.y * -2) - 1;
        }

        return (int)(.5f * (point.x + point.y) * (point.x + point.y + 1) + point.y);
    }

    //Given a root coord (local to the chunk), and a structure, attempt to place that structure on the terrain.
    //If you can't do so, return false. Otherwise, return true.
    public bool GenerateStructure(Vector3Int localRootCoord, Structure s, Chunk c)
    {
        Vector3Int globalRootCoord = LocalCoordToGlobalCoord(localRootCoord, c);

        //Read all blocks in our structure into a dictionary for easy access.
        Dictionary<Vector3Int, int> structureBlocks = new Dictionary<Vector3Int, int>();

        foreach(StructureBlock sb in s.blocks)
        {
            structureBlocks.Add(sb.position, sb.blockType.blockID);
        }

        Vector3 box = s.boundingBoxSize;

        //Looping through each block in the bounding box of the structure we'd like to place, ensure that 
        //there is no overlap between blocks in our structure, and blocks which are allready in the world.
        //Also ensure all blocks are within the vertical bounds of the chunk (VERY IMPORTANT).
        //Return false is any of this fails.
        for (int z = 0; z <= box.z; z++)
        {
            for (int y = -Mathf.FloorToInt(box.y / 2); y <= Mathf.FloorToInt(box.y / 2); y++)
            {
                for (int x = -Mathf.FloorToInt(box.x / 2); x <= Mathf.FloorToInt(box.x / 2); x++)
                {
                    Vector3Int coord = globalRootCoord + new Vector3Int(x, y, z);

                    if (coord.z > 63)
                        return false;

                   
                    int worldBlockAtCoord = GetBlockAtGlobalCoord(coord);
                    int structureBlockAtCoord = 0;
                    if(structureBlocks.ContainsKey(new Vector3Int(x, y, z)))
                    {
                        structureBlockAtCoord = structureBlocks[new Vector3Int(x, y, z)];
                    }

                    if (worldBlockAtCoord > 0 && structureBlockAtCoord > 0)
                        return false;
                }
            }
        }

        for (int z = 0; z <= box.z; z++)
        {
            for (int y = -Mathf.FloorToInt(box.y / 2); y <= Mathf.FloorToInt(box.y / 2); y++)
            {
                for (int x = -Mathf.FloorToInt(box.x / 2); x <= Mathf.FloorToInt(box.x / 2); x++)
                {
                    Vector3Int coord = globalRootCoord + new Vector3Int(x, y, z);
                    int structureBlockAtCoord = 0;
                    if (structureBlocks.ContainsKey(new Vector3Int(x, y, z)))
                    {
                        structureBlockAtCoord = structureBlocks[new Vector3Int(x, y, z)];
                    }

                    if(structureBlockAtCoord != 0)
                        SetBlockAtGlobalCoord(coord, structureBlockAtCoord);

                }
            }
        }

        return true;
    }

    //Given a global grid coordinate and a block id, set that block to the block of the given id.
    public void SetBlockAtGlobalCoord(Vector3Int globalCoord, int blockID)
    {
        Chunk c = GetChunk(GlobalCoordToChunk(globalCoord));
        int index = CoordToBlockIndex(GlobalCoordToLocalCoord(globalCoord));

        c.blockData[index] = blockID;
    }

    /// Everything from here down is helper functions for converting between diferent coordinate systems. ///
    public Vector3Int LocalCoordToGlobalCoord(Vector3Int localCoord, Chunk c)
    {
        return LocalCoordToGlobalCoord(localCoord, c.position);
    }

    public Vector3Int LocalCoordToGlobalCoord(Vector3Int localCoord, Vector2Int chunkCoord)
    {
        Vector3 worldPos = LocalCoordToWorldPos(localCoord, chunkCoord);
        return new Vector3Int(Mathf.FloorToInt(worldPos.x), Mathf.FloorToInt(worldPos.z), -Mathf.FloorToInt(worldPos.y));
    }

    public int GetBlockAtGlobalCoord(Vector3Int globalCoord)
    {
        Chunk c = GetChunk(GlobalCoordToChunk(globalCoord));
        int index = CoordToBlockIndex(GlobalCoordToLocalCoord(globalCoord));
        return c.blockData[index];
    }

    public Vector3Int GlobalCoordToLocalCoord(Vector3Int globalCoord)
    {
        return WorldPosToLocalCoord(GlobalCoordToWorldPos(globalCoord));
    }

    public Vector2Int GlobalCoordToChunk(Vector3Int globalCoord)
    {
        return (WorldPosToChunk(GlobalCoordToWorldPos(globalCoord)));
    }

    public Vector3 GlobalCoordToWorldPos(Vector3Int globalCoord)
    {
        return new Vector3(globalCoord.x + .5f, globalCoord.z - 64 + .5f, globalCoord.y +.5f);
    }

    public Vector2Int WorldPosToChunk(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.RoundToInt(worldPos.x / 16), Mathf.RoundToInt(worldPos.z / 16));
    }

    public Vector3Int WorldPosToGlobalCoord(Vector3 worldPos)
    {
        Vector2Int chunkPos = WorldPosToChunk(worldPos);

        return LocalCoordToGlobalCoord(WorldPosToLocalCoord(worldPos), chunkPos);
    }

    public Vector3Int WorldPosToLocalCoord(Vector3 worldPos)
    {
        Vector2Int chunkAtPoint = new Vector2Int(Mathf.RoundToInt(worldPos.x / 16), Mathf.RoundToInt(worldPos.z / 16));

        Vector3 recenteredPoint = new Vector3(worldPos.x - 16 * chunkAtPoint.x, worldPos.y, worldPos.z - 16 * chunkAtPoint.y);

        Vector3Int blockCoord = new Vector3Int();

        blockCoord.x = Mathf.FloorToInt(recenteredPoint.x + 8);
        blockCoord.y = Mathf.FloorToInt(recenteredPoint.z + 8);
        blockCoord.z = Mathf.FloorToInt(recenteredPoint.y + 64);

        return blockCoord;
    }

    public Vector3 LocalCoordToWorldPos(Vector3Int localCoord, Vector2Int chunkCoord)
    {
        Vector3 chunkWorldPos = new Vector3(chunkCoord.x * 16, 0, chunkCoord.y * 16);
        return new Vector3(chunkWorldPos.x + (localCoord.x - 8), -localCoord.z, chunkWorldPos.z + (localCoord.y - 8)) + Vector3.one / 2;
    }

    public Vector3 LocalCoordToWorldPos(Vector3Int localCoord, Chunk c)
    {
        return LocalCoordToWorldPos(localCoord, c.position);
    }

    public int CoordToBlockIndex(Vector3Int coord)
    {
        return CoordToBlockIndex(coord.x, coord.y, coord.z);
    }

    public int CoordToBlockIndex(int x, int y, int z)
    {
        return ((z * 256) + (y * 16) + x);
    }
}
