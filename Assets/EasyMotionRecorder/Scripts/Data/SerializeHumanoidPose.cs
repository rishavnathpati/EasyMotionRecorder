using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;

namespace Entum
{
    /// <summary>
    /// Represents a single frame of humanoid animation data
    /// </summary>
    [Serializable]
    public sealed class SerializeHumanoidPose : IEquatable<SerializeHumanoidPose>, IPoolable
    {
        #region Serialized Fields
        [SerializeField]
        private Vector3 _bodyRootPosition;
        
        [SerializeField]
        private Quaternion _bodyRootRotation = Quaternion.identity;
        
        [SerializeField]
        private Vector3 _bodyPosition;
        
        [SerializeField]
        private Quaternion _bodyRotation = Quaternion.identity;
        
        [SerializeField]
        private Vector3 _leftfootIKPos;
        
        [SerializeField]
        private Quaternion _leftfootIKRot = Quaternion.identity;
        
        [SerializeField]
        private Vector3 _rightfootIKPos;
        
        [SerializeField]
        private Quaternion _rightfootIKRot = Quaternion.identity;
        
        [SerializeField]
        private float[] _muscles = Array.Empty<float>();
        
        [SerializeField]
        private int _frameCount;
        
        [SerializeField]
        private float _time;
        
        [SerializeField]
        private List<HumanoidBone> _humanoidBones = new();
        #endregion

        #region Properties
        public Vector3 BodyRootPosition
        {
            get => _bodyRootPosition;
            set => _bodyRootPosition = value;
        }

        public Quaternion BodyRootRotation
        {
            get => _bodyRootRotation;
            set => _bodyRootRotation = value;
        }

        public Vector3 BodyPosition
        {
            get => _bodyPosition;
            set => _bodyPosition = value;
        }

        public Quaternion BodyRotation
        {
            get => _bodyRotation;
            set => _bodyRotation = value;
        }

        public Vector3 LeftfootIK_Pos
        {
            get => _leftfootIKPos;
            set => _leftfootIKPos = value;
        }

        public Quaternion LeftfootIK_Rot
        {
            get => _leftfootIKRot;
            set => _leftfootIKRot = value;
        }

        public Vector3 RightfootIK_Pos
        {
            get => _rightfootIKPos;
            set => _rightfootIKPos = value;
        }

        public Quaternion RightfootIK_Rot
        {
            get => _rightfootIKRot;
            set => _rightfootIKRot = value;
        }

        public float[] Muscles
        {
            get => _muscles;
            set => _muscles = value ?? throw new ArgumentNullException(nameof(value));
        }

        public int FrameCount
        {
            get => _frameCount;
            set => _frameCount = value;
        }

        public float Time
        {
            get => _time;
            set => _time = value;
        }

        public IReadOnlyList<HumanoidBone> HumanoidBones => _humanoidBones;
        #endregion

        #region Public Methods
        /// <summary>
        /// Creates a deep copy of the pose
        /// </summary>
        public SerializeHumanoidPose Clone()
        {
            var clone = new SerializeHumanoidPose
            {
                _bodyRootPosition = _bodyRootPosition,
                _bodyRootRotation = _bodyRootRotation,
                _bodyPosition = _bodyPosition,
                _bodyRotation = _bodyRotation,
                _leftfootIKPos = _leftfootIKPos,
                _leftfootIKRot = _leftfootIKRot,
                _rightfootIKPos = _rightfootIKPos,
                _rightfootIKRot = _rightfootIKRot,
                _frameCount = _frameCount,
                _time = _time
            };

            clone._muscles = new float[_muscles.Length];
            Array.Copy(_muscles, clone._muscles, _muscles.Length);

            foreach (var bone in _humanoidBones)
            {
                clone._humanoidBones.Add(bone.Clone());
            }

            return clone;
        }

        /// <summary>
        /// Adds a humanoid bone to the pose
        /// </summary>
        public void AddBone(HumanoidBone bone)
        {
            if (bone == null) throw new ArgumentNullException(nameof(bone));
            _humanoidBones.Add(bone);
        }

        /// <summary>
        /// Serializes the pose data to CSV format
        /// </summary>
        public string SerializeCSV()
        {
            var sb = new StringBuilder(1024);
            SerializeVector3(sb, _bodyRootPosition);
            SerializeQuaternion(sb, _bodyRootRotation);
            SerializeVector3(sb, _bodyPosition);
            SerializeQuaternion(sb, _bodyRotation);
            
            foreach (var muscle in _muscles)
            {
                sb.Append(muscle.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
            }
            
            sb.Append(_frameCount);
            sb.Append(',');
            sb.Append(_time.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            
            foreach (var bone in _humanoidBones)
            {
                sb.Append(bone.Name);
                sb.Append(',');
                SerializeVector3(sb, bone.LocalPosition);
                SerializeQuaternion(sb, bone.LocalRotation);
            }
            
            sb.Length--; // Remove last comma
            return sb.ToString();
        }

        /// <summary>
        /// Deserializes pose data from CSV format
        /// </summary>
        public void DeserializeCSV(string data)
        {
            if (string.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));

            try
            {
                var values = data.Split(',');
                int index = 0;

                _bodyRootPosition = DeserializeVector3(values, ref index);
                _bodyRootRotation = DeserializeQuaternion(values, ref index);
                _bodyPosition = DeserializeVector3(values, ref index);
                _bodyRotation = DeserializeQuaternion(values, ref index);

                _muscles = new float[HumanTrait.MuscleCount];
                for (int i = 0; i < HumanTrait.MuscleCount; i++)
                {
                    _muscles[i] = float.Parse(values[index++], CultureInfo.InvariantCulture);
                }

                _frameCount = int.Parse(values[index++], CultureInfo.InvariantCulture);
                _time = float.Parse(values[index++], CultureInfo.InvariantCulture);

                _humanoidBones.Clear();
                while (index < values.Length - 7) // Each bone needs 8 values (name + position + rotation)
                {
                    var bone = new HumanoidBone
                    {
                        Name = values[index++],
                        LocalPosition = DeserializeVector3(values, ref index),
                        LocalRotation = DeserializeQuaternion(values, ref index)
                    };
                    _humanoidBones.Add(bone);
                }
            }
            catch (Exception e)
            {
                throw new FormatException($"Failed to deserialize pose data: {e.Message}", e);
            }
        }

        public void Reset()
        {
            _bodyRootPosition = Vector3.zero;
            _bodyRootRotation = Quaternion.identity;
            _bodyPosition = Vector3.zero;
            _bodyRotation = Quaternion.identity;
            _leftfootIKPos = Vector3.zero;
            _leftfootIKRot = Quaternion.identity;
            _rightfootIKPos = Vector3.zero;
            _rightfootIKRot = Quaternion.identity;
            _frameCount = 0;
            _time = 0;
            Array.Clear(_muscles, 0, _muscles.Length);
            _humanoidBones.Clear();
        }

        public bool Equals(SerializeHumanoidPose other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return _bodyRootPosition.Equals(other._bodyRootPosition) &&
                   _bodyRootRotation.Equals(other._bodyRootRotation) &&
                   _bodyPosition.Equals(other._bodyPosition) &&
                   _bodyRotation.Equals(other._bodyRotation) &&
                   _leftfootIKPos.Equals(other._leftfootIKPos) &&
                   _leftfootIKRot.Equals(other._leftfootIKRot) &&
                   _rightfootIKPos.Equals(other._rightfootIKPos) &&
                   _rightfootIKRot.Equals(other._rightfootIKRot) &&
                   _frameCount == other._frameCount &&
                   _time.Equals(other._time);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || (obj is SerializeHumanoidPose other && Equals(other));
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = _bodyRootPosition.GetHashCode();
                hashCode = (hashCode * 397) ^ _bodyRootRotation.GetHashCode();
                hashCode = (hashCode * 397) ^ _bodyPosition.GetHashCode();
                hashCode = (hashCode * 397) ^ _bodyRotation.GetHashCode();
                hashCode = (hashCode * 397) ^ _leftfootIKPos.GetHashCode();
                hashCode = (hashCode * 397) ^ _leftfootIKRot.GetHashCode();
                hashCode = (hashCode * 397) ^ _rightfootIKPos.GetHashCode();
                hashCode = (hashCode * 397) ^ _rightfootIKRot.GetHashCode();
                hashCode = (hashCode * 397) ^ _frameCount;
                hashCode = (hashCode * 397) ^ _time.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(SerializeHumanoidPose left, SerializeHumanoidPose right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(SerializeHumanoidPose left, SerializeHumanoidPose right)
        {
            return !Equals(left, right);
        }
        #endregion

        #region Private Methods
        private static void SerializeVector3(StringBuilder sb, Vector3 vec)
        {
            sb.Append(vec.x.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(vec.y.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(vec.z.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
        }

        private static void SerializeQuaternion(StringBuilder sb, Quaternion q)
        {
            sb.Append(q.x.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(q.y.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(q.z.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            sb.Append(q.w.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
        }

        private static Vector3 DeserializeVector3(string[] values, ref int index)
        {
            return new Vector3(
                float.Parse(values[index++], CultureInfo.InvariantCulture),
                float.Parse(values[index++], CultureInfo.InvariantCulture),
                float.Parse(values[index++], CultureInfo.InvariantCulture)
            );
        }

        private static Quaternion DeserializeQuaternion(string[] values, ref int index)
        {
            return new Quaternion(
                float.Parse(values[index++], CultureInfo.InvariantCulture),
                float.Parse(values[index++], CultureInfo.InvariantCulture),
                float.Parse(values[index++], CultureInfo.InvariantCulture),
                float.Parse(values[index++], CultureInfo.InvariantCulture)
            );
        }
        #endregion
    }
}
