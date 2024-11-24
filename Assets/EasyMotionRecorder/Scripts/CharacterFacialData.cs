using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("FaceAnimationRecorder")]
namespace Entum
{
    /// <summary>
    /// Stores facial animation data for character blend shapes
    /// </summary>
    public sealed class CharacterFacialData : ScriptableObject
    {
        [SerializeField]
        private List<SerializeHumanoidFace> _facials = new();

        /// <summary>
        /// Gets the read-only list of facial animation frames
        /// </summary>
        public IReadOnlyList<SerializeHumanoidFace> Facials => _facials;

        /// <summary>
        /// Adds a new facial animation frame
        /// </summary>
        /// <param name="face">The facial animation frame to add</param>
        /// <exception cref="ArgumentNullException">Thrown when face is null</exception>
        internal void AddFacial(SerializeHumanoidFace face)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));
            _facials.Add(face);
        }

        /// <summary>
        /// Clears all facial animation data
        /// </summary>
        internal void Clear() => _facials.Clear();

        /// <summary>
        /// Represents a single frame of facial animation data
        /// </summary>
        [Serializable]
        public sealed class SerializeHumanoidFace : IEquatable<SerializeHumanoidFace>, IPoolable
        {
            /// <summary>
            /// Represents blend shape data for a specific mesh
            /// </summary>
            [Serializable]
            public sealed class MeshAndBlendshape : IEquatable<MeshAndBlendshape>, IPoolable
            {
                [SerializeField]
                private string _path;
                
                [SerializeField]
                private float[] _blendShapes;

                /// <summary>
                /// Gets or sets the mesh path
                /// </summary>
                public string Path
                {
                    get => _path;
                    set => _path = value ?? throw new ArgumentNullException(nameof(value));
                }

                /// <summary>
                /// Gets or sets the blend shape weights
                /// </summary>
                public float[] BlendShapes
                {
                    get => _blendShapes;
                    set => _blendShapes = value ?? throw new ArgumentNullException(nameof(value));
                }

                public bool Equals(MeshAndBlendshape other)
                {
                    if (ReferenceEquals(null, other)) return false;
                    if (ReferenceEquals(this, other)) return true;

                    return string.Equals(_path, other._path) && 
                           (_blendShapes?.SequenceEqual(other._blendShapes) ?? other._blendShapes == null);
                }

                public override bool Equals(object obj) => 
                    ReferenceEquals(this, obj) || (obj is MeshAndBlendshape other && Equals(other));

                public override int GetHashCode()
                {
                    unchecked
                    {
                        return ((_path != null ? _path.GetHashCode() : 0) * 397) ^ 
                               (_blendShapes != null ? _blendShapes.GetHashCode() : 0);
                    }
                }

                public void Reset()
                {
                    _path = null;
                    _blendShapes = null;
                }

                public static bool operator ==(MeshAndBlendshape left, MeshAndBlendshape right) => 
                    Equals(left, right);

                public static bool operator !=(MeshAndBlendshape left, MeshAndBlendshape right) => 
                    !Equals(left, right);
            }

            [SerializeField]
            private List<MeshAndBlendshape> _smeshes = new();
            
            [SerializeField]
            private int _frameCount;
            
            [SerializeField]
            private float _time;

            /// <summary>
            /// Gets the read-only list of mesh blend shape data
            /// </summary>
            public IReadOnlyList<MeshAndBlendshape> Smeshes => _smeshes;

            /// <summary>
            /// Gets or sets the frame count
            /// </summary>
            public int FrameCount
            {
                get => _frameCount;
                set => _frameCount = value;
            }

            /// <summary>
            /// Gets or sets the elapsed time since recording started
            /// </summary>
            public float Time
            {
                get => _time;
                set => _time = value;
            }

            /// <summary>
            /// Gets the total number of blend shapes across all meshes
            /// </summary>
            public int BlendShapeNum() => 
                _smeshes.Count == 0 ? 0 : _smeshes.Sum(t => t.BlendShapes.Length);

            /// <summary>
            /// Creates a deep copy of the facial animation frame
            /// </summary>
            public SerializeHumanoidFace Clone()
            {
                var clone = new SerializeHumanoidFace
                {
                    _frameCount = _frameCount,
                    _time = _time
                };

                foreach (var smesh in _smeshes)
                {
                    var meshClone = new MeshAndBlendshape
                    {
                        Path = smesh.Path,
                        BlendShapes = new float[smesh.BlendShapes.Length]
                    };
                    Array.Copy(smesh.BlendShapes, meshClone.BlendShapes, smesh.BlendShapes.Length);
                    clone._smeshes.Add(meshClone);
                }

                return clone;
            }

            public bool Equals(SerializeHumanoidFace other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;

                return _frameCount == other._frameCount && 
                       Mathf.Approximately(_time, other._time) && 
                       _smeshes.SequenceEqual(other._smeshes);
            }

            public override bool Equals(object obj) => 
                ReferenceEquals(this, obj) || (obj is SerializeHumanoidFace other && Equals(other));

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = _frameCount;
                    hashCode = (hashCode * 397) ^ _time.GetHashCode();
                    hashCode = (hashCode * 397) ^ (_smeshes != null ? _smeshes.GetHashCode() : 0);
                    return hashCode;
                }
            }

            public void Reset()
            {
                _frameCount = 0;
                _time = 0;
                foreach (var smesh in _smeshes)
                {
                    smesh.Reset();
                }
                _smeshes.Clear();
            }

            public static bool operator ==(SerializeHumanoidFace left, SerializeHumanoidFace right) => 
                Equals(left, right);

            public static bool operator !=(SerializeHumanoidFace left, SerializeHumanoidFace right) => 
                !Equals(left, right);

            /// <summary>
            /// Adds a mesh blend shape data entry
            /// </summary>
            internal void AddMesh(MeshAndBlendshape mesh)
            {
                if (mesh == null) throw new ArgumentNullException(nameof(mesh));
                _smeshes.Add(mesh);
            }

            /// <summary>
            /// Clears all mesh blend shape data
            /// </summary>
            internal void ClearMeshes() => _smeshes.Clear();
        }
    }

    /// <summary>
    /// Interface for poolable objects that can be reset
    /// </summary>
    internal interface IPoolable
    {
        /// <summary>
        /// Resets the object's state for reuse
        /// </summary>
        void Reset();
    }
}
