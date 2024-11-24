using UnityEngine;
using System.Collections.Generic;

namespace Entum
{
    /// <summary>
    /// ScriptableObject for motion data settings and muscle mapping configuration
    /// </summary>
    [CreateAssetMenu(fileName = "MotionDataSettings", menuName = "EasyMotionRecorder/Motion Data Settings")]
    public class MotionDataSettings : ScriptableObject
    {
        #region Singleton
        private static MotionDataSettings _instance;
        public static MotionDataSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<MotionDataSettings>("MotionDataSettings");
                    if (_instance == null)
                    {
                        _instance = CreateInstance<MotionDataSettings>();
                        #if UNITY_EDITOR
                        UnityEditor.AssetDatabase.CreateAsset(_instance, "Assets/Resources/MotionDataSettings.asset");
                        UnityEditor.AssetDatabase.SaveAssets();
                        #endif
                    }
                }
                return _instance;
            }
        }
        #endregion

        #region Enums
        public enum Rootbonesystem
        {
            Hipbone,
            Objectroot
        }
        #endregion

        #region Serialized Fields
        [SerializeField]
        private Dictionary<string, string> _traitPropMap = new()
        {
            {"Left Thumb 1 Stretched", "LeftHand.Thumb.1 Stretched"},
            {"Left Thumb Spread", "LeftHand.Thumb.Spread"},
            {"Left Thumb 2 Stretched", "LeftHand.Thumb.2 Stretched"},
            {"Left Thumb 3 Stretched", "LeftHand.Thumb.3 Stretched"},
            {"Left Index 1 Stretched", "LeftHand.Index.1 Stretched"},
            {"Left Index Spread", "LeftHand.Index.Spread"},
            {"Left Index 2 Stretched", "LeftHand.Index.2 Stretched"},
            {"Left Index 3 Stretched", "LeftHand.Index.3 Stretched"},
            {"Left Middle 1 Stretched", "LeftHand.Middle.1 Stretched"},
            {"Left Middle Spread", "LeftHand.Middle.Spread"},
            {"Left Middle 2 Stretched", "LeftHand.Middle.2 Stretched"},
            {"Left Middle 3 Stretched", "LeftHand.Middle.3 Stretched"},
            {"Left Ring 1 Stretched", "LeftHand.Ring.1 Stretched"},
            {"Left Ring Spread", "LeftHand.Ring.Spread"},
            {"Left Ring 2 Stretched", "LeftHand.Ring.2 Stretched"},
            {"Left Ring 3 Stretched", "LeftHand.Ring.3 Stretched"},
            {"Left Little 1 Stretched", "LeftHand.Little.1 Stretched"},
            {"Left Little Spread", "LeftHand.Little.Spread"},
            {"Left Little 2 Stretched", "LeftHand.Little.2 Stretched"},
            {"Left Little 3 Stretched", "LeftHand.Little.3 Stretched"},
            {"Right Thumb 1 Stretched", "RightHand.Thumb.1 Stretched"},
            {"Right Thumb Spread", "RightHand.Thumb.Spread"},
            {"Right Thumb 2 Stretched", "RightHand.Thumb.2 Stretched"},
            {"Right Thumb 3 Stretched", "RightHand.Thumb.3 Stretched"},
            {"Right Index 1 Stretched", "RightHand.Index.1 Stretched"},
            {"Right Index Spread", "RightHand.Index.Spread"},
            {"Right Index 2 Stretched", "RightHand.Index.2 Stretched"},
            {"Right Index 3 Stretched", "RightHand.Index.3 Stretched"},
            {"Right Middle 1 Stretched", "RightHand.Middle.1 Stretched"},
            {"Right Middle Spread", "RightHand.Middle.Spread"},
            {"Right Middle 2 Stretched", "RightHand.Middle.2 Stretched"},
            {"Right Middle 3 Stretched", "RightHand.Middle.3 Stretched"},
            {"Right Ring 1 Stretched", "RightHand.Ring.1 Stretched"},
            {"Right Ring Spread", "RightHand.Ring.Spread"},
            {"Right Ring 2 Stretched", "RightHand.Ring.2 Stretched"},
            {"Right Ring 3 Stretched", "RightHand.Ring.3 Stretched"},
            {"Right Little 1 Stretched", "RightHand.Little.1 Stretched"},
            {"Right Little Spread", "RightHand.Little.Spread"},
            {"Right Little 2 Stretched", "RightHand.Little.2 Stretched"},
            {"Right Little 3 Stretched", "RightHand.Little.3 Stretched"},
        };
        #endregion

        #region Properties
        /// <summary>
        /// Gets the muscle mapping dictionary for humanoid animation
        /// </summary>
        public IReadOnlyDictionary<string, string> TraitPropMap => _traitPropMap;
        #endregion

        #region Public Methods
        /// <summary>
        /// Adds or updates a muscle mapping
        /// </summary>
        public void SetMuscleMapping(string muscleName, string mappedName)
        {
            if (string.IsNullOrEmpty(muscleName)) throw new System.ArgumentNullException(nameof(muscleName));
            if (string.IsNullOrEmpty(mappedName)) throw new System.ArgumentNullException(nameof(mappedName));

            _traitPropMap[muscleName] = mappedName;
        }

        /// <summary>
        /// Removes a muscle mapping
        /// </summary>
        public void RemoveMuscleMapping(string muscleName)
        {
            if (string.IsNullOrEmpty(muscleName)) throw new System.ArgumentNullException(nameof(muscleName));

            _traitPropMap.Remove(muscleName);
        }

        /// <summary>
        /// Clears all muscle mappings
        /// </summary>
        public void ClearMuscleMappings()
        {
            _traitPropMap.Clear();
        }
        #endregion
    }
}
