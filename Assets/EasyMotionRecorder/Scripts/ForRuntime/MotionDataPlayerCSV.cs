/**
[EasyMotionRecorder]

Copyright (c) 2018 Duo.inc

This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/

using UnityEngine;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Entum
{
    /// <summary>
    /// Runtime player for CSV motion data with optimized performance
    /// </summary>
    public sealed class MotionDataPlayerCSV : MotionDataPlayer
    {
        #region Serialized Fields
        [Header("CSV Settings")]
        [SerializeField, Tooltip("Directory path must end with a slash")]
        private string _recordedDirectory;

        [SerializeField, Tooltip("Include file extension")]
        private string _recordedFileName;

        [SerializeField]
        private bool _loadOnStart = true;

        [SerializeField]
        private bool _autoPlay;
        #endregion

        #region Events
        public event Action<float> OnLoadProgress;
        public event Action<Exception> OnLoadError;
        public event Action OnLoadComplete;
        #endregion

        #region Unity Lifecycle
        private async void Start()
        {
            if (!_loadOnStart) return;

            try
            {
                await LoadMotionDataAsync();
                if (_autoPlay)
                {
                    Play();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(MotionDataPlayerCSV)}] Failed to load motion data: {e.Message}");
                OnLoadError?.Invoke(e);
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Loads motion data from CSV file
        /// </summary>
        public async Task LoadMotionDataAsync(string directory = null, string fileName = null)
        {
            var dir = directory ?? _recordedDirectory;
            if (string.IsNullOrEmpty(dir))
            {
                dir = Path.Combine(Application.streamingAssetsPath, "MotionData/");
            }

            var file = fileName ?? _recordedFileName;
            if (string.IsNullOrEmpty(file))
            {
                throw new ArgumentException("File name not specified");
            }

            var path = Path.Combine(dir, file);
            await LoadCSVDataAsync(path);
        }
        #endregion

        #region Private Methods
        private async Task LoadCSVDataAsync(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Motion data file not found: {path}");
            }

            try
            {
                RecordedMotionData = ScriptableObject.CreateInstance<HumanoidPoses>();
                
                using var reader = new StreamReader(path);
                var totalLines = await CountLinesAsync(path);
                var currentLine = 0;

                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var pose = new SerializeHumanoidPose();
                        pose.DeserializeCSV(line);
                        RecordedMotionData.AddPose(pose);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[{nameof(MotionDataPlayerCSV)}] Failed to parse line {currentLine}: {e.Message}");
                    }

                    currentLine++;
                    OnLoadProgress?.Invoke((float)currentLine / totalLines);
                }

                OnLoadComplete?.Invoke();
            }
            catch (Exception e)
            {
                if (RecordedMotionData != null)
                {
                    Destroy(RecordedMotionData);
                    RecordedMotionData = null;
                }
                throw new IOException($"Failed to load CSV data: {e.Message}", e);
            }
        }

        private static async Task<int> CountLinesAsync(string path)
        {
            using var reader = new StreamReader(path);
            var count = 0;
            while (await reader.ReadLineAsync() != null)
            {
                count++;
            }
            return count;
        }
        #endregion
    }
}
