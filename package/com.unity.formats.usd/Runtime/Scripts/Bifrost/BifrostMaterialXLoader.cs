using System;
using System.Collections.Generic;
using System.IO;
using Unity.Formats.USD;
using UnityEditor;
using UnityEditor.MemoryProfiler;
using UnityEngine;
using UnityEngine.Windows;
using USD.NET;

public class BifrostMaterialXLoader
{
    Scene scene;
    SceneImportOptions importOptions;
    double sceneTime;
    string csvPath;
    public Dictionary<string, List<string[]>> matxSchema = new Dictionary<string, List<string[]>>();


    public Dictionary<string, Color> shaderColorDictionary = new Dictionary<string, Color>();
    public Dictionary<string, Texture2D> shaderTextureDictionary = new Dictionary<string, Texture2D>();
    public Dictionary<string, float> shaderFloatDictionary = new Dictionary<string, float>();

    public BifrostMaterialXLoader(Scene scene, SceneImportOptions importOptions)
    {
        if (scene != null)
        {
            this.scene = scene;
            this.importOptions = importOptions;
            sceneTime = scene.Time.GetValueOrDefault();
        }
        else
        {
            UnityEngine.Debug.LogWarning("Scene is null!");
        }

        csvPath = Path.GetFullPath("Packages/com.unity.formats.usd/Editor/Scripts/Bifrost/MaterialX.csv");
        LoadCSVAtPath();
    }

    void LoadCSVAtPath()
    {
        LoadCSVAtPath(csvPath);
    }

    void LoadCSVAtPath(string csvPath)
    {
        matxSchema.Clear();
        int lineCounter = 0;
        string currentKey = string.Empty;
        List<string[]> listOfNodeEntries = null;

        using (StreamReader reader = new StreamReader(csvPath))
        {
            while (!reader.EndOfStream)
            {
                string line = reader.ReadLine();
                ++lineCounter;
                string[] values = line.Split(',');
                if (values.Length < 2)
                {
                    Debug.LogError($"CSV Line {lineCounter} has too little data : {values}");
                    continue;
                }

                if (values[0] != string.Empty)
                {
                    currentKey = values[0];
                    if (matxSchema.ContainsKey(currentKey))
                    {
                        Debug.LogError($"Node {currentKey} has duplicate entries in csv.");
                    }
                    else
                    {
                        listOfNodeEntries = new List<string[]>();
                        matxSchema.Add(currentKey, listOfNodeEntries);
                    }
                }

                if (listOfNodeEntries == null)
                {
                    Debug.LogError($"CSV has sub nodes without specifying main node {values}");
                    continue;
                }

                listOfNodeEntries.Add(values[1..]);
            }
        }
    }
    string matName;
    public void ParseMaterialXNodesIntoShaderMap(string materialPath)
    {

        pxr.UsdPrim matPrim = scene.GetPrimAtPath(materialPath);
        matName = matPrim.GetName();
        List<string[]> listOfNodeEntries = null;
        if (matxSchema.TryGetValue("root", out listOfNodeEntries))
        {
            for (int i = 0; i < listOfNodeEntries.Count; i++)
            {
                RecursivelyExtractMaterialXData(matPrim, listOfNodeEntries[i], "", "");
            }

            Material mat = CreateMaterials();
            if (mat != null)
            {
                importOptions.materialMap[materialPath] = mat;
            }
        }
        else
        {
            Debug.LogError($"Root not specified in csv");
            return;
        }

    }

    Material CreateMaterials()
    {
        string destPath = Path.Combine(importOptions.projectAssetPath, importOptions.materialStorePath, matName, $"{matName}.mat");
        destPath = destPath.Replace('\\', '/');
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(destPath);

        if (AssetDatabase.LoadAssetAtPath<Material>(destPath) != null)
        {
            if (importOptions.overwriteMats)
            {
                AssetDatabase.DeleteAsset(destPath);
            }
            else
            {
                Debug.Log($"Material exists at {destPath} and override material is set to false.");
                return mat;
            }
        }

        mat = Material.Instantiate(importOptions.materialMap.MtlxXBifrostMaterial);
        AssetDatabase.CreateAsset(mat, destPath);

        var matAdapter = new HdrpShaderImporter(mat);
        matAdapter.ImportMtlxFromUsd();

        foreach(KeyValuePair<string, Color> pair in shaderColorDictionary)
        {
            mat.SetColor(pair.Key, pair.Value);
        }

        foreach (KeyValuePair<string, float> pair in shaderFloatDictionary)
        {
            mat.SetFloat(pair.Key, pair.Value);
        }

        foreach (KeyValuePair<string, Texture2D> pair in shaderTextureDictionary)
        {
            mat.SetTexture(pair.Key, pair.Value);
            string textureToggleKey = "_Use" + pair.Key;
            if (mat.HasInt(textureToggleKey))
            {
                mat.SetInt(textureToggleKey, 1);
            }
        }

        return mat;
    }

    void RecursivelyExtractMaterialXData(pxr.UsdPrim prim, string[] tokenNames, string shaderType, string shaderKey)
    {
        string tokenName = tokenNames[0];
        string currentShaderType = tokenNames[1];
        string currentShaderKey = tokenNames[2];
        string connectString = ".connect";
        string fileString = ":file";
        if (tokenName.EndsWith(connectString))
        {
            string tokenWithoutConnect = tokenName.Substring(0, tokenName.Length - connectString.Length);
            pxr.TfToken token = new pxr.TfToken(tokenWithoutConnect);
            pxr.UsdAttribute attribute = prim.GetAttribute(token);
            pxr.SdfPathVector connection = new pxr.SdfPathVector();
            if (attribute.GetConnections(connection) && connection.Count > 0)
            {
                string connectionPath = connection[0].ToString();
                pxr.SdfPath connectedPrimPath = new pxr.SdfPath(connectionPath).GetPrimPath();
                pxr.UsdPrim connectedPrim = scene.GetPrimAtPath(connectedPrimPath);
                pxr.TfToken connectedPrimName = connectedPrim.GetName();
                if (matxSchema.TryGetValue(connectedPrimName, out List<string[]> listOfNodeEntries))
                {
                    for (int i = 0; i < listOfNodeEntries.Count; i++)
                    {
                        string shaderTypeToRecurseDown = shaderType;
                        string shaderKeyToRecurseDown = shaderKey;

                        if (shaderTypeToRecurseDown == string.Empty && shaderKeyToRecurseDown == string.Empty)
                        {
                            shaderTypeToRecurseDown = currentShaderType;
                            shaderKeyToRecurseDown = currentShaderKey;
                        }

                        if((shaderType == string.Empty && shaderKey != string.Empty) ||
                            (shaderType != string.Empty && shaderKey == string.Empty))
                        {
                            Debug.LogError($"ShaderType : {shaderType} & shaderKey {shaderKey} do not both contain values");
                        }

                        if ((currentShaderType == string.Empty && currentShaderKey != string.Empty) ||
                            (currentShaderType != string.Empty && currentShaderKey == string.Empty))
                        {
                            Debug.LogError($"ShaderType : {shaderType} & shaderKey {shaderKey} do not both contain values");
                        }

                        if (currentShaderType != string.Empty && shaderType != string.Empty)
                        {
                            Debug.LogError($"CurrentShaderType : {currentShaderType} & shaderType {shaderType} both contain values.");
                        }

                        if (currentShaderKey != string.Empty && shaderKey != string.Empty)
                        {
                            Debug.LogError($"currentShaderKey : {currentShaderKey} & shaderKey {shaderKey} both contain values.");
                        }

                        RecursivelyExtractMaterialXData(connectedPrim, listOfNodeEntries[i], shaderTypeToRecurseDown, shaderKeyToRecurseDown);
                    }
                }
                else
                {
                    Debug.Log($"{connectedPrimName} does not have nodes to check");
                }
            }
        }
        else if (tokenName.EndsWith(fileString))
        {
            string shaderKeyToStoreTexture = "";
            if(shaderType == "texture" && shaderKey != string.Empty)
            {
                shaderKeyToStoreTexture = shaderKey;
            }
            else if(currentShaderType == "texture" && currentShaderKey != string.Empty)
            {
                shaderKeyToStoreTexture = currentShaderKey;
            }

            if(shaderKeyToStoreTexture == string.Empty)
            {
                Debug.LogError($"Trying to load texture file but no connection found to shader {tokenName}");
                return;
            }

            if (shaderTextureDictionary.ContainsKey(shaderKeyToStoreTexture))
            {
                Debug.LogError($"shaderColorDictionary already contains key {shaderKey}");
                return;
            }

            pxr.TfToken token = new pxr.TfToken(tokenName);
            pxr.VtValue vtvalue = new pxr.VtValue();
            if(prim.GetAttributeValue(token, vtvalue, sceneTime))
            {
                pxr.SdfAssetPath texturePath = pxr.UsdCs.VtValueToSdfAssetPath(vtvalue);
                string resolvedTexturePath = texturePath.GetResolvedPath();
                if (!System.IO.File.Exists(resolvedTexturePath))
                {
                    Debug.Log($"File does not exist at texture Path : {resolvedTexturePath}");
                }
                //string destPath = Path.Combine(importOptions.projectAssetPath, Path.GetFileName(resolvedTexturePath));
                //string assetPath = importOptions.projectAssetPath + Path.GetFileName(resolvedTexturePath);
                string destPath = Path.Combine(importOptions.projectAssetPath, importOptions.materialStorePath, matName, Path.GetFileName(resolvedTexturePath));
                destPath = destPath.Replace('\\', '/');

                if (System.IO.File.Exists(destPath) && importOptions.overwriteMats)
                {
                    System.IO.File.Delete(destPath);
                    Debug.Log($"Replacing texture at : {destPath}");
                }

                if (!System.IO.File.Exists(destPath))
                {
                    string destinationDirectory = Path.GetDirectoryName(destPath);
                    System.IO.Directory.CreateDirectory(destinationDirectory);
                    System.IO.File.Copy(resolvedTexturePath, destPath, true);
                    UnityEditor.AssetDatabase.ImportAsset(destPath);
                    UnityEditor.TextureImporter texImporter =
                        (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(destPath);
                    if (texImporter == null)
                    {
                        Debug.LogError("Failed to load asset: " + destPath);
                    }
                    else
                    {
                        texImporter.isReadable = true;
                        if (shaderKey.Contains("normal", StringComparison.InvariantCultureIgnoreCase))
                        {
                            texImporter.convertToNormalmap = true;
                            texImporter.textureType = UnityEditor.TextureImporterType.NormalMap;
                        }

                        UnityEditor.EditorUtility.SetDirty(texImporter);
                        texImporter.SaveAndReimport();
                    }
                }

                Texture2D texture2D = (Texture2D)UnityEditor.AssetDatabase.LoadAssetAtPath(destPath, typeof(Texture2D));
                shaderTextureDictionary.Add(shaderKeyToStoreTexture, texture2D);
            }
        }
        else
        {
            if (currentShaderType != string.Empty && currentShaderKey != string.Empty)
            {
                pxr.TfToken token = new pxr.TfToken(tokenName);
                pxr.UsdAttribute attribute = prim.GetAttribute(token);

                if (tokenNames.Length >= 6 && tokenNames[3] == "customData")
                {
                    string customDataName = tokenNames[4];
                    string customDataType = tokenNames[5];

                    if (customDataName == string.Empty)
                    {
                        Debug.LogError("Custom data specified but name is empty");
                    }

                    if (customDataType == string.Empty)
                    {
                        Debug.LogError("Custom data specified but type is empty");
                    }

                    AddCustomDataToDictionaryBasedOnType(attribute, customDataName, customDataType, currentShaderKey);
                }
                else
                {
                    pxr.VtValue vtvalue = new pxr.VtValue();
                    if (prim.GetAttributeValue(token, vtvalue, sceneTime))
                    {
                        AddDataToDictionaryBasedOnType(currentShaderType, vtvalue, currentShaderKey);
                    }
                }

            }
        }
    }

    void AddCustomDataToDictionaryBasedOnType(pxr.UsdAttribute attribute, string customDataName, string customDataType, string shaderKey)
    {

        switch (customDataType)
        {
            case "double3":
                pxr.GfVec3d customData = pxr.UsdCs.VtValueToGfVec3d(attribute.GetCustomDataByKey(new pxr.TfToken(customDataName)));

                if (shaderColorDictionary.ContainsKey(shaderKey))
                {
                    Debug.LogError($"shaderColorDictionary already contains key {shaderKey}");
                    return;
                }

                Color col = new Color((float)customData[0], (float)customData[1], (float)customData[2]);
                Debug.Log($"Adding color to dictionary : {shaderKey},{col}");
                shaderColorDictionary.Add(shaderKey, col);

                break;

            case "double":
                if (shaderFloatDictionary.ContainsKey(shaderKey))
                {
                    Debug.LogError($"shaderFloatDictionary already contains key {shaderKey}");
                    return;
                }

                double doubleVal = pxr.UsdCs.VtValueTodouble(attribute.GetCustomDataByKey(new pxr.TfToken(customDataName)));
                Debug.Log($"Got custom data as string : {shaderKey},{doubleVal}");
                shaderFloatDictionary.Add(shaderKey, (float)doubleVal);

                break;

            case "string":
                string customString = pxr.UsdCs.VtValueTostring(attribute.GetCustomDataByKey(new pxr.TfToken(customDataName)));
                Debug.Log($"Got custom data as string : {shaderKey},{customString}");
                break;
        }
    }

    void AddDataToDictionaryBasedOnType(string type, pxr.VtValue vtvalue, string shaderKey)
    {

        switch (type)
        {
            case "color":
                if (shaderColorDictionary.ContainsKey(shaderKey))
                {
                    Debug.LogError($"shaderColorDictionary already contains key {shaderKey}");
                    return;
                }

                pxr.GfVec3f vtCol = pxr.UsdCs.VtValueToGfVec3f(vtvalue);
                Color col = new Color(vtCol[0], vtCol[1], vtCol[2]);
                Debug.Log($"Adding color to dictionary : {shaderKey}, {col}");
                shaderColorDictionary.Add(shaderKey, col);

                break;

            case "texture":
                Debug.LogError($"Converting custom data to texture is not supported");
                break;

            case "float":
                if (shaderFloatDictionary.ContainsKey(shaderKey))
                {
                    Debug.LogError($"Dictionary already contains key {shaderKey}");
                    return;
                }

                float floatval = pxr.UsdCs.VtValueTofloat(vtvalue);
                Debug.Log($"Adding float to dictionary : {shaderKey}, {floatval}");
                shaderFloatDictionary.Add(shaderKey, floatval);

                break;
        }
    }
}


/*
 * 
 *         [InputParameter("_File")]
        public Connectable<pxr.SdfAssetPath> file =
            new Connectable<pxr.SdfAssetPath>(new pxr.SdfAssetPath(""));

// TODO: look for the expected texture/primvar reader pair.
            var textureSample = new TextureReaderSample();
            var connectedPrimPath = scene.GetSdfPath(connection.connectedPath).GetPrimPath();
            Texture2D result = null;

            scene.Read(connectedPrimPath, textureSample);

*/
