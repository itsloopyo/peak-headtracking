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
        private static bool quadModified = false;
        private static Quaternion storedRotation;
        private static Vector3 storedPosition;
        private static float storedNearClip;

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
        /// PREFIX: Temporarily set the camera transform + near clip to the exact pose the
        /// render injects, so CameraQuad places, sizes, and orients the near-clip overlay quad
        /// to fill the head-tracked view instead of the clean view.
        /// </summary>
        [HarmonyPrefix]
        public static void LateUpdate_Prefix()
        {
            if (!patchActive)
                return;

            if (!CameraPatches.IsHeadTrackingEnabled())
                return;

            // Mirror the render, which suppresses head tracking outside active gameplay.
            // Moving the quad here while the render leaves the view clean would desync the
            // fog overlay from the view during menus, loading, and transitions.
            if (GameplayStateDetection.ShouldSkipHeadTracking())
                return;

            UnityEngine.Camera cam = CameraPatches.MainCamera;
            if (cam == null)
                return;

            float yaw = CameraPatches.ProcessedYaw;
            float pitch = CameraPatches.ProcessedPitch;
            float roll = CameraPatches.ProcessedRoll;

            storedRotation = cam.transform.rotation;
            storedPosition = cam.transform.position;
            storedNearClip = cam.nearClipPlane;
            quadModified = true;

            // Reproduce the camera pose the render injects into the view matrix.
            //
            // Rotation: world-space-yaw composition with -roll. Both render paths apply -roll
            //   (ApplyHeadRotationDecomposed is called with -roll; ApplyHeadRotation negates roll
            //   internally), so passing raw +roll would tilt the quad opposite to the view.
            // Position (6DOF): the render translates the rotated view matrix by -posOffset in view
            //   space. The equivalent camera world position is camPos + finalRot * (ox, oy, -oz);
            //   the -oz is Unity's view-space Z flip. Without this the near quad (~0.16m away) stays
            //   put while the view leans, throwing the overlay badly off-centre.
            // Near clip: the render forces nearClip >= NearClipOverride. CameraQuad puts the quad at
            //   nearClip + 0.01, so it must build against the same plane or the render's larger near
            //   plane clips the quad away entirely.
            Quaternion finalRot = CameraRotationComposer.ComposeAdditive(storedRotation, yaw, pitch, -roll);
            Vector3 o = CameraPatches.ProcessedPositionOffset;
            Vector3 worldPosDelta = finalRot * new Vector3(o.x, o.y, -o.z);

            cam.transform.rotation = finalRot;
            cam.transform.position = storedPosition + worldPosDelta;
            cam.nearClipPlane = CameraPatches.GetEffectiveNearClip(storedNearClip);
        }

        /// <summary>
        /// POSTFIX: Restore the camera transform + near clip after CameraQuad has read them.
        /// Guarded by quadModified so a toggle/state flip between prefix and postfix can never
        /// leave the camera with the temporary pose applied.
        /// </summary>
        [HarmonyPostfix]
        public static void LateUpdate_Postfix()
        {
            if (!patchActive)
                return;

            if (!quadModified)
                return;
            quadModified = false;

            UnityEngine.Camera cam = CameraPatches.MainCamera;
            if (cam == null)
                return;

            cam.transform.rotation = storedRotation;
            cam.transform.position = storedPosition;
            cam.nearClipPlane = storedNearClip;
        }
    }
}
