/**
[EasyMotionRecorder]

Copyright (c) 2018 Duo.inc

This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/

using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.IO;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Entum
{
    /// <summary>
    /// Motion data recording class with optimized performance and modern input handling.
    /// Set to execution order 32000 to capture pose after VRIK processing.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class MotionDataRecorder : MonoBehaviour, IDisposable
    {
        #region Events
        public event Action OnRecordStart;
        public event Action OnRecordEnd;
        public event Action<float> OnFrameRecorded;
        #endregion

        #region Serialized Fields
        [Header("Input Settings")]
        [SerializeField]
        private InputAction _recordStartAction = new(binding: "<Keyboard>/r");
        
        [SerializeField]
        private InputAction _recordStopAction = new(binding: "<Keyboard>/x");

        [Header("Recording Settings")]
        [SerializeField]
        private Animator _animator;

        [SerializeField, Range(0, 120), Tooltip("Recording FPS. 0 means no limit")]
        private float _targetFPS = 60.0f;

        [Header("Bone Settings")]
        [SerializeField, Tooltip("OBJECTROOT for normal use")]
        private MotionDataSettings.Rootbonesystem _rootBoneSystem = MotionDataSettings.Rootbonesystem.Objectroot;
        
        [SerializeField, Tooltip("Used only when not OBJECTROOT")]
        private HumanBodyBones _targetRootBone = HumanBodyBones.Hips;
        
        [SerializeField]
        private HumanBodyBones _ikLeftFootBone = HumanBodyBones.LeftFoot;
        
        [SerializeField]
        private HumanBodyBones _ikRightFootBone = HumanBodyBones.RightFoot;
        #endregion

        #region Private Fields
        private readonly ObjectPool<HumanoidPoses.SerializeHumanoidPose> _posePool = new();
        private readonly ObjectPool<TransformData> _transformPool = new();
        private readonly ObjectPool<HumanPose> _humanPosePool = new();
        
        private HumanPoseHandler _poseHandler;
        private Transform _rootBoneTransform;
        private Transform _leftFootTransform;
        private Transform _rightFootTransform;
        private RecordingState _state;
        private bool _isDisposed;
        private bool _isInitialized;
        #endregion

        #region Properties
        public bool IsRecording => _state?.IsRecording ?? false;
        public float RecordedTime => _state?.RecordedTime ?? 0f;
        public int FrameCount => _state?.FrameCount ?? 0;
        public Animator CharacterAnimator => _animator;
        
        public float TargetFPS
        {
            get => _targetFPS;
            set => _targetFPS = Mathf.Clamp(value, 0f, 120f);
        }
        #endregion

        #region Unity Lifecycle
        private void OnEnable()
        {
            InitializeInputActions();
        }

        private void OnDisable()
        {
            DisableInputActions();
            StopRecording();
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            Dispose(true);
        }

        private void LateUpdate()
        {
            if (!_state.IsRecording || !_isInitialized) return;

            UpdateRecording();
        }
        #endregion

        #region Public Methods
        public void StartRecording()
        {
            if (!ValidateRecordingState()) return;

            _state.StartRecording();
            OnRecordStart?.Invoke();
        }

        public void StopRecording()
        {
            if (!_state.IsRecording) return;

            WriteAnimationFile();
            _state.StopRecording();
            OnRecordEnd?.Invoke();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public static DirectoryInfo SafeCreateDirectory(string path)
        {
            return Directory.Exists(path) ? null : Directory.CreateDirectory(path);
        }
        #endregion

        #region Private Methods
        private void Initialize()
        {
            if (!ValidateComponents()) return;

            try
            {
                _poseHandler = new HumanPoseHandler(_animator.avatar, _animator.transform);
                CacheTransforms();
                _state = new RecordingState();
                _isInitialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(MotionDataRecorder)}] Initialization failed: {e.Message}");
                enabled = false;
            }
        }

        private bool ValidateComponents()
        {
            if (_animator == null)
            {
                Debug.LogError($"[{nameof(MotionDataRecorder)}] Animator is required.");
                enabled = false;
                return false;
            }

            if (_animator.avatar == null || !_animator.avatar.isHuman)
            {
                Debug.LogError($"[{nameof(MotionDataRecorder)}] Humanoid avatar is required.");
                enabled = false;
                return false;
            }

            return true;
        }

        private void CacheTransforms()
        {
            _rootBoneTransform = _animator.GetBoneTransform(_targetRootBone);
            _leftFootTransform = _animator.GetBoneTransform(_ikLeftFootBone);
            _rightFootTransform = _animator.GetBoneTransform(_ikRightFootBone);
        }

        private void InitializeInputActions()
        {
            _recordStartAction.Enable();
            _recordStopAction.Enable();

            _recordStartAction.performed += _ => StartRecording();
            _recordStopAction.performed += _ => StopRecording();
        }

        private void DisableInputActions()
        {
            _recordStartAction.Disable();
            _recordStopAction.Disable();

            _recordStartAction.performed -= _ => StartRecording();
            _recordStopAction.performed -= _ => StopRecording();
        }

        private bool ValidateRecordingState()
        {
            if (_state.IsRecording)
            {
                Debug.LogWarning($"[{nameof(MotionDataRecorder)}] Recording is already in progress.");
                return false;
            }

            return true;
        }

        private void UpdateRecording()
        {
            _state.UpdateTime(Time.time);

            if (!ShouldRecordFrame()) return;

            RecordFrame();
            _state.IncrementFrame();
        }

        private bool ShouldRecordFrame()
        {
            if (_targetFPS <= 0) return true;

            var nextTime = (1.0f * (_state.FrameCount + 1)) / _targetFPS;
            if (nextTime > _state.RecordedTime) return false;

            if (_state.FrameCount % _targetFPS == 0)
            {
                var currentFPS = 1 / (_state.RecordedTime / _state.FrameCount);
                OnFrameRecorded?.Invoke(currentFPS);
            }

            return true;
        }

        private void RecordFrame()
        {
            var pose = _humanPosePool.Get();
            _poseHandler.GetHumanPose(ref pose);

            var serializedPose = _posePool.Get();
            try
            {
                RecordPoseData(serializedPose, pose);
                _state.Poses.Poses.Add(serializedPose);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(MotionDataRecorder)}] Failed to record frame: {e.Message}");
                _posePool.Release(serializedPose);
            }
            finally
            {
                _humanPosePool.Release(pose);
            }
        }

        private void RecordPoseData(HumanoidPoses.SerializeHumanoidPose serializedPose, HumanPose pose)
        {
            RecordRootMotion(serializedPose);
            RecordIKPositions(serializedPose, pose);
            RecordFrameData(serializedPose, pose);
            RecordBoneTransforms(serializedPose);
        }

        private void RecordRootMotion(HumanoidPoses.SerializeHumanoidPose serializedPose)
        {
            switch (_rootBoneSystem)
            {
                case MotionDataSettings.Rootbonesystem.Objectroot:
                    serializedPose.BodyRootPosition = _animator.transform.localPosition;
                    serializedPose.BodyRootRotation = _animator.transform.localRotation;
                    break;

                case MotionDataSettings.Rootbonesystem.Hipbone:
                    if (_rootBoneTransform != null)
                    {
                        serializedPose.BodyRootPosition = _rootBoneTransform.position;
                        serializedPose.BodyRootRotation = _rootBoneTransform.rotation;
                    }
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(_rootBoneSystem));
            }
        }

        private void RecordIKPositions(HumanoidPoses.SerializeHumanoidPose serializedPose, HumanPose pose)
        {
            var bodyTQ = new TransformData(pose.bodyPosition, pose.bodyRotation);
            var leftFootTQ = new TransformData(_leftFootTransform.position, _leftFootTransform.rotation);
            var rightFootTQ = new TransformData(_rightFootTransform.position, _rightFootTransform.rotation);

            leftFootTQ = AvatarUtility.GetIKGoalTQ(_animator.avatar, _animator.humanScale, AvatarIKGoal.LeftFoot, bodyTQ, leftFootTQ);
            rightFootTQ = AvatarUtility.GetIKGoalTQ(_animator.avatar, _animator.humanScale, AvatarIKGoal.RightFoot, bodyTQ, rightFootTQ);

            serializedPose.BodyPosition = bodyTQ.Position;
            serializedPose.BodyRotation = bodyTQ.Rotation;
            serializedPose.LeftfootIK_Pos = leftFootTQ.Position;
            serializedPose.LeftfootIK_Rot = leftFootTQ.Rotation;
            serializedPose.RightfootIK_Pos = rightFootTQ.Position;
            serializedPose.RightfootIK_Rot = rightFootTQ.Rotation;
        }

        private void RecordFrameData(HumanoidPoses.SerializeHumanoidPose serializedPose, HumanPose pose)
        {
            serializedPose.FrameCount = _state.FrameCount;
            serializedPose.Time = _state.RecordedTime;
            serializedPose.Muscles = new float[pose.muscles.Length];
            Array.Copy(pose.muscles, serializedPose.Muscles, pose.muscles.Length);
        }

        private void RecordBoneTransforms(HumanoidPoses.SerializeHumanoidPose pose)
        {
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone < 0 || bone >= HumanBodyBones.LastBone) continue;

                var transform = _animator.GetBoneTransform(bone);
                if (transform == null) continue;

                var boneData = new HumanoidPoses.SerializeHumanoidPose.HumanoidBone();
                boneData.Set(_animator.transform, transform);
                pose.HumanoidBones.Add(boneData);
            }
        }

        private void WriteAnimationFile()
        {
#if UNITY_EDITOR
            try
            {
                SafeCreateDirectory("Assets/Resources");
                var path = $"Assets/Resources/RecordMotion_{_animator.name}{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.asset";
                var uniquePath = AssetDatabase.GenerateUniqueAssetPath(path);

                AssetDatabase.CreateAsset(_state.Poses, uniquePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(MotionDataRecorder)}] Failed to write animation file: {e.Message}");
            }
#endif
        }

        private void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                _poseHandler?.Dispose();
                _posePool.Clear();
                _transformPool.Clear();
                _humanPosePool.Clear();
                DisableInputActions();
            }

            _isDisposed = true;
        }
        #endregion

        #region Helper Classes
        private sealed class RecordingState
        {
            public bool IsRecording { get; private set; }
            public float RecordedTime { get; private set; }
            public float StartTime { get; private set; }
            public int FrameCount { get; private set; }
            public HumanoidPoses Poses { get; private set; }

            public void StartRecording()
            {
                IsRecording = true;
                RecordedTime = 0f;
                StartTime = Time.time;
                FrameCount = 0;
                Poses = ScriptableObject.CreateInstance<HumanoidPoses>();
            }

            public void StopRecording()
            {
                IsRecording = false;
                Poses = null;
            }

            public void UpdateTime(float currentTime)
            {
                RecordedTime = currentTime - StartTime;
            }

            public void IncrementFrame()
            {
                FrameCount++;
            }
        }

        private sealed class ObjectPool<T> where T : class, new()
        {
            private readonly Stack<T> _pool = new();

            public T Get() => _pool.Count > 0 ? _pool.Pop() : new T();

            public void Release(T item)
            {
                if (item != null)
                {
                    _pool.Push(item);
                }
            }

            public void Clear() => _pool.Clear();
        }

        private readonly struct TransformData
        {
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }

            public TransformData(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }
        }
        #endregion
    }
}
