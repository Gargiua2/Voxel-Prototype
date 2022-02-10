using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Structure", menuName = "Structure")]
public class Structure : ScriptableObject
{
    public Vector3Int boundingBoxSize;
    public StructureBlock[] blocks;

    public void OnValidate()
    {
        if ((float)boundingBoxSize.x % 2 == 0)
            boundingBoxSize.x++;
        if ((float)boundingBoxSize.y % 2 == 0)
            boundingBoxSize.y++;
    }

}

[System.Serializable]
public struct StructureBlock
{
    public Vector3Int position;
    public Block blockType;

    public StructureBlock(Vector3Int _position, Block _blockType)
    {
        this.position = _position;
        this.blockType = _blockType;
    }
}