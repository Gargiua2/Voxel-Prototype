using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//Data representing different block types.
//These are ScriptableObjects, so that I can easily create new block types via the asset menu/inspector
//All block types are read into a dictionary when the game runs in our BlockManager class. This allows for 
//blocks to be easily referenced by ID, without the need to wastefully store this data literally millions of times.

[CreateAssetMenu(fileName = "New Block Type", menuName = "Block")]
public class Block : ScriptableObject
{
    //The ID of this type of block. Used to look up this block/its texture when generating the world.
    public int blockID;

    //We use a texture atlas to store all of our block textures. Each of these vector2ints represent
    //the cell in the atlas grid where each block's sides texture is. Used when generating chunk meshes
    //to build UVs.
    public Vector2Int topTextureCoord;
    public Vector2Int leftTextureCoord;
    public Vector2Int rightTextureCoord;
    public Vector2Int bottomTextureCoord;
    public Vector2Int backTextureCoord;
    public Vector2Int frontTextureCoord;
}
