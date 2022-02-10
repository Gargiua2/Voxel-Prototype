    using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;

//Class in charge of creating physical chunks. 
//Mainly requests the generation of chunk block data, which it uses to generate chunk meshes.
//Chunks are generated in a multithreaded fashion to avoid slowdown/stutter as the player loads new chunks.

public class ChunkLoader : MonoBehaviour
{
    public int renderDistance = 15; //Must be an odd number, the area in chunks around the player we wish to render at a given time.
    public GameObject indicator; //Object that the player's selecte block.
    public Material common; //Material used by all chunks.
    TextureHandler texHandler; //Script that manages the texture atlas we use to texture chunks.

    public Vector2Int currentChunk; //Chunk the player is in.
    ConcurrentDictionary<Vector2Int,Chunk> loadedChunks = new ConcurrentDictionary<Vector2Int, Chunk>(); //Threadsafe dicitonary containing all of our currently loaded chunks.

    //Queue of ChunkMeshData structs waiting to loaded onto gameobjects.
    Queue<ChunkMeshData> meshDataQueue = new Queue<ChunkMeshData>();

    //Lists of the chunks we wish to load, and chunks that are actively being loaded on another thread currently.
    List<Vector2Int> desiredLoadedChunks = new List<Vector2Int>();
    List<Vector2Int> activelyLoadingChunks = new List<Vector2Int>();

    //Queue of chunks that we would like to unload.
    Queue<Chunk> chunksToUnload = new Queue<Chunk>();

    //Bool used to check if we need to update chunks.
    bool chunkUpdateRequired = true;
    bool currentlyResolvingLoadedChunkUpdate = false;
    //An object pool used to recycle chunk game objects.
    ChunkPool pool;

    //Simply ensures the render distance the user sets is odd.
    void OnValidate()
    {
        if(renderDistance % 2 == 0)
        {
            renderDistance++;
        }    
    }


    //Initialization. 
    void Start()
    {
        texHandler = GetComponent<TextureHandler>();

        pool = new ChunkPool(renderDistance * renderDistance + renderDistance * 3, common);

        UpdateLoadedChunks(true); //Initial generation step. This initial generation is not done on a seperate thread.
    }


    Vector2Int pCurrentChunk; //Previous chunk the player was in.
    void Update()
    {
        //Debugging key.
        if (Input.GetKeyDown(KeyCode.L))
        {
            for(int i = 0; i <loadedChunks.Count; i++)
            {
                var c = loadedChunks.ElementAt(i);
                Destroy(loadedChunks[c.Key].obj.obj);
            }
        }

        //See if we changed chunks between frames.
        CalculateCurrentChunk();
        if(pCurrentChunk != currentChunk)
        {
            chunkUpdateRequired = true;
        }
        pCurrentChunk = currentChunk;
        

        //This code is awkward, that's becasue it was changed a few times for performance
        //Currently, every frame, if we have chunk mesh data that needs to be applied to an object
        //we do so, but only 1 chunk per frame. This helps stop sudden lag spikes when several chunks 
        //need to be loaded in a frame.
        for(int i = 0; i < meshDataQueue.Count; i++)
        {

            if (i > 0)
                break;
            lock (meshDataQueue)
            {
                ChunkMeshData meshData = meshDataQueue.Dequeue();
                LoadChunkMesh(meshData);
            }
        }

        //Unload all chunks in our list of chunks to unload.
        //This has historically caused problems, so I have a stopwatch setup to track these times.
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();
        for (int i = 0; i < chunksToUnload.Count; i++)
        {
            lock (chunksToUnload)
            {
                
                Chunk toUnload = chunksToUnload.Dequeue();
                    UnloadChunk(toUnload);
            }
        }
        sw.Stop();
        Debug.Log(sw.ElapsedMilliseconds);

        //If we ened to update loaded chunks, do so!
        if (chunkUpdateRequired)
        {
            chunkUpdateRequired = false;
            UpdateLoadedChunks();
        }

    }

    //Called when the player edits a chunk by placing or destroying a block. 
    //Essentially, given the point the player clicked on, we find the corresponding chunk, block, and block index.
    //We replace the block at that index with the appropriate blockID, then remesh the chunk.
    public void UpdateChunk(Vector3 point, int replacementBlockID)
    {
        Vector2Int chunkAtPoint = new Vector2Int(Mathf.RoundToInt(point.x / 16), Mathf.RoundToInt(point.z / 16));

        Chunk target = loadedChunks[chunkAtPoint];

        Vector3 recenteredPoint = new Vector3(point.x - 16 * chunkAtPoint.x, point.y, point.z - 16 * chunkAtPoint.y);

        Vector3Int blockCoord = new Vector3Int();

        blockCoord.x = Mathf.FloorToInt(recenteredPoint.x + 8);
        blockCoord.y = Mathf.FloorToInt(recenteredPoint.z + 8);
        blockCoord.z = Mathf.FloorToInt(recenteredPoint.y + 64);

        int index = CoordToBlockIndex(blockCoord.x, blockCoord.y, blockCoord.z);

        target.blockData[index] = replacementBlockID;

        UnloadChunk(target);
        
        LoadChunkMesh(MeshChunk(target));

        if(blockCoord.x == 0)
        {
            ReloadChunk(new Vector2Int(chunkAtPoint.x - 1, chunkAtPoint.y));
        } else if(blockCoord.x == 15)
        {
            ReloadChunk(new Vector2Int(chunkAtPoint.x + 1, chunkAtPoint.y));
        }

        if (blockCoord.y == 0)
        {
            ReloadChunk(new Vector2Int(chunkAtPoint.x, chunkAtPoint.y - 1));
        }
        else if (blockCoord.y == 15)
        {
            ReloadChunk(new Vector2Int(chunkAtPoint.x, chunkAtPoint.y + 1));
        }
    }

    //Handles reloading a chunk when it's block data is edited.
    public void ReloadChunk(Vector2Int position)
    {
        Chunk toReload = loadedChunks[position];

        UnloadChunk(toReload);
        
        LoadChunkMesh(MeshChunk(toReload));
    }

    //Called when we want to generate a chunk. 
    //Starts a new thread for generating each chunk.
    public void RequestChunkMeshData(Chunk c)
    {
        ThreadStart threadStart = delegate
        {
            ChunkMeshDataThread(c);
        };

        new Thread(threadStart).Start();
    }

    //Called on a new thread when generating a new chunk. Generates a chunk, gets 
    //the mesh data for that chunk, and adds that data the meshDataQueue, to be loaded
    //onto a gameobject on the main thread.
    void ChunkMeshDataThread(Chunk c)
    {
        lock (meshDataQueue)
        {
            ChunkMeshData meshData = MeshChunk(c);
            meshDataQueue.Enqueue(meshData);
        }
        
    }
    
    //Called when the main thread recieves new mesh data. 
    //Given ChunkMeshData, it grabs a gameObject from our chunk pool,
    //applies the meshData to its mesh, and positions it in the world
    public void LoadChunkMesh(ChunkMeshData meshData)
    {
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        if (!desiredLoadedChunks.Contains(meshData.c.position))
        {
            if (activelyLoadingChunks.Contains(meshData.c.position))
            {
                lock (desiredLoadedChunks)
                {
                    activelyLoadingChunks.Remove(meshData.c.position);
                }
            }
            
            return;
        }


        ChunkObject chunk;
        lock (pool)
        {
            chunk = pool.Draw();
        }
        
        GameObject chunkGO = chunk.obj;
        MeshCollider collider = chunk.collider;
        MeshFilter filter = chunk.filter;
        meshData.c.obj = chunk;

        Vector3[] vertices = meshData.vertices;
        int[] triangles = meshData.triangles;
        Vector2[] uv = meshData.uvs;

        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;

        filter.mesh = mesh;
        collider.sharedMesh = mesh;


        chunkGO.transform.position = new Vector3(meshData.c.position.x * 16, 0, meshData.c.position.y * 16);
        
        lock (loadedChunks)
        {
            loadedChunks.TryAdd(meshData.c.position, meshData.c);
        }
        
        if (activelyLoadingChunks.Contains(meshData.c.position))
            activelyLoadingChunks.Remove(meshData.c.position);

        sw.Stop();
    }

    //Arguably the main method of this class.
    //Given a chunk with a coord, it requrest the data of that chunk.
    //It then generates the mesh data for that chunk using the generated block data.
    //All of this is done on a seperate thread.
    public ChunkMeshData MeshChunk(Chunk c)
    {

        List<Vector3> verts = new List<Vector3>();
        List<int> tris = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        Chunk[] neighbors = new Chunk[4];

        lock (World.instance.chunks)
        {
            neighbors[0] = World.instance.GetChunk(new Vector2Int(c.position.x - 1, c.position.y));
            neighbors[1] = World.instance.GetChunk(new Vector2Int(c.position.x + 1, c.position.y));
            neighbors[2] = World.instance.GetChunk(new Vector2Int(c.position.x, c.position.y - 1));
            neighbors[3] = World.instance.GetChunk(new Vector2Int(c.position.x, c.position.y + 1));
        }

        for (int z = 0; z < 64; z++)
        {
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    int blockIndex = CoordToBlockIndex(x, y, z);
                    int blockID = c.blockData[blockIndex];

                    if (c.blockData[blockIndex] > 0)
                    {
                        bool back = CheckForAirAtIndex(x, y - 1, z, c, neighbors);
                        bool front = CheckForAirAtIndex(x, y + 1, z, c, neighbors);
                        bool left = CheckForAirAtIndex(x - 1, y, z, c, neighbors);
                        bool right = CheckForAirAtIndex(x + 1, y, z, c, neighbors);
                        bool top = CheckForAirAtIndex(x, y, z - 1, c, neighbors);
                        bool bottom = CheckForAirAtIndex(x, y, z + 1, c, neighbors);

                        if (top)
                        {
                            verts.Add(new Vector3(-8 * World.blockSize + x * World.blockSize, -64 * World.blockSize + z * World.blockSize, -8 * World.blockSize + y * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + x * World.blockSize, -64 * World.blockSize + z * World.blockSize, -8 * World.blockSize + (y + 1) * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + (x + 1) * World.blockSize, -64 * World.blockSize + z * World.blockSize, -8 * World.blockSize + y * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + (x + 1) * World.blockSize, -64 * World.blockSize + z * World.blockSize, -8 * World.blockSize + (y + 1) * World.blockSize));

                            List<Vector2> uvList = texHandler.GetUVSForTexCoord(BlockManager.blocks[blockID].topTextureCoord);

                            uvs.Add(uvList[0]);
                            uvs.Add(uvList[1]);
                            uvs.Add(uvList[2]);
                            uvs.Add(uvList[3]);

                            tris.Add(verts.Count - 4);
                            tris.Add(verts.Count - 1);
                            tris.Add(verts.Count - 3);
                            tris.Add(verts.Count - 1);
                            tris.Add(verts.Count - 4);
                            tris.Add(verts.Count - 2);

                        }

                        if (bottom)
                        {
                            verts.Add(new Vector3(-8 * World.blockSize + x * World.blockSize, -64 * World.blockSize + (z + 1) * World.blockSize, -8 * World.blockSize + y * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + x * World.blockSize, -64 * World.blockSize + (z + 1) * World.blockSize, -8 * World.blockSize + (y + 1) * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + (x + 1) * World.blockSize, -64 * World.blockSize + (z + 1) * World.blockSize, -8 * World.blockSize + y * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + (x + 1) * World.blockSize, -64 * World.blockSize + (z + 1) * World.blockSize, -8 * World.blockSize + (y + 1) * World.blockSize));

                            List<Vector2> uvList = texHandler.GetUVSForTexCoord(BlockManager.blocks[blockID].bottomTextureCoord);

                            uvs.Add(uvList[0]);
                            uvs.Add(uvList[1]);
                            uvs.Add(uvList[2]);
                            uvs.Add(uvList[3]);

                            tris.Add(verts.Count - 3);
                            tris.Add(verts.Count - 1);
                            tris.Add(verts.Count - 4);
                            tris.Add(verts.Count - 2);
                            tris.Add(verts.Count - 4);
                            tris.Add(verts.Count - 1);
                        }

                        if (front)
                        {
                            verts.Add(new Vector3(-8 * World.blockSize + x * World.blockSize, -64 * World.blockSize + z * World.blockSize, -8 * World.blockSize + (y + 1) * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + x * World.blockSize, -64 * World.blockSize + (z + 1) * World.blockSize, -8 * World.blockSize + (y + 1) * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + (x + 1) * World.blockSize, -64 * World.blockSize + z * World.blockSize, -8 * World.blockSize + (y + 1) * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + (x + 1) * World.blockSize, -64 * World.blockSize + (z + 1) * World.blockSize, -8 * World.blockSize + (y + 1) * World.blockSize));

                            List<Vector2> uvList = texHandler.GetUVSForTexCoord(BlockManager.blocks[blockID].frontTextureCoord);

                            uvs.Add(uvList[0]);
                            uvs.Add(uvList[1]);
                            uvs.Add(uvList[2]);
                            uvs.Add(uvList[3]);

                            tris.Add(verts.Count - 4);
                            tris.Add(verts.Count - 1);
                            tris.Add(verts.Count - 3);
                            tris.Add(verts.Count - 1);
                            tris.Add(verts.Count - 4);
                            tris.Add(verts.Count - 2);
                        }

                        if (back)
                        {
                            verts.Add(new Vector3(-8 * World.blockSize + x * World.blockSize, -64 * World.blockSize + z * World.blockSize, -8 * World.blockSize + (y) * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + x * World.blockSize, -64 * World.blockSize + (z + 1) * World.blockSize, -8 * World.blockSize + (y) * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + (x + 1) * World.blockSize, -64 * World.blockSize + z * World.blockSize, -8 * World.blockSize + (y) * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + (x + 1) * World.blockSize, -64 * World.blockSize + (z + 1) * World.blockSize, -8 * World.blockSize + (y) * World.blockSize));

                            List<Vector2> uvList = texHandler.GetUVSForTexCoord(BlockManager.blocks[blockID].backTextureCoord);

                            uvs.Add(uvList[0]);
                            uvs.Add(uvList[1]);
                            uvs.Add(uvList[2]);
                            uvs.Add(uvList[3]);

                            tris.Add(verts.Count - 3);
                            tris.Add(verts.Count - 1);
                            tris.Add(verts.Count - 4);
                            tris.Add(verts.Count - 2);
                            tris.Add(verts.Count - 4);
                            tris.Add(verts.Count - 1);
                        }

                        if (left)
                        {
                            verts.Add(new Vector3(-8 * World.blockSize + x * World.blockSize, -64 * World.blockSize + z * World.blockSize, -8 * World.blockSize + y * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + x * World.blockSize, -64 * World.blockSize + (z + 1) * World.blockSize, -8 * World.blockSize + y * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + x * World.blockSize, -64 * World.blockSize + z * World.blockSize, -8 * World.blockSize + (y + 1) * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + x * World.blockSize, -64 * World.blockSize + (z + 1) * World.blockSize, -8 * World.blockSize + (y + 1) * World.blockSize));

                            List<Vector2> uvList = texHandler.GetUVSForTexCoord(BlockManager.blocks[blockID].leftTextureCoord);

                            uvs.Add(uvList[0]);
                            uvs.Add(uvList[1]);
                            uvs.Add(uvList[2]);
                            uvs.Add(uvList[3]);

                            tris.Add(verts.Count - 4);
                            tris.Add(verts.Count - 1);
                            tris.Add(verts.Count - 3);

                            tris.Add(verts.Count - 1);
                            tris.Add(verts.Count - 4);
                            tris.Add(verts.Count - 2);
                        }

                        if (right)
                        {
                            verts.Add(new Vector3(-8 * World.blockSize + (x + 1) * World.blockSize, -64 * World.blockSize + z * World.blockSize, -8 * World.blockSize + y * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + (x + 1) * World.blockSize, -64 * World.blockSize + (z + 1) * World.blockSize, -8 * World.blockSize + y * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + (x + 1) * World.blockSize, -64 * World.blockSize + z * World.blockSize, -8 * World.blockSize + (y + 1) * World.blockSize));
                            verts.Add(new Vector3(-8 * World.blockSize + (x + 1) * World.blockSize, -64 * World.blockSize + (z + 1) * World.blockSize, -8 * World.blockSize + (y + 1) * World.blockSize));

                            List<Vector2> uvList = texHandler.GetUVSForTexCoord(BlockManager.blocks[blockID].rightTextureCoord);

                            uvs.Add(uvList[0]);
                            uvs.Add(uvList[1]);
                            uvs.Add(uvList[2]);
                            uvs.Add(uvList[3]);

                            tris.Add(verts.Count - 3);
                            tris.Add(verts.Count - 1);
                            tris.Add(verts.Count - 4);
                            tris.Add(verts.Count - 2);
                            tris.Add(verts.Count - 4);
                            tris.Add(verts.Count - 1);
                        }

                    }
                }
            }
        }

        return new ChunkMeshData(verts, tris, uvs, c);
    }

    //Called when a chunk is unloaded. Removes the chunk from our loadedChunks,
    //returns its gameObject to the pool.
    void UnloadChunk(Chunk c)
    {
        loadedChunks.TryRemove(c.position, out c);

        lock (pool)
        {
            pool.Receive(c.obj);
        }
        
    }

    //Calculates teh current chunk based on the position of this object.
    //(This script is attatched to the player)
    void CalculateCurrentChunk()
    {
        currentChunk = new Vector2Int(Mathf.RoundToInt(transform.position.x/16), Mathf.RoundToInt(transform.position.z / 16));
    }

    //Called when the game starts, or we step over a chunk boundary. 
    //Cues the World script to load new chunks if needed, then 
    //figures out what chunks need to have their meshses loaded. 
    //Starts the process of generating chunk meshes on a new thread.
    //Also unloads uneeded chunks.
    void UpdateLoadedChunks(bool forceHardLoad = false)
    {
        currentlyResolvingLoadedChunkUpdate = true;

        //Update world generation for newly loaded/loading chunks.
        World.instance.UpdateWorldGeneration(currentChunk, renderDistance);

        //Create a list of which chunks we want to be loaded right now.
        desiredLoadedChunks = new List<Vector2Int>();
        for (int x = Mathf.FloorToInt(-renderDistance/2); x <= Mathf.FloorToInt(renderDistance/2); x++)
        {
            for (int y = Mathf.FloorToInt(-renderDistance / 2); y <= Mathf.FloorToInt(renderDistance / 2); y++)
            {
                desiredLoadedChunks.Add(new Vector2Int(currentChunk.x + x, currentChunk.y + y));
            }
        }

        //Read the positions of those chunks which we currently have loaded into a list.
        List<Vector2Int> loadedChunkPositions = new List<Vector2Int>();
        
        foreach(Vector2Int k in loadedChunks.Keys)
        {
            loadedChunkPositions.Add(k);
        }

        //Create a list of chunks we don't want loaded by comparing our list of currently loaded chunks to our list of desired chunks.
        
        chunksToUnload = new Queue<Chunk>();
        
        foreach(Vector2Int k in loadedChunks.Keys) {
            if (!desiredLoadedChunks.Contains(k)){
                
                lock (chunksToUnload)
                {
                    chunksToUnload.Enqueue(loadedChunks[k]);
                }
                
            }
        }

        //Create a list of chunks that we want to load by comparing our list of desired chunks to our currently loaded and actively loading chunks.
        List<Vector2Int> chunksToLoad = new List<Vector2Int>();
        foreach (Vector2Int pos in desiredLoadedChunks)
        {
            if (!loadedChunkPositions.Contains(pos) && !activelyLoadingChunks.Contains(pos))
            { 
                chunksToLoad.Add(pos);
            }
        }

        //If hard loading is true, we load all of the mesh data of our chunks to load on this thread.
        //If hard loading false, we send a request to load the mesh data of all chunks to load, and they are managed on their own threads.
        if (!forceHardLoad)
        {
            foreach (Vector2Int pos in chunksToLoad)
            {
                activelyLoadingChunks.Add(pos);
                RequestChunkMeshData(World.instance.GetChunk(pos));
            }
        } else
        {
            foreach (Vector2Int pos in chunksToLoad)
            {
                LoadChunkMesh(MeshChunk(World.instance.GetChunk(pos)));
            }
        }

        currentlyResolvingLoadedChunkUpdate = false;
    }

    //Given an x/y/z local chunk coord, returns the block data index of that coord.
    public static int CoordToBlockIndex(int x, int y, int z)
    {
        return ((z * 256) + (y * 16) + x);
    }

    //Given a chunk, local position, and possibly neighbor chunks, checks to see if this index is air or solid. 
    public static bool CheckForAirAtIndex(int x, int y, int z, Chunk c, Chunk[] neighbors = null)
    {
        if (World.forceShowChunkEdges)
        {
            if(x < 0 || x > 15)
            {
                return true;
            }

            if(y < 0 || y > 15)
            {
                return true;
            }
        } else
        {
            if (x < 0)
            {
                Chunk neighbor = neighbors[0];
                return CheckForAirAtIndex(15, y, z, neighbor);
            }

            if (x > 15)
            {
                Chunk neighbor = neighbors[1];
                return CheckForAirAtIndex(0, y, z, neighbor);
            }

            if (y < 0)
            {
                Chunk neighbor = neighbors[2];
                return CheckForAirAtIndex(x, 15, z, neighbor);
            }

            if (y > 15)
            {
                Chunk neighbor = neighbors[3];
                return CheckForAirAtIndex(x, 0, z, neighbor);
            }
        }
        

        if(z < 0 || z > 63)
        {
            return true;
        }

        int index = CoordToBlockIndex(x, y, z);
        return (c.blockData[index] < 1) ? true : false;
    }

    
}

//Object pool that we use to avoid needing to constantly create and destroy new chunk gameobjects.
//Allows us to return a game object to the pool when we no longer need it, and draw one when we do.
public class ChunkPool
{
    Queue<ChunkObject> chunks = new Queue<ChunkObject>();
    Material mat;
    public ChunkPool(int numChunks, Material common)
    {
        mat = common;
        for(int i = 0; i <= numChunks; i++)
        {
            GameObject chunkGO = new GameObject("Chunk");
            chunkGO.AddComponent<MeshRenderer>().sharedMaterial = common;
            MeshCollider collider = chunkGO.AddComponent<MeshCollider>();
            MeshFilter filter = chunkGO.AddComponent<MeshFilter>();

            chunks.Enqueue(new ChunkObject(chunkGO, collider, filter));
        }
    }

    public ChunkObject Draw()
    {
        if(chunks.Count > 0)
        {
            chunks.Peek().obj.SetActive(true);
            return (chunks.Dequeue());
        }

        Debug.LogWarning("ChunkPool recieved request for new chunk, but the pool is empty. Creating new chunk object.");

        GameObject chunkGO = new GameObject("Chunk");
        chunkGO.AddComponent<MeshRenderer>().sharedMaterial = mat;
        MeshCollider collider = chunkGO.AddComponent<MeshCollider>();
        MeshFilter filter = chunkGO.AddComponent<MeshFilter>();
        return (new ChunkObject(chunkGO, collider, filter));
    }

    public void Receive(ChunkObject chunk)
    {
        chunk.obj.SetActive(false);
        chunks.Enqueue(chunk);
    }
}

//Struct containing the most important elements of a chunk gameobject. Stored in loaded Chunks for easy access.
public struct ChunkObject
{
    public GameObject obj;
    public MeshCollider collider;
    public MeshFilter filter;
    public ChunkObject(GameObject obj, MeshCollider collider, MeshFilter filter)
    {
        this.obj = obj;
        this.collider = collider;
        this.filter = filter;
    }
}

//Struct used to store the mesh data for a given chunk.
public struct ChunkMeshData
{
    public Chunk c;
    public Vector3[] vertices;
    public int[] triangles;
    public Vector2[] uvs;

    public ChunkMeshData(Vector3[] _verticies, int[] _triangles, Vector2[] _uvs, Chunk _c)
    {
        c = _c;
        vertices = _verticies;
        triangles = _triangles;
        uvs = _uvs;
    }

    public ChunkMeshData(List<Vector3> _verticies, List<int> _triangles, List<Vector2> _uvs, Chunk _c)
    {
        c = _c;
        vertices = _verticies.ToArray();
        triangles = _triangles.ToArray();
        uvs = _uvs.ToArray();
    }

    struct ChunkUpdateData
    {
        public float timeRequestSent;
        public List<Chunk> chunksToUnload;
    }
}