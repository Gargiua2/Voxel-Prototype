using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class UIOverlay : MonoBehaviour
{
    public TextMeshProUGUI FPScounter;
    public int framesToAverage = 5;

    List<float> fps = new List<float>();

    void Update()
    {
        float frameFPS = 1f / Time.smoothDeltaTime;
        fps.Add(frameFPS);
        if (fps.Count > framesToAverage)
        {
            fps.RemoveAt(0);
        }
        float averageFPS = 1000000;
        for(int i = 0; i < fps.Count; i++)
        {
            if (fps[i] < averageFPS)
                averageFPS = fps[i];
        }

        FPScounter.text = "" + Mathf.RoundToInt(averageFPS) * 2;
    }
}
