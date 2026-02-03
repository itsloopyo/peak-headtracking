using System;
using System.Reflection;
using CameraUnlock.Core.Unity.Tracking;
using HarmonyLib;
using UnityEngine;

namespace PeakHeadTracking.Patches
{
    /// <summary>
    /// Harmony patch for CameraQuad to apply head tracking before it reads the camera transform.
    ///
    /// Problem: CameraQuad.LateUpdate() positions a near-clip plane quad using cam.transform.forward.
    /// View matrix modification does NOT change the transform, so the quad would be misaligned.
    ///
    /// Solution: In a PREFIX, temporarily set cam.transform.rotation to include head tracking,
    /// then in a POSTFIX, restore the original rotation. This is self-contained and scoped.
    /// </summary>
    [HarmonyPatch]
    public static class CameraQuadPatches
    {
        private static bool patchActive = false;
        private static Quaternion storedRotation;

        /// <summary>
        /// Dynamically find the target method (CameraQuad.LateUpdate).
        /// Uses string-based lookup since CameraQuad is a game class.
        /// </summary>
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            try
            {
                var type = AccessTools.TypeByName(GameTypeNames.CameraQuad);
                if (type == null)
                {
                    PeakHeadTrackingPlugin.Logger?.LogWarning("[CameraQuadPatch] CameraQuad type not found - patch will not be applied");
                    return null;
                }

                var method = AccessTools.Method(type, "LateUpdate");
                if (method == null)
                {
                    PeakHeadTrackingPlugin.Logger?.LogWarning("[CameraQuadPatch] CameraQuad.LateUpdate method not found");
                    return null;
                }

                patchActive = true;
                PeakHeadTrackingPlugin.Logger?.LogInfo("[CameraQuadPatch] Successfully targeting CameraQuad.LateUpdate");
                return method;
            }
            catch (Exception ex)
            {
                PeakHeadTrackingPlugin.Logger?.LogError($"[CameraQuadPatch] Error finding target method: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// PREFIX: Temporarily set cam.transform.rotation to include head tracking
        /// so CameraQuad reads the correct forward direction for quad positioning.
        /// </summary>
        [HarmonyPrefix]
        public static void LateUpdate_Prefix()
        {
            if (!patchActive)
                return;

            if (!CameraPatches.IsHeadTrackingEnabled())
                return;

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null)
                return;

            float yaw = CameraPatches.ProcessedYaw;
            float pitch = CameraPatches.ProcessedPitch;
            float roll = CameraPatches.ProcessedRoll;

            // Store original rotation for POSTFIX restore
            storedRotation = cam.transform.rotation;

            // Temporarily set transform to head-tracked rotation
            cam.transform.rotation = CameraRotationComposer.ComposeAdditive(storedRotation, yaw, pitch, roll);
        }

        /// <summary>
        /// POSTFIX: Restore the camera transform rotation after CameraQuad has read it.
        /// </summary>
        [HarmonyPostfix]
        public static void LateUpdate_Postfix()
        {
            if (!patchActive)
                return;

            if (!CameraPatches.IsHeadTrackingEnabled())
                return;

            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam == null)
                return;

            // Restore original rotation
            cam.transform.rotation = storedRotation;
        }
    }
}
