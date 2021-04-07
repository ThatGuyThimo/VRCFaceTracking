﻿using System;
using System.Collections.Generic;
using System.Threading;
using MelonLoader;
using ViveSR;
using ViveSR.anipal;
using ViveSR.anipal.Eye;
using ViveSR.anipal.Lip;

namespace VRCEyeTracking
{
    public static class SRanipalTrack
    {
        public static bool EyeEnabled, FaceEnabled;
        

        public static EyeData_v2 LatestEyeData;
        public static Dictionary<LipShape_v2, float> LatestLipData;

        public static float CurrentDiameter;

        public static float MaxOpen;
        public static float MinOpen = 999;

        public static readonly Thread Initializer = new Thread(Initialize);
        private static readonly Thread SRanipalWorker = new Thread(() => Update(cancellationToken.Token));
        
        private static CancellationTokenSource cancellationToken = new CancellationTokenSource();
        
        private static bool IsRealError(this Error error) => error != Error.WORK && error != Error.UNDEFINED;

        private static void Initialize()
        {
            MelonLogger.Msg($"Initializing SRanipal...");
            
            var eyeError = SRanipal_API.Initial(SRanipal_Eye_v2.ANIPAL_TYPE_EYE_V2, IntPtr.Zero);
            var faceError = SRanipal_API.Initial(SRanipal_Lip_v2.ANIPAL_TYPE_LIP_V2, IntPtr.Zero);

            HandleErrors(eyeError, faceError);
            SRanipalWorker.Start();
        }

        private static void HandleErrors(Error eyeError, Error faceError)
        {
            if (eyeError != Error.UNDEFINED && eyeError != Error.WORK)
                MelonLogger.Warning($"Eye Tracking will be unavailable for this session. ({eyeError})");
            else if (eyeError == Error.WORK)
            {
                EyeEnabled = true;
                MelonLogger.Msg("SRanipal Eye Initialized!");
            }

            if (faceError != Error.UNDEFINED && faceError != Error.WORK)
                MelonLogger.Warning($"Lip Tracking will be unavailable for this session. ({faceError})");
            else if (faceError == Error.WORK)
            {
                FaceEnabled = true;
                MelonLogger.Msg("SRanipal Lip Initialized!");
            }
        }

        public static void Stop()
        {
            cancellationToken.Cancel();
            
            if (EyeEnabled) SRanipal_API.Release(SRanipal_Eye_v2.ANIPAL_TYPE_EYE_V2);
            if (FaceEnabled) SRanipal_API.Release(SRanipal_Lip_v2.ANIPAL_TYPE_LIP_V2);
            
            cancellationToken.Dispose();
        }

        private static void Update(CancellationToken token)
        {
            while (!token.IsCancellationRequested && (EyeEnabled || FaceEnabled))
            {
                try
                {
                    if (EyeEnabled) UpdateEye();
                    if (FaceEnabled) UpdateMouth();
                }
                catch (Exception e)
                {
                    if (e.InnerException.GetType() != typeof(ThreadAbortException))
                        MelonLogger.Error("Threading error occured in SRanipalTrack.Update: "+e+": "+e.InnerException);
                }

                Thread.Sleep(5);
            }
        }
        
        #region EyeUpdate

        private static void UpdateEye()
        {
            SRanipal_Eye_API.GetEyeData_v2(ref LatestEyeData);

            if (LatestEyeData.verbose_data.right.GetValidity(SingleEyeDataValidity
                .SINGLE_EYE_DATA_PUPIL_DIAMETER_VALIDITY))
            {
                CurrentDiameter = LatestEyeData.verbose_data.right.pupil_diameter_mm;
                if (LatestEyeData.verbose_data.right.eye_openness >= 1f)
                    UpdateMinMaxDilation(LatestEyeData.verbose_data.right.pupil_diameter_mm);
            }
            else if (LatestEyeData.verbose_data.left.GetValidity(SingleEyeDataValidity
                .SINGLE_EYE_DATA_PUPIL_DIAMETER_VALIDITY))
            {
                CurrentDiameter = LatestEyeData.verbose_data.left.pupil_diameter_mm;
                if (LatestEyeData.verbose_data.left.eye_openness >= 1f)
                    UpdateMinMaxDilation(LatestEyeData.verbose_data.left.pupil_diameter_mm);
            }
        }

        private static void UpdateMinMaxDilation(float readDilation)
        {
            if (readDilation > MaxOpen)
                MaxOpen = readDilation;
            if (readDilation < MinOpen)
                MinOpen = readDilation;
        }
        
        #endregion

        #region MouthUpdate

        private static void UpdateMouth()
        {
            SRanipal_Lip_v2.GetLipWeightings(out LatestLipData);
        }

        #endregion
    }
}