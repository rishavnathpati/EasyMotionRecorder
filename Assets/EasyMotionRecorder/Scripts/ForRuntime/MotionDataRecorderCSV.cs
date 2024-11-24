/**
[EasyMotionRecorder]

Copyright (c) 2018 Duo.inc

This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/

using UnityEngine;
using System;
using System.IO;

namespace Entum
{
    /// <summary>
    /// Class for recording motion data to CSV
    /// Can record during runtime
    /// </summary>
    [DefaultExecutionOrder(31000)]
    public class MotionDataRecorderCSV : MotionDataRecorder
    {
        [SerializeField, Tooltip("Must end with a slash")]
        private string _outputDirectory;

        [SerializeField, Tooltip("Include file extension")]
        private string _outputFileName;

        protected override void WriteAnimationFile()
        {
            // Open file
            string directoryStr = _outputDirectory;
            if (directoryStr == "")
            {
                // Auto-set directory
                directoryStr = Application.streamingAssetsPath + "/";

                if (!Directory.Exists(directoryStr))
                {
                    Directory.CreateDirectory(directoryStr);
                }
            }

            string fileNameStr = _outputFileName;
            if (fileNameStr == "")
            {
                // Auto-set filename
                fileNameStr = string.Format("motion_{0:yyyy_MM_dd_HH_mm_ss}.csv", DateTime.Now);
            }

            FileStream fs = new FileStream(directoryStr + fileNameStr, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);

            foreach (var pose in Poses.Poses)
            {
                string seriStr = pose.SerializeCSV();
                sw.WriteLine(seriStr);
            }

            // Close file
            try
            {
                sw.Close();
                fs.Close();
                sw = null;
                fs = null;
            }
            catch (Exception e)
            {
                Debug.LogError("File writing failed! " + e.Message + e.StackTrace);
            }

            if (sw != null)
            {
                sw.Close();
            }

            if (fs != null)
            {
                fs.Close();
            }

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif

            RecordedTime = 0f;
            StartTime = Time.time;
            FrameIndex = 0;
        }
    }
}
