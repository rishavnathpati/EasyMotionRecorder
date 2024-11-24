/**
[EasyMotionRecorder]

Copyright (c) 2018 Duo.inc

This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
#endif

[assembly: InternalsVisibleTo("MotionDataRecorder")]
[assembly: InternalsVisibleTo("MotionDataPlayer")]
namespace Entum
{
    /// <summary>
    /// Stores and manages humanoid animation pose data with optimized performance
    /// </summary>
    public sealed class HumanoidPoses : ScriptableObject, ISerializationCallbackReceiver
    {
        #region Serialized Fields
        [SerializeField]
        private List<SerializeHumanoidPose> _poses = new();

        [SerializeField]
        private float _frameRate = 30f;

        [SerializeField]
        private bool _loopTime;
        #endregion

        #region Properties
        public IReadOnlyList<SerializeHumanoidPose> Poses => _poses;
        public float FrameRate
        {
            get => _frameRate;
            set => _frameRate = Mathf.Clamp(value, 1f, 120f);
        }
        public bool LoopTime
        {
            get => _loopTime;
            set => _loopTime = value;
        }
        #endregion

        #region Events
        public event Action<SerializeHumanoidPose> OnPoseAdded;
        public event Action OnPosesCleared;
        #endregion

        #region Public Methods
        /// <summary>
        /// Adds a new pose to the animation data
        /// </summary>
        internal void AddPose(SerializeHumanoidPose pose)
        {
            if (pose == null) throw new ArgumentNullException(nameof(pose));
            _poses.Add(pose);
            OnPoseAdded?.Invoke(pose);
        }

        /// <summary>
        /// Clears all recorded poses
        /// </summary>
        public void Clear()
        {
            _poses.Clear();
            OnPosesCleared?.Invoke();
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Exports the animation data as a generic animation clip
        /// </summary>
        [ContextMenu("Export as Generic animation clips")]
        public void ExportGenericAnim()
        {
            if (_poses.Count == 0)
            {
                Debug.LogWarning("No poses to export");
                return;
            }

            try
            {
                var clip = CreateAnimationClip();
                ExportGenericAnimationCurves(clip);
                SaveAnimationClip(clip, "Generic");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to export generic animation: {e.Message}");
            }
        }

        /// <summary>
        /// Exports the animation data as a humanoid animation clip
        /// </summary>
        [ContextMenu("Export as Humanoid animation clips")]
        public void ExportHumanoidAnim()
        {
            if (_poses.Count == 0)
            {
                Debug.LogWarning("No poses to export");
                return;
            }

            try
            {
                var clip = CreateAnimationClip();
                ExportHumanoidAnimationCurves(clip);
                SaveAnimationClip(clip, "Humanoid");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to export humanoid animation: {e.Message}");
            }
        }
        #endif
        #endregion

        #region Private Methods
        #if UNITY_EDITOR
        private AnimationClip CreateAnimationClip()
        {
            var clip = new AnimationClip { frameRate = _frameRate };
            AnimationUtility.SetAnimationClipSettings(clip, new AnimationClipSettings { loopTime = _loopTime });
            return clip;
        }

        private void ExportGenericAnimationCurves(AnimationClip clip)
        {
            if (_poses.Count == 0) return;

            var bones = _poses[0].HumanoidBones;
            foreach (var bone in bones)
            {
                ExportTransformCurves(clip, bone);
            }

            clip.EnsureQuaternionContinuity();
        }

        private void ExportHumanoidAnimationCurves(AnimationClip clip)
        {
            ExportBodyPositionCurves(clip);
            ExportBodyRotationCurves(clip);
            ExportFootIKCurves(clip);
            ExportMuscleCurves(clip);
            clip.EnsureQuaternionContinuity();
        }

        private void ExportTransformCurves(AnimationClip clip, SerializeHumanoidPose.HumanoidBone bone)
        {
            var curves = new Dictionary<string, AnimationCurve>
            {
                { "m_LocalPosition.x", new AnimationCurve() },
                { "m_LocalPosition.y", new AnimationCurve() },
                { "m_LocalPosition.z", new AnimationCurve() },
                { "m_LocalRotation.x", new AnimationCurve() },
                { "m_LocalRotation.y", new AnimationCurve() },
                { "m_LocalRotation.z", new AnimationCurve() },
                { "m_LocalRotation.w", new AnimationCurve() }
            };

            foreach (var pose in _poses)
            {
                var bonePose = pose.HumanoidBones.Find(b => b.Name == bone.Name);
                if (bonePose == null) continue;

                curves["m_LocalPosition.x"].AddKey(pose.Time, bonePose.LocalPosition.x);
                curves["m_LocalPosition.y"].AddKey(pose.Time, bonePose.LocalPosition.y);
                curves["m_LocalPosition.z"].AddKey(pose.Time, bonePose.LocalPosition.z);
                curves["m_LocalRotation.x"].AddKey(pose.Time, bonePose.LocalRotation.x);
                curves["m_LocalRotation.y"].AddKey(pose.Time, bonePose.LocalRotation.y);
                curves["m_LocalRotation.z"].AddKey(pose.Time, bonePose.LocalRotation.z);
                curves["m_LocalRotation.w"].AddKey(pose.Time, bonePose.LocalRotation.w);
            }

            foreach (var curve in curves)
            {
                AnimationUtility.SetEditorCurve(clip, new EditorCurveBinding
                {
                    path = bone.Name,
                    type = typeof(Transform),
                    propertyName = curve.Key
                }, curve.Value);
            }
        }

        private void ExportBodyPositionCurves(AnimationClip clip)
        {
            var curves = new Dictionary<string, AnimationCurve>
            {
                { "RootT.x", new AnimationCurve() },
                { "RootT.y", new AnimationCurve() },
                { "RootT.z", new AnimationCurve() }
            };

            foreach (var pose in _poses)
            {
                curves["RootT.x"].AddKey(pose.Time, pose.BodyPosition.x);
                curves["RootT.y"].AddKey(pose.Time, pose.BodyPosition.y);
                curves["RootT.z"].AddKey(pose.Time, pose.BodyPosition.z);
            }

            foreach (var curve in curves)
            {
                clip.SetCurve("", typeof(Animator), curve.Key, curve.Value);
            }
        }

        private void ExportBodyRotationCurves(AnimationClip clip)
        {
            var curves = new Dictionary<string, AnimationCurve>
            {
                { "RootQ.x", new AnimationCurve() },
                { "RootQ.y", new AnimationCurve() },
                { "RootQ.z", new AnimationCurve() },
                { "RootQ.w", new AnimationCurve() }
            };

            foreach (var pose in _poses)
            {
                curves["RootQ.x"].AddKey(pose.Time, pose.BodyRotation.x);
                curves["RootQ.y"].AddKey(pose.Time, pose.BodyRotation.y);
                curves["RootQ.z"].AddKey(pose.Time, pose.BodyRotation.z);
                curves["RootQ.w"].AddKey(pose.Time, pose.BodyRotation.w);
            }

            foreach (var curve in curves)
            {
                clip.SetCurve("", typeof(Animator), curve.Key, curve.Value);
            }
        }

        private void ExportFootIKCurves(AnimationClip clip)
        {
            ExportFootPositionCurves(clip, "LeftFootT", p => p.LeftfootIK_Pos);
            ExportFootPositionCurves(clip, "RightFootT", p => p.RightfootIK_Pos);
            ExportFootRotationCurves(clip, "LeftFootQ", p => p.LeftfootIK_Rot);
            ExportFootRotationCurves(clip, "RightFootQ", p => p.RightfootIK_Rot);
        }

        private void ExportFootPositionCurves(AnimationClip clip, string prefix, Func<SerializeHumanoidPose, Vector3> getter)
        {
            var curves = new Dictionary<string, AnimationCurve>
            {
                { $"{prefix}.x", new AnimationCurve() },
                { $"{prefix}.y", new AnimationCurve() },
                { $"{prefix}.z", new AnimationCurve() }
            };

            foreach (var pose in _poses)
            {
                var position = getter(pose);
                curves[$"{prefix}.x"].AddKey(pose.Time, position.x);
                curves[$"{prefix}.y"].AddKey(pose.Time, position.y);
                curves[$"{prefix}.z"].AddKey(pose.Time, position.z);
            }

            foreach (var curve in curves)
            {
                clip.SetCurve("", typeof(Animator), curve.Key, curve.Value);
            }
        }

        private void ExportFootRotationCurves(AnimationClip clip, string prefix, Func<SerializeHumanoidPose, Quaternion> getter)
        {
            var curves = new Dictionary<string, AnimationCurve>
            {
                { $"{prefix}.x", new AnimationCurve() },
                { $"{prefix}.y", new AnimationCurve() },
                { $"{prefix}.z", new AnimationCurve() },
                { $"{prefix}.w", new AnimationCurve() }
            };

            foreach (var pose in _poses)
            {
                var rotation = getter(pose);
                curves[$"{prefix}.x"].AddKey(pose.Time, rotation.x);
                curves[$"{prefix}.y"].AddKey(pose.Time, rotation.y);
                curves[$"{prefix}.z"].AddKey(pose.Time, rotation.z);
                curves[$"{prefix}.w"].AddKey(pose.Time, rotation.w);
            }

            foreach (var curve in curves)
            {
                clip.SetCurve("", typeof(Animator), curve.Key, curve.Value);
            }
        }

        private void ExportMuscleCurves(AnimationClip clip)
        {
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                var curve = new AnimationCurve();
                foreach (var pose in _poses)
                {
                    curve.AddKey(pose.Time, pose.Muscles[i]);
                }

                var muscleName = HumanTrait.MuscleName[i];
                if (MotionDataSettings.Instance.TraitPropMap.TryGetValue(muscleName, out var mappedName))
                {
                    muscleName = mappedName;
                }

                clip.SetCurve("", typeof(Animator), muscleName, curve);
            }
        }

        private void SaveAnimationClip(AnimationClip clip, string type)
        {
            var path = $"Assets/Resources/RecordMotion_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_{type}.anim";
            var uniquePath = AssetDatabase.GenerateUniqueAssetPath(path);

            AssetDatabase.CreateAsset(clip, uniquePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Animation saved to: {uniquePath}");
        }
        #endif

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            // Implement if needed
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            // Implement if needed
        }
        #endregion
    }
}
