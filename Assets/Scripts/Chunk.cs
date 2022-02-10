using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Non-MonoBehaviour class that stores the data pertaining to a chunk of terrain.

public class Chunk
{
    public Vector2Int position; //Position of this chunk in the infinite grid.
    public ChunkObject obj; //If this chunk is loaded, this contains a reference to the GameObject representing this chunk. 

    //The most important element of a chunk. A 16*16*64 int array. Representing all of the voxels in a chunk. 
    //The int in each index of the array represents what type of voxel is at the corresponding position.
    //A more optimal version of this code would likely use a smaller data type than int, since we have very block types.
    public int[] blockData = new int[16 * 16 * 64]; 
    public ChunkState state = 0;
}

//Due to the way we generate worlds, we can't simply generate a chunk in one pass.
//Chunks require some data about the chunks near them, and because of this, we generate
//chunks in a few distinct phases, as those chunks get closer to becoming rendered/loaded by the palyer.
//This enum defines the generation state of a chunk, from empty to fully heightmapped and populated.
public enum ChunkState : int
{
    EMPTY = 0,
    HEIGHTMAPPED = 1,
    POPULATED = 2
}