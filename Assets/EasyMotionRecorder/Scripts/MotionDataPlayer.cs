/**
[EasyMotionRecorder]

Copyright (c) 2018 Duo.inc

This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/

using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

namespace Entum
{
    /// <summary>
    /// Motion data playback class with optimized performance and modern input handling.
    /// Set Script Execution Order to 11000 to ensure processing after VRIK systems.
    /// For physics-based assets (SpringBone, DynamicBone, etc.), set to 20000.
    /// </summary>
    [DefaultExecutionOrder(11000)]
    public sealed class MotionDataPlayer : MonoBehaviour, IDisposable
    {
        #region Events
        public event Action OnPlaybackStarted;
        public event Action OnPlaybackStopped;
        public event Action OnPlaybackCompleted;
        #endregion

        #region Serialized Fields
        [Header("Input Settings")]
        [SerializeField]
        private InputAction _playAction = new InputAction(binding: "<Keyboard>/s");
        
        [SerializeField]
        private InputAction _stopAction = new InputAction(binding: "<Keyboard>/t");

        [Header("Motion Data")]
        [SerializeField]
        private HumanoidPoses _recordedMotionData;
        
        [SerializeField]
        private Animator _animator;

        [Header("Playback Settings")]
        [SerializeField, Tooltip("Specify the starting frame. 0 starts from the beginning")]
        private int _startFrame;

        [SerializeField, Tooltip("OBJECTROOT for normal use, change only for special equipment")]
        private MotionDataSettings.Rootbonesystem _rootBoneSystem = MotionDataSettings.Rootbonesystem.Objectroot;
        
        [SerializeField, Tooltip("Used only when rootBoneSystem is not OBJECTROOT")]
        private HumanBodyBones _targetRootBone = HumanBodyBones.Hips;
        #endregion

        #region Private Fields
        private HumanPoseHandler _poseHandler;
        private readonly ObjectPool<HumanPose> _posePool = new();
        private Transform _rootBoneTransform;
        private bool _isDisposed;
        private bool _isInitialized;
        
        private PlaybackState _state = new();
        #endregion

        #region Properties
        public bool IsPlaying => _state.IsPlaying;
        public float PlaybackTime => _state.PlayingTime;
        public int CurrentFrame => _state.FrameIndex;
        
        public HumanoidPoses RecordedMotionData
        {
            get => _recordedMotionData;
            set
            {
                _recordedMotionData = value;
                ValidateMotionData();
            }
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
            StopMotion();
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
            if (!_state.IsPlaying || !_isInitialized) return;
            
            UpdatePlayback();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Starts motion playback from the specified frame
        /// </summary>
        /// <param name="startFrame">Optional starting frame, defaults to configured start frame</param>
        public void Play(int? startFrame = null)
        {
            if (!ValidatePlaybackState()) return;

            _state.StartPlayback(startFrame ?? _startFrame);
            OnPlaybackStarted?.Invoke();
        }

        /// <summary>
        /// Stops motion playback
        /// </summary>
        public void Stop()
        {
            if (!_state.IsPlaying) return;

            _state.StopPlayback();
            OnPlaybackStopped?.Invoke();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Private Methods
        private void Initialize()
        {
            if (_animator == null)
            {
                Debug.LogError($"[{nameof(MotionDataPlayer)}] No animator assigned. Component will be removed.");
                Destroy(this);
                return;
            }

            try
            {
                _poseHandler = new HumanPoseHandler(_animator.avatar, _animator.transform);
                _rootBoneTransform = _animator.GetBoneTransform(_targetRootBone);
                ValidateMotionData();
                _isInitialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(MotionDataPlayer)}] Initialization failed: {e.Message}");
                Destroy(this);
            }
        }

        private void InitializeInputActions()
        {
            _playAction.Enable();
            _stopAction.Enable();

            _playAction.performed += _ => Play();
            _stopAction.performed += _ => Stop();
        }

        private void DisableInputActions()
        {
            _playAction.Disable();
            _stopAction.Disable();

            _playAction.performed -= _ => Play();
            _stopAction.performed -= _ => Stop();
        }

        private bool ValidatePlaybackState()
        {
            if (_state.IsPlaying)
            {
                Debug.LogWarning($"[{nameof(MotionDataPlayer)}] Playback is already in progress.");
                return false;
            }

            if (_recordedMotionData == null || _recordedMotionData.Poses.Count == 0)
            {
                Debug.LogError($"[{nameof(MotionDataPlayer)}] No valid motion data available for playback.");
                return false;
            }

            return true;
        }

        private void ValidateMotionData()
        {
            if (_recordedMotionData == null)
            {
                Debug.LogWarning($"[{nameof(MotionDataPlayer)}] No motion data assigned.");
                return;
            }

            if (_recordedMotionData.Poses.Count == 0)
            {
                Debug.LogWarning($"[{nameof(MotionDataPlayer)}] Motion data contains no poses.");
            }
        }

        private void UpdatePlayback()
        {
            _state.UpdatePlayingTime(Time.deltaTime);
            
            if (ShouldAdvanceFrame())
            {
                if (_state.FrameIndex >= _recordedMotionData.Poses.Count - 1)
                {
                    CompletePlayback();
                    return;
                }
                
                _state.AdvanceFrame();
            }

            ApplyPose();
        }

        private bool ShouldAdvanceFrame()
        {
            return _state.PlayingTime > _recordedMotionData.Poses[_state.FrameIndex].Time;
        }

        private void CompletePlayback()
        {
            Stop();
            OnPlaybackCompleted?.Invoke();
        }

        private void ApplyPose()
        {
            var pose = _posePool.Get();
            var currentPose = _recordedMotionData.Poses[_state.FrameIndex];

            pose.muscles = currentPose.Muscles;
            pose.bodyPosition = currentPose.BodyPosition;
            pose.bodyRotation = currentPose.BodyRotation;

            try
            {
                ApplyPoseToCharacter(pose, currentPose);
            }
            finally
            {
                _posePool.Release(pose);
            }
        }

        private void ApplyPoseToCharacter(HumanPose pose, HumanoidPoses.SerializeHumanoidPose currentPose)
        {
            _poseHandler.SetHumanPose(ref pose);

            if (_rootBoneSystem == MotionDataSettings.Rootbonesystem.Hipbone && _rootBoneTransform != null)
            {
                _rootBoneTransform.position = currentPose.BodyRootPosition;
                _rootBoneTransform.rotation = currentPose.BodyRootRotation;
            }
        }

        private void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                _poseHandler?.Dispose();
                _posePool.Clear();
                DisableInputActions();
            }

            _isDisposed = true;
        }
        #endregion

        #region Helper Classes
        private sealed class PlaybackState
        {
            public bool IsPlaying { get; private set; }
            public float PlayingTime { get; private set; }
            public int FrameIndex { get; private set; }

            public void StartPlayback(int startFrame)
            {
                IsPlaying = true;
                PlayingTime = startFrame * (Time.deltaTime / 1f);
                FrameIndex = startFrame;
            }

            public void StopPlayback()
            {
                IsPlaying = false;
                PlayingTime = 0f;
                FrameIndex = 0;
            }

            public void UpdatePlayingTime(float deltaTime)
            {
                PlayingTime += deltaTime;
            }

            public void AdvanceFrame()
            {
                FrameIndex++;
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
        #endregion
    }
}
