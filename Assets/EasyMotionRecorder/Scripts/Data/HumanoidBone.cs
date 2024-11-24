using UnityEngine;
using System;
using System.Collections.Generic;

namespace Entum
{
    /// <summary>
    /// Represents a single humanoid bone's transform data
    /// </summary>
    [Serializable]
    public sealed class HumanoidBone : IEquatable<HumanoidBone>, IPoolable
    {
        #region Serialized Fields
        [SerializeField]
        private string _name;
        
        [SerializeField]
        private Vector3 _localPosition;
        
        [SerializeField]
        private Quaternion _localRotation = Quaternion.identity;
        #endregion

        #region Static Fields
        private static readonly Dictionary<Transform, string> PathCache = new();
        #endregion

        #region Properties
        public string Name
        {
            get => _name;
            set => _name = value ?? throw new ArgumentNullException(nameof(value));
        }

        public Vector3 LocalPosition
        {
            get => _localPosition;
            set => _localPosition = value;
        }

        public Quaternion LocalRotation
        {
            get => _localRotation;
            set => _localRotation = value;
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Sets the bone's transform data relative to the root transform
        /// </summary>
        public void Set(Transform root, Transform target)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (target == null) throw new ArgumentNullException(nameof(target));

            _name = BuildRelativePath(root, target);
            _localPosition = target.localPosition;
            _localRotation = target.localRotation;
        }

        /// <summary>
        /// Creates a deep copy of the bone data
        /// </summary>
        public HumanoidBone Clone()
        {
            return new HumanoidBone
            {
                _name = _name,
                _localPosition = _localPosition,
                _localRotation = _localRotation
            };
        }

        /// <summary>
        /// Applies the bone's transform data to a target transform
        /// </summary>
        public void ApplyTo(Transform target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));

            target.localPosition = _localPosition;
            target.localRotation = _localRotation;
        }

        public void Reset()
        {
            _name = null;
            _localPosition = Vector3.zero;
            _localRotation = Quaternion.identity;
        }

        public bool Equals(HumanoidBone other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return string.Equals(_name, other._name) &&
                   _localPosition.Equals(other._localPosition) &&
                   _localRotation.Equals(other._localRotation);
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || (obj is HumanoidBone other && Equals(other));
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (_name != null ? _name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ _localPosition.GetHashCode();
                hashCode = (hashCode * 397) ^ _localRotation.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(HumanoidBone left, HumanoidBone right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(HumanoidBone left, HumanoidBone right)
        {
            return !Equals(left, right);
        }

        /// <summary>
        /// Clears the path cache
        /// </summary>
        public static void ClearPathCache()
        {
            PathCache.Clear();
        }
        #endregion

        #region Private Methods
        /// <summary>
        /// Builds and caches the relative path from root to target transform
        /// </summary>
        private static string BuildRelativePath(Transform root, Transform target)
        {
            // Check cache first
            if (PathCache.TryGetValue(target, out var cachedPath))
            {
                return cachedPath;
            }

            // Build path
            var path = new System.Text.StringBuilder(100);
            var current = target;

            while (true)
            {
                if (current == null)
                {
                    throw new InvalidOperationException($"Transform '{target.name}' is not a child of '{root.name}'");
                }

                if (current == root)
                {
                    break;
                }

                if (path.Length > 0)
                {
                    path.Insert(0, '/');
                }
                path.Insert(0, current.name);

                current = current.parent;
            }

            var result = path.ToString();
            PathCache[target] = result;
            return result;
        }
        #endregion
    }

    /// <summary>
    /// Interface for objects that can be reset and reused in object pools
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Resets the object's state for reuse
        /// </summary>
        void Reset();
    }
}
