using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;



public class BifrostMaterialXLoaderEditor : EditorWindow
{
    [MenuItem("Bifrost/MaterialX")]
    static void Init()
    {
        GetWindow<BifrostMaterialXLoaderEditor>();
    }

    void OnEnable()
    {
        titleContent = new GUIContent("BifrostMaterialX");
        minSize = new Vector2(650, 200);

        wantsMouseMove = true;
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Parse CSV"))
        {
            BifrostMaterialXLoader matxParser = new BifrostMaterialXLoader(null, null);

            foreach (KeyValuePair<string, List<string[]>> pair in matxParser.matxSchema)
            {
                string key = pair.Key;
                List<string[]> value = pair.Value;

                Debug.Log($"Node {key} has the following nodes : ");

                for (int i = 0; i < value.Count; ++i)
                {
                    string FormattedLine = $"Line {i} : ";
                    for (int j = 0; j < value[i].Length; j++)
                    {
                        string val = value[i][j];
                        if (val == string.Empty)
                        {
                            val = "N/A";
                        }
                        FormattedLine += $"{val} | ";
                    }
                    Debug.Log(FormattedLine);
                }
            }
        }
    }

}

