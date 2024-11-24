/**
[EasyMotionRecorder]

Copyright (c) 2018 Duo.inc

This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;

namespace Entum
{
    /// <summary>
    /// Class for recording blendshape movements
    /// Since lip sync is likely to be added later on the Timeline with AudioClip,
    /// you can register Exclusive (excluded) Blendshape names.
    /// </summary>
    [RequireComponent(typeof(MotionDataRecorder))]
    public class FaceAnimationRecorder : MonoBehaviour
    {
        [Header("Set to true if you want to record facial expressions simultaneously")] [SerializeField]
        private bool _recordFaceBlendshapes = false;

        [Header("Add morph names here if you don't want to record lip sync, e.g., face_mouse_e etc.")] [SerializeField]
        private List<string> _exclusiveBlendshapeNames;

        [Tooltip("Recording FPS. 0 means no limit. Cannot exceed Update FPS.")]
        public float TargetFPS = 60.0f;

        private MotionDataRecorder _animRecorder;

        private SkinnedMeshRenderer[] _smeshs;

        private CharacterFacialData _facialData = null;

        private bool _recording = false;

        private int _frameCount = 0;

        CharacterFacialData.SerializeHumanoidFace _past = new CharacterFacialData.SerializeHumanoidFace();

        private float _recordedTime = 0f;
        private float _startTime;

        // Use this for initialization
        private void OnEnable()
        {
            _animRecorder = GetComponent<MotionDataRecorder>();
            _animRecorder.OnRecordStart += RecordStart;
            _animRecorder.OnRecordEnd += RecordEnd;
            if (_animRecorder.CharacterAnimator != null)
            {
                _smeshs = GetSkinnedMeshRenderers(_animRecorder.CharacterAnimator);
            }
        }

        SkinnedMeshRenderer[] GetSkinnedMeshRenderers(Animator root)
        {
            var helper = root;
            var renderers = helper.GetComponentsInChildren<SkinnedMeshRenderer>();
            List<SkinnedMeshRenderer> smeshList = new List<SkinnedMeshRenderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                var rend = renderers[i];
                var cnt = rend.sharedMesh.blendShapeCount;
                if (cnt > 0)
                {
                    smeshList.Add(rend);
                }
            }

            return smeshList.ToArray();
        }

        private void OnDisable()
        {
            if (_recording)
            {
                RecordEnd();
                _recording = false;
            }

            if (_animRecorder == null) return;
            _animRecorder.OnRecordStart -= RecordStart;
            _animRecorder.OnRecordEnd -= RecordEnd;
        }

        /// <summary>
        /// Start recording
        /// </summary>
        private void RecordStart()
        {
            if (_recordFaceBlendshapes == false)
            {
                return;
            }

            if (_recording)
            {
                return;
            }

            if (_smeshs.Length == 0)
            {
                Debug.LogError("No facial mesh specified, facial animation will not be recorded");
                return;
            }

            Debug.Log("FaceAnimationRecorder record start");
            _recording = true;
            _recordedTime = 0f;
            _startTime = Time.time;
            _frameCount = 0;
            _facialData = ScriptableObject.CreateInstance<CharacterFacialData>();
        }

        /// <summary>
        /// End recording
        /// </summary>
        private void RecordEnd()
        {
            if (_recordFaceBlendshapes == false)
            {
                return;
            }

            if (_smeshs.Length == 0)
            {
                Debug.LogError("No facial mesh specified, facial animation was not recorded");
                if (_recording == true)
                {
                    Debug.LogAssertion("Unexpected execution!!!!");
                }
            }
            else
            {
                ExportFacialAnimationClip(_animRecorder.CharacterAnimator, _facialData);
            }

            Debug.Log("FaceAnimationRecorder record end");

            _recording = false;
        }

        private void WriteAnimationFileToScriptableObject()
        {
            MotionDataRecorder.SafeCreateDirectory("Assets/Resources");

            string path = AssetDatabase.GenerateUniqueAssetPath(
                "Assets/Resources/RecordMotion_ face" + _animRecorder.CharacterAnimator.name +
                DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") +
                ".asset");

            if (_facialData == null)
            {
                Debug.LogError("Recorded Face data is null");
            }
            else
            {
                AssetDatabase.CreateAsset(_facialData, path);
                AssetDatabase.Refresh();
            }
            _startTime = Time.time;
            _recordedTime = 0f;
            _frameCount = 0;
        }

        // Check for differences within frames
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

        private void LateUpdate()
        {
            if (Input.GetKeyDown(KeyCode.Y))
            {
                ExportFacialAnimationClipTest();
            }

            if (!_recording)
            {
                return;
            }

            _recordedTime = Time.time - _startTime;

            if (TargetFPS != 0.0f)
            {
                var nextTime = (1.0f * (_frameCount + 1)) / TargetFPS;
                if (nextTime > _recordedTime)
                {
                    return;
                }
                if (_frameCount % TargetFPS == 0)
                {
                    print("Face_FPS=" + 1 / (_recordedTime / _frameCount));
                }
            }
            else
            {
                if (Time.frameCount % Application.targetFrameRate == 0)
                {
                    print("Face_FPS=" + 1 / Time.deltaTime);
                }
            }

            var p = new CharacterFacialData.SerializeHumanoidFace();
            for (int i = 0; i < _smeshs.Length; i++)
            {
                var mesh = new CharacterFacialData.SerializeHumanoidFace.MeshAndBlendshape();
                mesh.path = _smeshs[i].name;
                mesh.blendShapes = new float[_smeshs[i].sharedMesh.blendShapeCount];

                for (int j = 0; j < _smeshs[i].sharedMesh.blendShapeCount; j++)
                {
                    var tname = _smeshs[i].sharedMesh.GetBlendShapeName(j);

                    var useThis = true;

                    foreach (var item in _exclusiveBlendshapeNames)
                    {
                        if (item.IndexOf(tname, StringComparison.Ordinal) >= 0)
                        {
                            useThis = false;
                        }
                    }

                    if (useThis)
                    {
                        mesh.blendShapes[j] = _smeshs[i].GetBlendShapeWeight(j);
                    }
                }

                p.Smeshes.Add(mesh);
            }

            if (!IsSame(p, _past))
            {
                p.FrameCount = _frameCount;
                p.Time = _recordedTime;

                _facialData.Facials.Add(p);
                _past = new CharacterFacialData.SerializeHumanoidFace(p);
            }

            _frameCount++;
        }

        /// <summary>
        /// Write with Animator and recorded data
        /// </summary>
        void ExportFacialAnimationClip(Animator root, CharacterFacialData facial)
        {
            var animclip = new AnimationClip();

            var mesh = _smeshs;

            for (int faceTargetMeshIndex = 0; faceTargetMeshIndex < mesh.Length; faceTargetMeshIndex++)
            {
                var pathsb = new StringBuilder().Append(mesh[faceTargetMeshIndex].transform.name);
                var trans = mesh[faceTargetMeshIndex].transform;
                while (trans.parent != null && trans.parent != root.transform)
                {
                    trans = trans.parent;
                    pathsb.Insert(0, "/").Insert(0, trans.name);
                }

                // Path contains the base name of the Blendshape
                // Something like U_CHAR_1:SkinnedMeshRenderer
                var path = pathsb.ToString();

                // Generate AnimationCurve for each individual Blendshape of each individual mesh
                for (var blendShapeIndex = 0;
                    blendShapeIndex < mesh[faceTargetMeshIndex].sharedMesh.blendShapeCount;
                    blendShapeIndex++)
                {
                    var curveBinding = new EditorCurveBinding();
                    curveBinding.type = typeof(SkinnedMeshRenderer);
                    curveBinding.path = path;
                    curveBinding.propertyName = "blendShape." +
                                                mesh[faceTargetMeshIndex].sharedMesh.GetBlendShapeName(blendShapeIndex);
                    AnimationCurve curve = new AnimationCurve();

                    float pastBlendshapeWeight = -1;
                    for (int k = 0; k < _facialData.Facials.Count; k++)
                    {
                        if (!(Mathf.Abs(pastBlendshapeWeight - _facialData.Facials[k].Smeshes[faceTargetMeshIndex].blendShapes[blendShapeIndex]) >
                              0.1f)) continue;
                        curve.AddKey(new Keyframe(facial.Facials[k].Time, _facialData.Facials[k].Smeshes[faceTargetMeshIndex].blendShapes[blendShapeIndex], float.PositiveInfinity, 0f));
                        pastBlendshapeWeight = _facialData.Facials[k].Smeshes[faceTargetMeshIndex].blendShapes[blendShapeIndex];
                    }

                    AnimationUtility.SetEditorCurve(animclip, curveBinding, curve);
                }
            }

            MotionDataRecorder.SafeCreateDirectory("Assets/Resources");

            var outputPath = "Assets/Resources/FaceRecordMotion_" + _animRecorder.CharacterAnimator.name + "_" +
                             DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + "_Clip.anim";

            Debug.Log("outputPath:" + outputPath);
            AssetDatabase.CreateAsset(animclip,
                AssetDatabase.GenerateUniqueAssetPath(outputPath));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Test writing with Animator and recorded data
        /// </summary>
        void ExportFacialAnimationClipTest()
        {
            var animclip = new AnimationClip();

            var mesh = _smeshs;

            for (int i = 0; i < mesh.Length; i++)
            {
                var pathsb = new StringBuilder().Append(mesh[i].transform.name);
                var trans = mesh[i].transform;
                while (trans.parent != null && trans.parent != _animRecorder.CharacterAnimator.transform)
                {
                    trans = trans.parent;
                    pathsb.Insert(0, "/").Insert(0, trans.name);
                }

                var path = pathsb.ToString();

                for (var j = 0; j < mesh[i].sharedMesh.blendShapeCount; j++)
                {
                    var curveBinding = new EditorCurveBinding();
                    curveBinding.type = typeof(SkinnedMeshRenderer);
                    curveBinding.path = path;
                    curveBinding.propertyName = "blendShape." + mesh[i].sharedMesh.GetBlendShapeName(j);
                    AnimationCurve curve = new AnimationCurve();

                    // Add keys for all Blendshapes with 0→100→0 transition
                    curve.AddKey(0, 0);
                    curve.AddKey(1, 100);
                    curve.AddKey(2, 0);

                    Debug.Log("path: " + curveBinding.path + "\r\nname: " + curveBinding.propertyName + " val:");

                    AnimationUtility.SetEditorCurve(animclip, curveBinding, curve);
                }
            }

            AssetDatabase.CreateAsset(animclip,
                AssetDatabase.GenerateUniqueAssetPath("Assets/" + _animRecorder.CharacterAnimator.name +
                                                      "_facial_ClipTest.anim"));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
