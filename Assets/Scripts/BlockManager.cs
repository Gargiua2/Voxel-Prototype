using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BlockManager : MonoBehaviour
{
    public List<Block> blockList = new List<Block>();
    public static Dictionary<int, Block> blocks = new Dictionary<int, Block>();


    //Read all blocks into from our list into a dictionary for easy access via blockID later.
    void Awake()
    {
        foreach(Block b in blockList)
        {
            blocks.Add(b.blockID, b);
        }
    }

    //Simply return the block data for the block with a given id.
    public static Block GetBlock(int id)
    {
        return blocks[id];
    }
}
