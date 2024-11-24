/**
[EasyMotionRecorder]

Copyright (c) 2018 Duo.inc

This software is released under the MIT License.
http://opensource.org/licenses/mit-license.php
*/

using UnityEngine;
using System.IO;

namespace Entum
{
    /// <summary>
    /// Play motion data that was output to CSV
    /// </summary>
    public class MotionDataPlayerCSV : MotionDataPlayer
    {
        [SerializeField, Tooltip("Must end with a slash")]
        private string _recordedDirectory;

        [SerializeField, Tooltip("Include file extension")]
        private string _recordedFileName;

        // Use this for initialization
        private void Start()
        {
            if (string.IsNullOrEmpty(_recordedDirectory))
            {
                _recordedDirectory = Application.streamingAssetsPath + "/";
            }

            string motionCSVPath = _recordedDirectory + _recordedFileName;
            LoadCSVData(motionCSVPath);
        }

        // Create _recordedMotionData from CSV
        private void LoadCSVData(string motionDataPath)
        {
            // Exit if file doesn't exist
            if (!File.Exists(motionDataPath))
            {
                return;
            }

            RecordedMotionData = ScriptableObject.CreateInstance<HumanoidPoses>();

            FileStream fs = null;
            StreamReader sr = null;

            // File reading
            try
            {
                fs = new FileStream(motionDataPath, FileMode.Open);
                sr = new StreamReader(fs);

                while (sr.Peek() > -1)
                {
                    string line = sr.ReadLine();
                    var seriHumanPose = new HumanoidPoses.SerializeHumanoidPose();
                    if (line != "")
                    {
                        seriHumanPose.DeserializeCSV(line);
                        RecordedMotionData.Poses.Add(seriHumanPose);
                    }
                }
                sr.Close();
                fs.Close();
                sr = null;
                fs = null;
            }
            catch (System.Exception e)
            {
                Debug.LogError("File reading failed! " + e.Message + e.StackTrace);
            }

            if (sr != null)
            {
                sr.Close();
            }

            if (fs != null)
            {
                fs.Close();
            }
        }
    }
}
