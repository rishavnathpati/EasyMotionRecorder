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
using System.Collections.Concurrent;

namespace Entum
{
    /// <summary>
    /// Runtime recorder for CSV motion data with optimized performance
    /// </summary>
    [DefaultExecutionOrder(31000)]
    public sealed class MotionDataRecorderCSV : MotionDataRecorder
    {
        #region Serialized Fields
        [Header("CSV Settings")]
        [SerializeField, Tooltip("Directory path must end with a slash")]
        private string _outputDirectory;

        [SerializeField, Tooltip("Include file extension")]
        private string _outputFileName;

        [SerializeField]
        private bool _useAsyncWrite = true;

        [SerializeField]
        private int _bufferSize = 1000;
        #endregion

        #region Private Fields
        private readonly ConcurrentQueue<string> _writeQueue = new();
        private StreamWriter _writer;
        private bool _isWriting;
        private string _currentFilePath;
        #endregion

        #region Events
        public event Action<string> OnRecordingSaved;
        public event Action<Exception> OnRecordingError;
        #endregion

        #region Unity Lifecycle
        protected override async void OnDisable()
        {
            base.OnDisable();
            await FlushAndCloseAsync();
        }

        private void OnApplicationQuit()
        {
            FlushAndCloseAsync().GetAwaiter().GetResult();
        }
        #endregion

        #region Protected Methods
        protected override async void WriteAnimationFile()
        {
            try
            {
                await InitializeWriterAsync();
                await WriteFramesAsync();
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(MotionDataRecorderCSV)}] Failed to write animation file: {e.Message}");
                OnRecordingError?.Invoke(e);
            }
        }
        #endregion

        #region Private Methods
        private async Task InitializeWriterAsync()
        {
            var directory = string.IsNullOrEmpty(_outputDirectory) 
                ? Path.Combine(Application.streamingAssetsPath, "MotionData/") 
                : _outputDirectory;

            var fileName = string.IsNullOrEmpty(_outputFileName)
                ? $"motion_{DateTime.Now:yyyy_MM_dd_HH_mm_ss}.csv"
                : _outputFileName;

            try
            {
                Directory.CreateDirectory(directory);
                _currentFilePath = Path.Combine(directory, fileName);
                
                if (_useAsyncWrite)
                {
                    _writer = new StreamWriter(new FileStream(_currentFilePath, FileMode.Create, FileAccess.Write, FileShare.None, _bufferSize, true));
                }
                else
                {
                    _writer = new StreamWriter(_currentFilePath);
                }
            }
            catch (Exception e)
            {
                throw new IOException($"Failed to initialize writer: {e.Message}", e);
            }
        }

        private async Task WriteFramesAsync()
        {
            try
            {
                _isWriting = true;

                foreach (var pose in Poses.Poses)
                {
                    var serializedPose = pose.SerializeCSV();
                    if (_useAsyncWrite)
                    {
                        _writeQueue.Enqueue(serializedPose);
                        if (_writeQueue.Count >= _bufferSize)
                        {
                            await ProcessWriteQueueAsync();
                        }
                    }
                    else
                    {
                        await _writer.WriteLineAsync(serializedPose);
                    }
                }

                if (_useAsyncWrite)
                {
                    await ProcessWriteQueueAsync();
                }

                await FlushAndCloseAsync();
                OnRecordingSaved?.Invoke(_currentFilePath);
            }
            catch (Exception e)
            {
                throw new IOException($"Failed to write frames: {e.Message}", e);
            }
            finally
            {
                _isWriting = false;
            }
        }

        private async Task ProcessWriteQueueAsync()
        {
            while (_writeQueue.TryDequeue(out var line))
            {
                await _writer.WriteLineAsync(line);
            }
            await _writer.FlushAsync();
        }

        private async Task FlushAndCloseAsync()
        {
            if (_writer == null) return;

            try
            {
                if (_useAsyncWrite && _isWriting)
                {
                    await ProcessWriteQueueAsync();
                }

                await _writer.FlushAsync();
                _writer.Close();
                _writer = null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(MotionDataRecorderCSV)}] Failed to close writer: {e.Message}");
                throw;
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Sets the output directory for recorded motion data
        /// </summary>
        public void SetOutputDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentNullException(nameof(directory));
            }

            _outputDirectory = directory;
        }

        /// <summary>
        /// Sets the output file name for recorded motion data
        /// </summary>
        public void SetOutputFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            _outputFileName = fileName;
        }

        /// <summary>
        /// Gets the current file path being written to
        /// </summary>
        public string GetCurrentFilePath()
        {
            return _currentFilePath;
        }

        /// <summary>
        /// Checks if the recorder is currently writing
        /// </summary>
        public bool IsWriting()
        {
            return _isWriting;
        }
        #endregion
    }
}
