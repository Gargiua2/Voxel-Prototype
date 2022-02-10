using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Tree", menuName = "Tree")]
public class Tree : Structure
{
    public Block logBlock;
    public Block leavesBlock;
    public Vector2Int heightVariation = new Vector2Int(-1,2);
    [Range(0,1)]public float leafDecayChance;

    public Structure GenerateTree()
    {
        int height = Random.Range(heightVariation.x, heightVariation.y);
        List<StructureBlock> blockList = new List<StructureBlock>();
        foreach(StructureBlock sb in blocks)
        {
            blockList.Add(sb);
        }

        for (int i = 0; i < blockList.Count; i++)
        {
            blockList[i] = new StructureBlock(blockList[i].position + new Vector3Int(0,0,height), blockList[i].blockType);
        }

        List<StructureBlock> newBlocks = blockList;

        for(int i = 0; i <blockList.Count; i++)
        {
            if(blockList[i].position.z < 0)
            {
                newBlocks.Remove(blockList[i]);
            }
        }

        for(int i = 0; i < height; i++)
        {
            newBlocks.Add(new StructureBlock(new Vector3Int(0, 0, i), logBlock));
        }

        Dictionary<Vector3Int, Block> currentBlocks = new Dictionary<Vector3Int, Block>();
        foreach(StructureBlock sb in newBlocks)
        {
            currentBlocks.Add(sb.position, sb.blockType);
        }

        List<StructureBlock> toRemove = new List<StructureBlock>();

        foreach(StructureBlock sb in newBlocks)
        {
            if(sb.blockType == leavesBlock)
            {
                int neighbors = 0;

                if (currentBlocks.ContainsKey(new Vector3Int(sb.position.x, sb.position.y, sb.position.z + 1)))
                {
                    neighbors++;
                }
                if (currentBlocks.ContainsKey(new Vector3Int(sb.position.x, sb.position.y, sb.position.z - 1)))
                {
                    neighbors++;
                }
                if (currentBlocks.ContainsKey(new Vector3Int(sb.position.x, sb.position.y+1, sb.position.z)))
                {
                    neighbors++;
                }
                if (currentBlocks.ContainsKey(new Vector3Int(sb.position.x, sb.position.y - 1, sb.position.z)))
                {
                    neighbors++;
                }
                if (currentBlocks.ContainsKey(new Vector3Int(sb.position.x + 1, sb.position.y, sb.position.z)))
                {
                    neighbors++;
                }
                if (currentBlocks.ContainsKey(new Vector3Int(sb.position.x - 1, sb.position.y, sb.position.z)))
                {
                    neighbors++;
                }

                if(neighbors <= 3)
                {
                    float roll = Random.value;

                    if (roll < leafDecayChance)
                        toRemove.Add(sb);
                }
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
            newBlocks.Remove(toRemove[i]);

        Structure s = CreateInstance(typeof(Structure)) as Structure;
        s.boundingBoxSize = new Vector3Int(this.boundingBoxSize.x, this.boundingBoxSize.y, this.boundingBoxSize.z + height);
        s.blocks = newBlocks.ToArray();

        return s;
    }


}
