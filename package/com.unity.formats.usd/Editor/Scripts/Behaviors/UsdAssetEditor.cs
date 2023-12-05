// Copyright 2018 Jeremy Cowles. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.IO;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System;
using USD.NET;
using pxr;
using System.Drawing;
#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;

#endif

namespace Unity.Formats.USD
{
    [CustomEditor(typeof(UsdAsset))]
    public class UsdAssetEditor : Editor
    {
        private readonly string[] kTabNames = new string[] { "Simple", "Advanced" };
        private int m_tab;

        private Texture2D m_usdLogo;
        private Texture2D m_refreshButton;
        private Texture2D m_trashButton;
        private Texture2D m_reimportButton;
        private Texture2D m_detachButton;

        private string pathToSaveImages = string.Empty;
        private int m_screenWidth = 1920;
        private int m_screenHeight = 1080;

        public Shader m_MaterialBaseShader;

        private enum LinearUnits
        {
            Millimeters = -1,
            Centimeters = 0,
            Meters = 1,
            Decimeters = 2,
            Kilometers = 3,
            Custom = 4
        }

        public void OnEnable()
        {
            if (!m_usdLogo)
            {
                var script = MonoScript.FromScriptableObject(this);
                var path = AssetDatabase.GetAssetPath(script);
                var rootPath = Path.GetDirectoryName(path);
                m_usdLogo = AssetDatabase.LoadAssetAtPath(rootPath + "/UsdBanner.png",
                    typeof(Texture2D)) as Texture2D;
                m_refreshButton = AssetDatabase.LoadAssetAtPath(rootPath + "/RefreshButton.png",
                    typeof(Texture2D)) as Texture2D;
                m_trashButton = AssetDatabase.LoadAssetAtPath(rootPath + "/Trash.png",
                    typeof(Texture2D)) as Texture2D;
                m_reimportButton = AssetDatabase.LoadAssetAtPath(rootPath + "/Reimport.png",
                    typeof(Texture2D)) as Texture2D;
                m_detachButton = AssetDatabase.LoadAssetAtPath(rootPath + "/Detach.png",
                    typeof(Texture2D)) as Texture2D;
            }
        }

        private GameObject GetPrefabObject(GameObject root)
        {
            // This is a great resource for determining object type, but only covers new APIs:
            // https://github.com/Unity-Technologies/UniteLA2018Examples/blob/master/Assets/Scripts/GameObjectTypeLogging.cs
            return PrefabUtility.GetCorrespondingObjectFromSource(root);
        }

        private bool IsPrefabInstance(GameObject root)
        {
            return GetPrefabObject(root) != null;
        }

        public override void OnInspectorGUI()
        {
            var usdAsset = (UsdAsset)this.target;

            if (usdAsset.m_displayColorMaterial == null)
            {
                Debug.LogWarning("No fallback material set, reverting to default");
                var matMap = new MaterialMap();
                usdAsset.m_displayColorMaterial = matMap.DisplayColorMaterial;
            }

            if (usdAsset.m_metallicWorkflowMaterial == null)
            {
                Debug.LogWarning("No metallic material set, reverting to default");
                var matMap = new MaterialMap();
                usdAsset.m_metallicWorkflowMaterial = matMap.MetallicWorkflowMaterial;
            }

            if (usdAsset.m_specularWorkflowMaterial == null)
            {
                Debug.LogWarning("No specular material set, reverting to default");
                var matMap = new MaterialMap();
                usdAsset.m_specularWorkflowMaterial = matMap.SpecularWorkflowMaterial;
            }

            if (usdAsset.m_mtlxBifrostMaterial == null)
            {
                Debug.LogWarning("No mtlx material set, reverting to default");
                var matMap = new MaterialMap();
                matMap.MtlxXBifrostShaderName = usdAsset.MtlxXBifrostShaderName;
                usdAsset.m_mtlxBifrostMaterial = matMap.MtlxXBifrostMaterial;
            }

            var buttonStyle = new GUIStyle(GUI.skin.button);
            buttonStyle.fixedWidth = 32;
            buttonStyle.fixedHeight = 32;

            var gsImageStyle = new GUIStyle();
            gsImageStyle.alignment = TextAnchor.MiddleCenter;
            gsImageStyle.normal.background = EditorGUIUtility.whiteTexture;
            gsImageStyle.padding.bottom = 0;

            GUILayout.Space(5);
            GUILayout.BeginHorizontal(gsImageStyle);
            EditorGUILayout.LabelField(new GUIContent(m_usdLogo), GUILayout.MinHeight(40.0f));

            var refreshStyle = new GUIStyle(buttonStyle);
            refreshStyle.fixedHeight = 38;
            refreshStyle.fixedWidth = 38;
            if (GUILayout.Button(new GUIContent(m_refreshButton, "Refresh values from USD"), refreshStyle))
            {
                if (EditorUtility.DisplayDialog("Refresh from Source", "Refresh values from USD?\n\n"
                    + "Any object set to import will have it's state updated from USD",
                    "OK", "Cancel"))
                {
                    ReloadFromUsd(usdAsset, forceRebuild: false);
                }
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.FlexibleSpace();
            if (IsPrefabInstance(usdAsset.gameObject))
            {
                var style = new GUIStyle();
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 12;
                style.wordWrap = true;
                EditorGUILayout.LabelField("Edit prefab for destructive operations", style);
            }
            else
            {
                if (usdAsset.transform.childCount == 0)
                {
                    if (GUILayout.Button(new GUIContent(m_reimportButton, "Import from USD"), buttonStyle))
                    {
                        ReloadFromUsd(usdAsset, forceRebuild: true);
                    }
                }
                else
                {
                    if (GUILayout.Button(new GUIContent(m_reimportButton, "Reimport from USD (destructive)"),
                        buttonStyle))
                    {
                        if (EditorUtility.DisplayDialog("Reimport from Source",
                            "Destroy and rebuild all USD objects?\n\n"
                            + "Any GameObject with a UsdPrimSource will be destroyed and reimported.",
                            "OK", "Cancel"))
                        {
                            ReloadFromUsd(usdAsset, forceRebuild: true);
                        }
                    }
                }

                EditorGUI.BeginDisabledGroup(usdAsset.transform.childCount == 0);
                if (GUILayout.Button(new GUIContent(m_trashButton, "Remove USD Contents (destructive)"), buttonStyle))
                {
                    if (EditorUtility.DisplayDialog("Clear Contents", "Destroy all USD objects?\n\n"
                        + "Any GameObject with a UsdPrimSource will be destroyed. "
                        + "These objects can be re-imported but any custom components will be lost.",
                        "OK", "Cancel"))
                    {
                        DestroyAllImportedObjects(usdAsset);
                    }
                }

                if (GUILayout.Button(new GUIContent(m_detachButton, "Detach, remove all USD components"), buttonStyle))
                {
                    if (EditorUtility.DisplayDialog("Detach from USD", "Remove all USD components?\n\n"
                        + "USD components will be destroyed (except the UsdAsset root), "
                        + "but can be recreated by refreshing from USD.",
                        "OK", "Cancel"))
                    {
                        DetachFromUsd(usdAsset);
                    }
                }

                EditorGUI.EndDisabledGroup();
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (Application.isPlaying && GUILayout.Button("Reload from USD (Coroutine)"))
            {
                ReloadFromUsdAsCoroutine(usdAsset);
            }

            GUILayout.Space(5);

            m_tab = GUILayout.Toolbar(m_tab, kTabNames);
            switch (m_tab)
            {
                case 0:
                    DrawSimpleInspector(usdAsset);
                    break;
                case 1:
                    base.DrawDefaultInspector();
                    break;
            }
        }

        private void DrawSimpleInspector(UsdAsset usdAsset)
        {
            GUILayout.Label("Source Asset", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("USD File");
            GUI.enabled = false;
            EditorGUILayout.TextField(usdAsset.usdFullPath, EditorStyles.textField);
            GUI.enabled = true;

            if (GUILayout.Button("..."))
            {
                string lastDir;
                if (string.IsNullOrEmpty(usdAsset.usdFullPath))
                    lastDir = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                else
                    lastDir = Path.GetDirectoryName(usdAsset.usdFullPath);
                string importFilepath =
                    EditorUtility.OpenFilePanelWithFilters("Usd Asset", lastDir, new string[] { "Usd", "us*" });
                if (string.IsNullOrEmpty(importFilepath)) return;
                usdAsset.usdFullPath = importFilepath;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("USD Root Path");
            usdAsset.m_usdRootPath = EditorGUILayout.TextField(usdAsset.m_usdRootPath, EditorStyles.textField);
            EditorGUILayout.EndHorizontal();

            GUILayout.Label("Import Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.BeginHorizontal();

            var op = LinearUnits.Custom;

            if (usdAsset.m_scale == 1)
            {
                op = LinearUnits.Meters;
            }
            else if (usdAsset.m_scale == .1f)
            {
                op = LinearUnits.Decimeters;
            }
            else if (usdAsset.m_scale == .01f)
            {
                op = LinearUnits.Centimeters;
            }
            else if (usdAsset.m_scale == .001f)
            {
                op = LinearUnits.Millimeters;
            }
            else if (usdAsset.m_scale == 1000f)
            {
                op = LinearUnits.Kilometers;
            }

            var newOp = (LinearUnits)EditorGUILayout.EnumPopup("Original Scale", op);

            if (newOp == LinearUnits.Custom)
            {
                // Force the UI to stay on the "custom" selection by adding an offset.
                float offset = op == newOp ? 0 : 0.01f;
                usdAsset.m_scale = EditorGUILayout.FloatField(usdAsset.m_scale + offset);
            }

            op = newOp;
            EditorGUILayout.EndHorizontal();

            switch (op)
            {
                case LinearUnits.Millimeters:
                    usdAsset.m_scale = .001f;
                    break;
                case LinearUnits.Centimeters:
                    usdAsset.m_scale = .01f;
                    break;
                case LinearUnits.Decimeters:
                    usdAsset.m_scale = .1f;
                    break;
                case LinearUnits.Meters:
                    usdAsset.m_scale = 1f;
                    break;
                case LinearUnits.Kilometers:
                    usdAsset.m_scale = 1000;
                    break;
            }

            usdAsset.m_materialImportMode =
                (MaterialImportMode)EditorGUILayout.EnumPopup("Materials", usdAsset.m_materialImportMode);
            usdAsset.m_payloadPolicy =
                (PayloadPolicy)EditorGUILayout.EnumPopup("Payload Policy", usdAsset.m_payloadPolicy);

            GUILayout.Label("Object Types", EditorStyles.boldLabel);

            usdAsset.m_importCameras = EditorGUILayout.Toggle("Import Cameras", usdAsset.m_importCameras);
            usdAsset.m_importMeshes = EditorGUILayout.Toggle("Import Meshes", usdAsset.m_importMeshes);
            usdAsset.m_importSkinning = EditorGUILayout.Toggle("Import Skinning", usdAsset.m_importSkinning);
            usdAsset.m_importTransforms = EditorGUILayout.Toggle("Import Transforms", usdAsset.m_importTransforms);
            usdAsset.m_overwriteMats = EditorGUILayout.Toggle("Overwrite Materials", usdAsset.m_overwriteMats);

            // #MTODO : Change this to load materials from folder
            //if (GUILayout.Button("Save Materials"))
            //{
            //    string rootFolderPath = "Assets/" + m_MaterialStorePath;
            //    if (!Directory.Exists(rootFolderPath))
            //    {
            //        Directory.CreateDirectory(rootFolderPath);
            //    }

            //    CreateHierarchyAndMaterials(usdAsset.transform, rootFolderPath);
            //    AssetDatabase.Refresh();
            //}

            GUILayout.Label("Screenshot Tools", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Screenshot Height");
            m_screenHeight = int.Parse(GUILayout.TextField(m_screenHeight.ToString(), 250, "textfield"));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Screenshot Width");
            m_screenWidth = int.Parse(GUILayout.TextField(m_screenWidth.ToString(), 250, "textfield"));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Path");
            pathToSaveImages = GUILayout.TextField(pathToSaveImages, 250, "textfield");
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Capture Camera shots") && IsPathValid(pathToSaveImages))
            {
                GameObject camerasGO = GameObject.Find("cameras");
                Camera mainCamera = Camera.main;

                if (camerasGO != null && Camera.main != null)
                {
                    Vector3 mainCameraOriginalPosition = mainCamera.transform.position;
                    Quaternion mainCameraOriginalRotation = mainCamera.transform.rotation;

                    for (int i = 0; i < camerasGO.transform.childCount; ++i)
                    {
                        mainCamera.transform.SetPositionAndRotation(
                            camerasGO.transform.GetChild(i).position,
                            camerasGO.transform.GetChild(i).rotation);
                        CaptureScreenshot(mainCamera, Path.Combine(pathToSaveImages, i.ToString() + ".png"));
                    }

                    mainCamera.transform.SetPositionAndRotation(
                            mainCameraOriginalPosition,
                            mainCameraOriginalRotation);
                }


            }

            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(usdAsset);
        }
        private void CreateHierarchyAndMaterials(Transform parent, string parentFolderPath)
        {
            //foreach (Transform child in parent)
            //{
            //    string childFolderPath = parentFolderPath + "/" + child.name;
            //    Directory.CreateDirectory(childFolderPath);

            //    MeshFilter meshFilter = child.GetComponent<MeshFilter>();
            //    if (meshFilter != null)
            //    {
            //        MeshRenderer meshRenderer = child.GetComponent<MeshRenderer>();

            //        if (meshRenderer != null)
            //        {
            //            Material[] materials = meshRenderer.sharedMaterials;

            //            foreach (Material material in materials)
            //            {
            //                string materialName = material.name + ".mat";
            //                string fullMaterialPath = childFolderPath + "/" + materialName;

            //                string materialPath = AssetDatabase.GetAssetPath(material);
            //                if(!string.IsNullOrEmpty(materialPath))
            //                {
            //                    Debug.Log($"Material already saved at {materialPath}");
            //                    continue;
            //                }

            //                if (AssetDatabase.LoadAssetAtPath<Material>(fullMaterialPath) != null)
            //                {
            //                    if(m_OverwriteExistingMaterials)
            //                    {
            //                        AssetDatabase.DeleteAsset(fullMaterialPath);
            //                    }
            //                    else
            //                    {
            //                        Debug.Log($"Material exists at {fullMaterialPath} and override material is set to false.");
            //                        continue;
            //                    }
            //                }


            //                AssetDatabase.CreateAsset(material, fullMaterialPath);
            //            }
            //        }
            //    }

            //    CreateHierarchyAndMaterials(child, childFolderPath);
            //}
        }

        static bool IsPathValid(string path)
        {
            try
            {
                Path.GetFullPath(path);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        void CaptureScreenshot(Camera camera, string path)
        {
            if (camera == null)
            {
                Debug.LogError("Camera not assigned!");
                return;
            }
            m_screenWidth = 1920;
            m_screenHeight = 1080;

            RenderTexture renderTexture = new RenderTexture(m_screenWidth, m_screenHeight, 24);
            camera.targetTexture = renderTexture;

            Texture2D screenshot = new Texture2D(m_screenWidth, m_screenHeight, TextureFormat.RGB24, false);
            camera.Render();
            RenderTexture.active = renderTexture;
            screenshot.ReadPixels(new Rect(0, 0, m_screenWidth, m_screenHeight), 0, 0);
            screenshot.Apply();

            camera.targetTexture = null;
            RenderTexture.active = null;
            DestroyImmediate(renderTexture);

            byte[] bytes = screenshot.EncodeToPNG();
            //string filePath = "Assets/Screenshots/screenshot.png"; // Adjust the file path as needed
            System.IO.File.WriteAllBytes(path, bytes);

            Debug.Log("Screenshot captured and saved to: " + path);
        }

        private void IterateChildrenAndDeleteMatchingNames(UnityEngine.Transform go, string name)
        {
            List<UnityEngine.Transform> childrenToDelete = new List<UnityEngine.Transform>();

            GetAllChildTransformsWithMatchingName(go.transform, name, ref childrenToDelete);

            for (int i = 0; i < childrenToDelete.Count; ++i)
            {
                DestroyImmediate(childrenToDelete[i].gameObject);
            }
        }

        private void LoadUVsOnAllChildren(Transform transform, Scene scene)
        {
            for (int i = 0; i < transform.childCount; ++i)
            {
                Transform childTransform = transform.GetChild(i);
                LoadUVsOnAllChildren(childTransform, scene);

                UsdPrimSource primSource = childTransform.GetComponent<UsdPrimSource>();

                if(!primSource)
                {
                    return;
                }

                string usdPrimpath = primSource.m_usdPrimPath;
                UsdPrim prim = scene.GetPrimAtPath(usdPrimpath);
                VtValue val = new VtValue();
                TfToken attriname = new TfToken("primvars:st");

                double usdTime = scene.Time.GetValueOrDefault();
                MeshFilter meshFilter = childTransform.GetComponent<MeshFilter>();

                if (meshFilter && prim.GetAttributeValue(attriname, val, usdTime))
                {
                    VtVec2fArray Vec2dArray = new VtVec2fArray(val.GetArraySize());

                    if (val.CanCastToTypeOf(Vec2dArray))
                    {
                        UsdCs.VtValueToVtVec2fArray(val, Vec2dArray);
                        uint size = Vec2dArray.size();
                        Vector2[] uvs = new Vector2[size];

                        for (int j = 0; j < size; ++j)
                        {
                            Vector2 vector2D = new Vector2(Vec2dArray[j][0], Vec2dArray[j][1]);
                            uvs[j] = vector2D;
                        }

                        int verticesLength = meshFilter.sharedMesh.vertices.Length;
                        int uvsLength = uvs.Length;
;                       if (verticesLength == uvsLength)
                        {
                            meshFilter.sharedMesh.uv = uvs;
                        }
                        else
                        {
                            Debug.LogError("UV Length does not match vertices!");
                        }
                    }
                };

            }
        }

        private void GetAllChildTransformsWithMatchingName(UnityEngine.Transform go, string name, ref List<UnityEngine.Transform> children)
        {
            if (go.name == name)
            {
                children.Add(go);
            }

            for (int i = 0; i < go.childCount; ++i)
            {
                GetAllChildTransformsWithMatchingName(go.GetChild(i), name, ref children);
            }
        }


        private void ReloadFromUsd(UsdAsset stageRoot, bool forceRebuild)
        {
            stageRoot.Reload(forceRebuild);
            Repaint();
        }

        private void DestroyAllImportedObjects(UsdAsset stageRoot)
        {
            stageRoot.DestroyAllImportedObjects();
            Repaint();
        }

        private void DetachFromUsd(UsdAsset stageRoot)
        {
            stageRoot.RemoveAllUsdComponents();
            Repaint();
        }

        private void ReloadFromUsdAsCoroutine(UsdAsset stageRoot)
        {
            var options = new SceneImportOptions();
            stageRoot.StateToOptions(ref options);
            var parent = stageRoot.gameObject.transform.parent;
            var root = parent ? parent.gameObject : null;
            stageRoot.ImportUsdAsCoroutine(root, stageRoot.usdFullPath, stageRoot.m_usdTimeOffset, options,
                targetFrameMilliseconds: 5);
        }
    }
}
