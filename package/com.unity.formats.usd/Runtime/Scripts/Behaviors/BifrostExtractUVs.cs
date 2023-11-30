using pxr;
using UnityEngine;
using USD.NET;

namespace Unity.Formats.USD
{
    public class BifrostExtractUVs : MonoBehaviour
    {
        void Start()
        {
            GetUVs();
        }

        public void GetUVs()
        {
            var stageRoot = GetComponentInParent<UsdAsset>();

            if (!stageRoot)
            {
                stageRoot = GetComponent<UsdAsset>();
            }

            if (!stageRoot)
            {
                Debug.Log("No UsdAsset found!");
                return;
            }

            string usdPrimpath = GetComponent<UsdPrimSource>().m_usdPrimPath;

            Scene scene = stageRoot.GetScene();
            UsdPrim prim = scene.GetPrimAtPath(usdPrimpath);

            VtValue val = new VtValue();
            TfToken attriname = new TfToken("primvars:st");

            double usdTime = scene.Time.GetValueOrDefault();

            if (prim.GetAttributeValue(attriname, val, usdTime))
            {
                Debug.Log("Is array typed : " + val.IsArrayValued());
                Debug.Log("Is typed : " + val.GetTypeName());
                Debug.Log("Array Size : " + val.GetArraySize());

                VtVec2fArray Vec2dArray = new VtVec2fArray(val.GetArraySize());

                if (val.CanCastToTypeOf(Vec2dArray))
                {
                    UsdCs.VtValueToVtVec2fArray(val, Vec2dArray);
                    Debug.Log("VtArray Size : " + Vec2dArray.size());

                    uint size = Vec2dArray.size();

                    Vector2[] uvs = new Vector2[size];

                    for (int i = 0; i < size; ++i)
                    {
                        Vector2 vector2D = new Vector2(Vec2dArray[i][0], Vec2dArray[i][1]);
                        uvs[i] = vector2D;
                    }

                    MeshFilter meshFilter = GetComponent<MeshFilter>();
                    meshFilter.sharedMesh.uv = uvs;
                }
            };


            //foreach (UsdAttribute attr in prim.GetAttributes())
            //{
            //    bool hasAuthoredValue = attr.HasAuthoredValueOpinion();
            //    bool hasValue = attr.HasValue();
            //    bool isSelected = GUI.GetNameOfFocusedControl() == attr.GetName();
            //    string displayName = attr.GetName();
            //    Debug.Log("UsdAttribute : " + displayName);

            //    double usdTime = scene.Time.GetValueOrDefault();
            //    VtValue defaultVal = attr.Get(UsdTimeCode.Default());

            //    VtFloatArray floatArray = new VtFloatArray();
            //    if (defaultVal.CanCastToTypeOf(floatArray))
            //    {
            //        floatArray = UsdCs.VtValueToVtFloatArray(defaultVal);
            //    }

            //}
        }

        object GetCSharpValue(UsdAttribute attr, UsdTimeCode time)
        {
            UsdTypeBinding binding;
            if (!UsdIo.Bindings.GetReverseBinding(attr.GetTypeName(), out binding))
            {
                return null;
            }

            return binding.toCsObject(attr.Get(time));
        }

        void WalkNodes(PcpNodeRef node)
        {
            string arctype = "[" + node.GetArcType().ToString().Replace("PcpArcType", "") + "] < " + node.GetPath() + ">";
            Debug.Log("Arctype : " + arctype);

            WalkLayers(node, node.GetLayerStack().GetLayerTree(), 1);
            foreach (PcpNodeRef child in node.GetChildren())
            {
                WalkNodes(child);
            }
        }

        void WalkLayers(PcpNodeRef node, SdfLayerTreeHandle tree, int indent)
        {
            string identifier = tree.GetLayer().GetIdentifier();
            Debug.Log("Identifier : " + identifier);

            foreach (var childTree in tree.GetChildTrees())
            {
                WalkLayers(node, childTree, indent++);
            }
        }
    }
}
