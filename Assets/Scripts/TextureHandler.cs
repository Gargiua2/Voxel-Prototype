using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureHandler : MonoBehaviour
{
    public Vector2Int textureSheetSize = new Vector2Int(1,1);

    public List<Vector2> GetUVSForTexCoord (Vector2Int coord)
    {
        float w = textureSheetSize.x;
        float h = textureSheetSize.y;
        Vector2 gridCoords = coord;

        List<Vector2> uvs = new List<Vector2>();
        uvs.Add(new Vector2(gridCoords.x / w, gridCoords.y / h));
        uvs.Add(new Vector2(gridCoords.x / w, (gridCoords.y + 1) / h));
        uvs.Add(new Vector2((gridCoords.x + 1) / w, gridCoords.y / h));
        uvs.Add(new Vector2((gridCoords.x + 1) / w, (gridCoords.y + 1) / h));

        return uvs;
    }
}
