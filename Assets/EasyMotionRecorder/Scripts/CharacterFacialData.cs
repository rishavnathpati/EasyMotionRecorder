using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Entum
{
    public class CharacterFacialData : ScriptableObject
    {
      
        [System.SerializableAttribute]
        public class SerializeHumanoidFace
        {
            public class MeshAndBlendshape
            {
                public string path;
                public float[] blendShapes;
            }

            public int BlendShapeNum()
            {
                return Smeshes.Count == 0 ? 0 : Smeshes.Sum(t => t.blendShapes.Length);
            }

            // Frame count
            public int FrameCount;

            // Elapsed time since recording started. For handling frame drops
            public float Time;

            public SerializeHumanoidFace(SerializeHumanoidFace serializeHumanoidFace)
            {
                for (int i = 0; i < serializeHumanoidFace.Smeshes.Count; i++)
                {
                    Smeshes.Add(serializeHumanoidFace.Smeshes[i]);
                    Array.Copy(serializeHumanoidFace.Smeshes[i].blendShapes,Smeshes[i].blendShapes,
                        serializeHumanoidFace.Smeshes[i].blendShapes.Length);

                }
                FrameCount = serializeHumanoidFace.FrameCount;
                Time = serializeHumanoidFace.Time;
            }
            // Within a single frame, individual meshes like mouth mesh and eye mesh are stored here
            public List<MeshAndBlendshape> Smeshes= new List<MeshAndBlendshape>();
            public SerializeHumanoidFace()
            {
            }
        }

        public List<SerializeHumanoidFace> Facials = new List<SerializeHumanoidFace>();
    }
}
