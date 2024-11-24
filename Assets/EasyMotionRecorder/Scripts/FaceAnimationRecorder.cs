/**
[EasyMotionRecorder]

Copyright (c) 2018 Duo.inc

This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Entum
{
    /// <summary>
    /// Class for recording blendshape movements
    /// Since lip sync is likely to be added later on the Timeline with AudioClip,
    /// you can register Exclusive (excluded) Blendshape names.
    /// </summary>
    [RequireComponent(typeof(MotionDataRecorder))]
    public sealed class FaceAnimationRecorder : MonoBehaviour, IDisposable
    {
        #region Serialized Fields
        [Header("Recording Settings")]
        [SerializeField, Tooltip("Set to true if you want to record facial expressions simultaneously")]
        private bool _recordFaceBlendshapes;

        [SerializeField, Tooltip("Add morph names here if you don't want to record lip sync, e.g., face_mouse_e etc.")]
        private HashSet<string> _exclusiveBlendshapeNames = new();

        [SerializeField, Range(0, 120), Tooltip("Recording FPS. 0 means no limit. Cannot exceed Update FPS.")]
        private float _targetFPS = 60.0f;
        #endregion

        #region Private Fields
        private readonly StringBuilder _pathBuilder = new(100);
        private readonly List<SkinnedMeshRenderer> _meshPool = new();
        private readonly ObjectPool<CharacterFacialData.SerializeHumanoidFace> _facePool = new();
        private readonly ObjectPool<CharacterFacialData.SerializeHumanoidFace.MeshAndBlendshape> _meshBlendPool = new();

        private MotionDataRecorder _animRecorder;
        private SkinnedMeshRenderer[] _smeshs;
        private CharacterFacialData _facialData;
        private CharacterFacialData.SerializeHumanoidFace _past;
        
        private bool _recording;
        private int _frameCount;
        private float _recordedTime;
        private float _startTime;
        private bool _isDisposed;
        #endregion

        #region Properties
        public float TargetFPS
        {
            get => _targetFPS;
            set => _targetFPS = Mathf.Clamp(value, 0f, 120f);
        }
        #endregion

        #region Unity Lifecycle
        private void OnEnable()
        {
            InitializeComponents();
            SubscribeToEvents();
        }

        private void OnDisable()
        {
            if (_recording)
            {
                RecordEnd();
            }
            UnsubscribeFromEvents();
        }

        private void LateUpdate()
        {
            if (!_recording) return;

            UpdateRecording();
        }

        private void OnDestroy()
        {
            Dispose(true);
        }
        #endregion

        #region Public Methods
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        #region Private Methods
        private void InitializeComponents()
        {
            _animRecorder = GetComponent<MotionDataRecorder>();
            if (_animRecorder?.CharacterAnimator != null)
            {
                _smeshs = GetSkinnedMeshRenderers(_animRecorder.CharacterAnimator);
            }
        }

        private void SubscribeToEvents()
        {
            if (_animRecorder == null) return;
            _animRecorder.OnRecordStart += RecordStart;
            _animRecorder.OnRecordEnd += RecordEnd;
        }

        private void UnsubscribeFromEvents()
        {
            if (_animRecorder == null) return;
            _animRecorder.OnRecordStart -= RecordStart;
            _animRecorder.OnRecordEnd -= RecordEnd;
        }

        private SkinnedMeshRenderer[] GetSkinnedMeshRenderers(Animator root)
        {
            if (root == null) return Array.Empty<SkinnedMeshRenderer>();

            _meshPool.Clear();
            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>();
            
            foreach (var renderer in renderers)
            {
                if (renderer?.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 0)
                {
                    _meshPool.Add(renderer);
                }
            }

            return _meshPool.ToArray();
        }

        private void RecordStart()
        {
            if (!_recordFaceBlendshapes || _recording) return;
            if (_smeshs == null || _smeshs.Length == 0)
            {
                Debug.LogError($"[{nameof(FaceAnimationRecorder)}] No facial mesh specified, facial animation will not be recorded");
                return;
            }

            Debug.Log($"[{nameof(FaceAnimationRecorder)}] Record start");
            InitializeRecording();
        }

        private void InitializeRecording()
        {
            _recording = true;
            _recordedTime = 0f;
            _startTime = Time.time;
            _frameCount = 0;
            _facialData = ScriptableObject.CreateInstance<CharacterFacialData>();
            _past = _facePool.Get();
        }

        private void RecordEnd()
        {
            if (!_recordFaceBlendshapes) return;

            if (_smeshs == null || _smeshs.Length == 0)
            {
                Debug.LogError($"[{nameof(FaceAnimationRecorder)}] No facial mesh specified, facial animation was not recorded");
                if (_recording)
                {
                    Debug.LogError($"[{nameof(FaceAnimationRecorder)}] Unexpected execution state");
                }
            }
            else
            {
                ExportFacialAnimationClip(_animRecorder.CharacterAnimator, _facialData);
            }

            Debug.Log($"[{nameof(FaceAnimationRecorder)}] Record end");
            CleanupRecording();
        }

        private void CleanupRecording()
        {
            _recording = false;
            if (_past != null)
            {
                _facePool.Release(_past);
                _past = null;
            }
        }

        private void UpdateRecording()
        {
            _recordedTime = Time.time - _startTime;

            if (!ShouldRecordFrame()) return;

            var currentFace = RecordCurrentFrame();
            if (!IsSame(currentFace, _past))
            {
                SaveFrame(currentFace);
            }
            else
            {
                _facePool.Release(currentFace);
            }

            _frameCount++;
        }

        private bool ShouldRecordFrame()
        {
            if (_targetFPS <= 0) return true;

            var nextTime = (1.0f * (_frameCount + 1)) / _targetFPS;
            if (nextTime > _recordedTime) return false;

            if (_frameCount % _targetFPS == 0)
            {
                Debug.Log($"Face_FPS: {1 / (_recordedTime / _frameCount):F2}");
            }

            return true;
        }

        private CharacterFacialData.SerializeHumanoidFace RecordCurrentFrame()
        {
            var face = _facePool.Get();
            face.Smeshes.Clear();

            foreach (var mesh in _smeshs)
            {
                if (mesh == null || mesh.sharedMesh == null) continue;

                var blendShape = _meshBlendPool.Get();
                blendShape.path = mesh.name;
                blendShape.blendShapes = new float[mesh.sharedMesh.blendShapeCount];

                RecordBlendShapes(mesh, blendShape);
                face.Smeshes.Add(blendShape);
            }

            face.FrameCount = _frameCount;
            face.Time = _recordedTime;

            return face;
        }

        private void RecordBlendShapes(SkinnedMeshRenderer mesh, CharacterFacialData.SerializeHumanoidFace.MeshAndBlendshape blendShape)
        {
            for (int j = 0; j < mesh.sharedMesh.blendShapeCount; j++)
            {
                var shapeName = mesh.sharedMesh.GetBlendShapeName(j);
                if (!_exclusiveBlendshapeNames.Contains(shapeName))
                {
                    blendShape.blendShapes[j] = mesh.GetBlendShapeWeight(j);
                }
            }
        }

        private void SaveFrame(CharacterFacialData.SerializeHumanoidFace face)
        {
            _facialData.Facials.Add(face);
            if (_past != null)
            {
                _facePool.Release(_past);
            }
            _past = face;
        }

        private bool IsSame(CharacterFacialData.SerializeHumanoidFace a, CharacterFacialData.SerializeHumanoidFace b)
        {
            if (a == null || b == null || a.Smeshes.Count == 0 || b.Smeshes.Count == 0)
            {
                return false;
            }

            if (a.BlendShapeNum() != b.BlendShapeNum())
            {
                return false;
            }

            return !a.Smeshes.Where((t1, i) =>
                t1.blendShapes.Where((t, j) => Mathf.Abs(t - b.Smeshes[i].blendShapes[j]) > 1).Any()).Any();
        }

        private void ExportFacialAnimationClip(Animator root, CharacterFacialData facial)
        {
            if (root == null || facial == null) return;

            var animclip = new AnimationClip { frameRate = _targetFPS };
            
            foreach (var meshRenderer in _smeshs)
            {
                if (meshRenderer == null || meshRenderer.sharedMesh == null) continue;
                
                var path = BuildAnimationPath(meshRenderer.transform, root.transform);
                ExportBlendShapes(meshRenderer, path, animclip, facial);
            }

            SaveAnimationClip(animclip);
        }

        private string BuildAnimationPath(Transform current, Transform root)
        {
            _pathBuilder.Clear();
            _pathBuilder.Append(current.name);

            var parent = current.parent;
            while (parent != null && parent != root)
            {
                _pathBuilder.Insert(0, '/').Insert(0, parent.name);
                parent = parent.parent;
            }

            return _pathBuilder.ToString();
        }

        private void ExportBlendShapes(SkinnedMeshRenderer meshRenderer, string path, AnimationClip animclip, CharacterFacialData facial)
        {
            for (var blendShapeIndex = 0; blendShapeIndex < meshRenderer.sharedMesh.blendShapeCount; blendShapeIndex++)
            {
                var curve = CreateBlendShapeCurve(meshRenderer, blendShapeIndex, facial);
                var binding = CreateCurveBinding(path, meshRenderer.sharedMesh.GetBlendShapeName(blendShapeIndex));
                AnimationUtility.SetEditorCurve(animclip, binding, curve);
            }
        }

        private AnimationCurve CreateBlendShapeCurve(SkinnedMeshRenderer meshRenderer, int blendShapeIndex, CharacterFacialData facial)
        {
            var curve = new AnimationCurve();
            float lastWeight = -1;

            for (int k = 0; k < facial.Facials.Count; k++)
            {
                var currentWeight = facial.Facials[k].Smeshes[Array.IndexOf(_smeshs, meshRenderer)].blendShapes[blendShapeIndex];
                if (Mathf.Abs(lastWeight - currentWeight) > 0.1f)
                {
                    curve.AddKey(new Keyframe(facial.Facials[k].Time, currentWeight, float.PositiveInfinity, 0f));
                    lastWeight = currentWeight;
                }
            }

            return curve;
        }

        private EditorCurveBinding CreateCurveBinding(string path, string blendShapeName)
        {
            return new EditorCurveBinding
            {
                type = typeof(SkinnedMeshRenderer),
                path = path,
                propertyName = $"blendShape.{blendShapeName}"
            };
        }

        private void SaveAnimationClip(AnimationClip clip)
        {
            MotionDataRecorder.SafeCreateDirectory("Assets/Resources");

            var outputPath = $"Assets/Resources/FaceRecordMotion_{_animRecorder.CharacterAnimator.name}_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}_Clip.anim";
            var uniquePath = AssetDatabase.GenerateUniqueAssetPath(outputPath);

            Debug.Log($"Saving animation to: {uniquePath}");
            AssetDatabase.CreateAsset(clip, uniquePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                _meshPool.Clear();
                _facePool.Clear();
                _meshBlendPool.Clear();
                
                if (_facialData != null)
                {
                    if (Application.isPlaying)
                    {
                        Destroy(_facialData);
                    }
                    else
                    {
                        DestroyImmediate(_facialData);
                    }
                }
            }

            _isDisposed = true;
        }
        #endregion

        #region Helper Classes
        private class ObjectPool<T> where T : class, new()
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
