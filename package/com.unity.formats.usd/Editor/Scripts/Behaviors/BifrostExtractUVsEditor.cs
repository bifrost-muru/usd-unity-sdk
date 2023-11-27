using System.Collections;
using System.Collections.Generic;
using Unity.Formats.USD;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BifrostExtractUVs))]
public class BifrostExtractUVsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BifrostExtractUVs UVExtractor = (BifrostExtractUVs)target;
        if (GUILayout.Button("Extract UVs"))
        {
            UVExtractor.GetUVs();
        }
    }
}
