/**
[EasyMotionRecorder]

Copyright (c) 2018 Duo.inc

This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/

using UnityEngine;
using System;

namespace Entum
{
    /// <summary>
    /// Motion data playback class
    /// Please set Script Execution Order to a large value like 20000 for assets with physics-based movement
    /// like SpringBone, DynamicBone, BulletPhysicsImpl, etc.
    /// DefaultExecutionOrder(11000) is intended to make the processing order later than VRIK systems
    /// </summary>
    [DefaultExecutionOrder(11000)]
    public class MotionDataPlayer : MonoBehaviour
    {
        [SerializeField]
        private KeyCode _playStartKey = KeyCode.S;
        [SerializeField]
        private KeyCode _playStopKey = KeyCode.T;

        [SerializeField]
        protected HumanoidPoses RecordedMotionData;
        [SerializeField]
        private Animator _animator;

        [SerializeField, Tooltip("Specify the starting frame. 0 starts from the beginning of the file")]
        private int _startFrame;
        [SerializeField]
        private bool _playing;
        [SerializeField]
        private int _frameIndex;

        [SerializeField, Tooltip("OBJECTROOT is fine for normal use. Change only for special equipment")]
        private MotionDataSettings.Rootbonesystem _rootBoneSystem = MotionDataSettings.Rootbonesystem.Objectroot;
        [SerializeField, Tooltip("This parameter is not used when rootBoneSystem is OBJECTROOT")]
        private HumanBodyBones _targetRootBone = HumanBodyBones.Hips;

        private HumanPoseHandler _poseHandler;
        private Action _onPlayFinish;
        private float _playingTime;

        private void Awake()
        {
            if (_animator == null)
            {
                Debug.LogError("No animator set in MotionDataPlayer. Removing MotionDataPlayer.");
                Destroy(this);
                return;
            }

            _poseHandler = new HumanPoseHandler(_animator.avatar, _animator.transform);
            _onPlayFinish += StopMotion;
        }

        // Update is called once per frame
        private void Update()
        {
            if (Input.GetKeyDown(_playStartKey))
            {
                PlayMotion();
            }

            if (Input.GetKeyDown(_playStopKey))
            {
                StopMotion();
            }
        }

        private void LateUpdate()
        {
            if (!_playing)
            {
                return;
            }

            _playingTime += Time.deltaTime;
            SetHumanPose();
        }

        /// <summary>
        /// Start motion data playback
        /// </summary>
        private void PlayMotion()
        {
            if (_playing)
            {
                return;
            }

            if (RecordedMotionData == null)
            {
                Debug.LogError("No recorded motion data specified. Playback will not proceed.");
                return;
            }

            _playingTime = _startFrame * (Time.deltaTime / 1f);
            _frameIndex = _startFrame;
            _playing = true;
        }

        /// <summary>
        /// End motion data playback. Automatically called when frame count reaches the end
        /// </summary>
        private void StopMotion()
        {
            if (!_playing)
            {
                return;
            }

            _playingTime = 0f;
            _frameIndex = _startFrame;
            _playing = false;
        }

        private void SetHumanPose()
        {
            var pose = new HumanPose();
            pose.muscles = RecordedMotionData.Poses[_frameIndex].Muscles;
            _poseHandler.SetHumanPose(ref pose);
            pose.bodyPosition = RecordedMotionData.Poses[_frameIndex].BodyPosition;
            pose.bodyRotation = RecordedMotionData.Poses[_frameIndex].BodyRotation;

            switch (_rootBoneSystem)
            {
                case MotionDataSettings.Rootbonesystem.Objectroot:
                    //_animator.transform.localPosition = RecordedMotionData.Poses[_frameIndex].BodyRootPosition;
                    //_animator.transform.localRotation = RecordedMotionData.Poses[_frameIndex].BodyRootRotation;
                    break;

                case MotionDataSettings.Rootbonesystem.Hipbone:
                    pose.bodyPosition = RecordedMotionData.Poses[_frameIndex].BodyPosition;
                    pose.bodyRotation = RecordedMotionData.Poses[_frameIndex].BodyRotation;

                    _animator.GetBoneTransform(_targetRootBone).position = RecordedMotionData.Poses[_frameIndex].BodyRootPosition;
                    _animator.GetBoneTransform(_targetRootBone).rotation = RecordedMotionData.Poses[_frameIndex].BodyRootRotation;
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            // Adjust playback speed for motion data with frame drops
            if (_playingTime > RecordedMotionData.Poses[_frameIndex].Time)
            {
                _frameIndex++;
            }

            if (_frameIndex == RecordedMotionData.Poses.Count - 1)
            {
                if (_onPlayFinish != null)
                {
                    _onPlayFinish();
                }
            }
        }
    }
}
