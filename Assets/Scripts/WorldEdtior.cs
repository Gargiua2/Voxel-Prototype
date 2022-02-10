using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class WorldEdtior : MonoBehaviour
{
    public float range = 6;
    public LayerMask worldMask;
    public LayerMask playerMask;
    public GameObject indicator;

    int selectedBlockID = 1;
    ChunkLoader loader;
    void Start()
    {
        loader = GetComponent<ChunkLoader>();
    }

    void Update()
    {
        

        foreach (KeyCode kcode in Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(kcode))
            {
                int key;
                if(Int32.TryParse(kcode.ToString().Trim("Alpha".ToCharArray()), out key))
                {
                    Debug.Log("PARSED");
                    selectedBlockID = key;
                }
            }
        }

        RaycastHit h;
        if (Physics.Raycast(transform.position, transform.forward, out h, range, worldMask))
        {
            indicator.transform.position = h.point - h.normal * .01f;
            indicator.transform.position = new Vector3(Mathf.FloorToInt(indicator.transform.position.x), Mathf.FloorToInt(indicator.transform.position.y), Mathf.FloorToInt(indicator.transform.position.z)) + Vector3.one/2;
            indicator.transform.position += h.normal * .6f;
            indicator.transform.forward = h.normal;
        } else
        {
            indicator.transform.position = new Vector3(0, -1000, 0);
        }

        if (Input.GetMouseButtonDown(0))
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit, range, worldMask))
            {
                Vector3 testPoint = hit.point - hit.normal * .01f;

                loader.UpdateChunk(testPoint, 0);
            }
        }

        if (Input.GetMouseButtonDown(1))
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, transform.forward, out hit, range, worldMask))
            {
                Vector3 testPoint = hit.point + hit.normal * .01f;

                Vector3 physicsTestPoint;
                physicsTestPoint = hit.point - hit.normal * .01f;
                physicsTestPoint = new Vector3(Mathf.FloorToInt(physicsTestPoint.x), Mathf.FloorToInt(physicsTestPoint.y), Mathf.FloorToInt(physicsTestPoint.z)) + Vector3.one / 2;
                physicsTestPoint += hit.normal;

                Collider[] cast = Physics.OverlapBox(physicsTestPoint, Vector3.one * .48f, Quaternion.identity, playerMask);
                if (cast.Length == 0)
                {
                    loader.UpdateChunk(testPoint, selectedBlockID);
                }
                
            }
        }
    }
}
