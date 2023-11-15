// Copyright 2021 Unity Technologies. All rights reserved.
// Copyright 2017 Google Inc. All rights reserved.
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

using System.Runtime.Serialization;
using UnityEngine;

namespace USD.NET.Unity
{
    /// <summary>
    /// The following is based on the Pixar specification found here:
    /// https://graphics.pixar.com/usd/docs/UsdPreviewSurface-Proposal.html
    /// </summary>
    [System.Serializable]
    [UsdSchema("Shader")]
    public class MtlxSurfaceSample : ShaderSample
    {
        public MtlxSurfaceSample()
        {
            id = new pxr.TfToken("MtlxPreviewSurface");
        }

        [InputParameter("_BaseColor")]
        public Connectable<Vector3> base_color = new Connectable<Vector3>(new Vector3(0.18f, 0.18f, 0.18f));

        [UsdSchema("hmtlxcolorcorrect1")]
        public class Hmtlxcolorcorrect1 : SampleBase
        {
            [InputParameter("_In")]
            [DataMember(Name = "in")]
            public Connectable<Vector3> In = new Connectable<Vector3>(new Vector3(0.18f, 0.18f, 0.18f));
        }

        public Hmtlxcolorcorrect1 hmtlxcolorcorrect1 = new Hmtlxcolorcorrect1();
    }
}
