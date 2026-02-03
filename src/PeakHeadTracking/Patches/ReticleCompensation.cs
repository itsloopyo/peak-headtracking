using System;
using System.Reflection;
using CameraUnlock.Core.Unity.Extensions;
using UnityEngine;

namespace PeakHeadTracking.Patches
{
    /// <summary>
    /// Handles reticle compensation — moves the crosshair to show where the avatar is actually aiming
    /// when the camera is rotated by head tracking.
    /// </summary>
    internal static class ReticleCompensation
    {
        // Reticle compensation - cached reflection
        private static Type guiManagerType;
        private static FieldInfo instanceField;
        private static bool reticleReflectionInitialized = false;

        // Compiled delegates for FindReticleParent hot path
        private static Func<object> getGUIManagerInstance;
        private static Func<object, object> getReticleDefault;
        private static bool reticleParentFound = false;
        private static RectTransform reticleParentTransform;
        private static Canvas cachedReticleCanvas;
        private static float cachedCanvasScaleFactor = 1f;

        // Interaction text compensation — move individual elements, not the parent
        // (the parent also contains item prompts like "Open" which must stay fixed)
        private static Func<object, object> getInteractName;
        private static Func<object, object> getInteractPromptPrimary;
        private static Func<object, object> getInteractPromptSecondary;
        private static Func<object, object> getInteractPromptHold;
        private static Func<object, object> getInteractPromptLunge;
        private static RectTransform[] interactTransforms;
        private static bool interactElementsFound = false;
        private static bool interactElementsSearched = false;

        /// <summary>
        /// Exposes the GUIManager Type for GameplayStateDetection to check pause menu.
        /// </summary>
        internal static Type GUIManagerType => guiManagerType;

        /// <summary>
        /// Exposes the GUIManager instance FieldInfo for GameplayStateDetection.
        /// </summary>
        internal static FieldInfo GUIManagerInstanceField => instanceField;

        /// <summary>
        /// Initialize reticle reflection for compensation
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when required types or fields are not found</exception>
        internal static void InitializeReticleReflection()
        {
            if (reticleReflectionInitialized) return;
            reticleReflectionInitialized = true;

            guiManagerType = Type.GetType(GameTypeNames.GUIManager);
            if (guiManagerType == null)
            {
                throw new InvalidOperationException("[Reticle] GUIManager type not found - game version may be incompatible");
            }

            instanceField = guiManagerType.GetField("instance", BindingFlags.Public | BindingFlags.Static);
            var reticleDefaultField = guiManagerType.GetField("reticleDefault", BindingFlags.Public | BindingFlags.Instance);

            if (instanceField == null || reticleDefaultField == null)
            {
                throw new InvalidOperationException("[Reticle] GUIManager required fields not found - game version may be incompatible");
            }

            getGUIManagerInstance = ReflectionUtils.CreateStaticFieldGetter<object>(instanceField);
            getReticleDefault = ReflectionUtils.CreateInstanceFieldGetter<object>(guiManagerType, reticleDefaultField);

            // Interaction text elements — optional, don't fail if missing
            string[] interactFields = { "interactName", "interactPromptPrimary", "interactPromptSecondary", "interactPromptHold", "interactPromptLunge" };
            Func<object, object>[] interactGetters = new Func<object, object>[interactFields.Length];
            for (int i = 0; i < interactFields.Length; i++)
            {
                var field = guiManagerType.GetField(interactFields[i], BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                    interactGetters[i] = ReflectionUtils.CreateInstanceFieldGetter<object>(guiManagerType, field);
            }
            getInteractName = interactGetters[0];
            getInteractPromptPrimary = interactGetters[1];
            getInteractPromptSecondary = interactGetters[2];
            getInteractPromptHold = interactGetters[3];
            getInteractPromptLunge = interactGetters[4];

            PeakHeadTrackingPlugin.Logger?.LogInfo("[Reticle] Reflection initialized successfully");
        }

        /// <summary>
        /// Find and cache the reticle parent RectTransform and Canvas.
        /// All reticles share a common parent, so moving the parent moves all reticles.
        /// Re-finds if it becomes invalid (e.g., scene change).
        /// Also caches the Canvas reference to avoid GetComponentInParent every frame.
        /// </summary>
        private static void FindReticleParent()
        {
            if (guiManagerType == null) return;

            // Check if cached parent is still valid
            if (reticleParentFound && reticleParentTransform != null)
            {
                return;
            }

            // Need to re-find (either first time or after scene change)
            if (reticleParentFound)
            {
                PeakHeadTrackingPlugin.Logger?.LogInfo("[Reticle] Cached reticle parent became invalid, re-finding...");
                reticleParentFound = false;
                reticleParentTransform = null;
                cachedReticleCanvas = null;
                cachedCanvasScaleFactor = 1f;
            }

            var instance = getGUIManagerInstance();
            if (instance == null) return;

            var reticleDefault = getReticleDefault(instance) as GameObject;
            if (reticleDefault == null) return;

            // Get the parent of reticleDefault - this contains all reticle types
            Transform parent = reticleDefault.transform.parent;
            if (parent != null)
            {
                reticleParentTransform = parent.GetComponent<RectTransform>();
                if (reticleParentTransform != null)
                {
                    reticleParentFound = true;

                    // Cache Canvas reference to avoid GetComponentInParent every frame
                    cachedReticleCanvas = reticleParentTransform.GetComponentInParent<Canvas>();
                    if (cachedReticleCanvas != null && cachedReticleCanvas.scaleFactor > 0)
                    {
                        cachedCanvasScaleFactor = cachedReticleCanvas.scaleFactor;
                    }
                    else
                    {
                        cachedCanvasScaleFactor = 1f;
                    }

                    PeakHeadTrackingPlugin.Logger?.LogInfo($"[Reticle] Found reticle parent: {parent.name}, canvas scale: {cachedCanvasScaleFactor}");
                }
            }
        }

        /// <summary>
        /// Find and cache RectTransforms for individual interaction text elements.
        /// We move these individually because their parent also contains item prompts
        /// (e.g. "Open") that must stay fixed in their screen corner.
        /// </summary>
        private static void FindInteractElements()
        {
            if (interactElementsFound) return;
            if (interactElementsSearched) return;
            if (getInteractName == null || getGUIManagerInstance == null) return;

            interactElementsSearched = true;

            var instance = getGUIManagerInstance();
            if (instance == null) { interactElementsSearched = false; return; }

            var getters = new[] { getInteractName, getInteractPromptPrimary, getInteractPromptSecondary, getInteractPromptHold, getInteractPromptLunge };
            var results = new System.Collections.Generic.List<RectTransform>();

            foreach (var getter in getters)
            {
                if (getter == null) continue;
                var go = getter(instance) as GameObject;
                if (go == null) continue;
                var rt = go.GetComponent<RectTransform>();
                if (rt != null) results.Add(rt);
            }

            if (results.Count > 0)
            {
                interactTransforms = results.ToArray();
                interactElementsFound = true;
                PeakHeadTrackingPlugin.Logger?.LogInfo($"[Reticle] Found {interactTransforms.Length} interact elements to compensate");
            }
        }

        /// <summary>
        /// Check if reticle position can be updated.
        /// </summary>
        internal static bool CanUpdateReticle()
        {
            InitializeReticleReflection();
            FindReticleParent();
            return reticleParentFound && reticleParentTransform != null;
        }

        /// <summary>
        /// Update reticle position - reticle shows where avatar is aiming (game's forward direction).
        /// This is projected through the head-tracked camera to show where aiming point appears on screen.
        /// Moves the parent container so all reticle types are affected.
        /// Uses shared CanvasCompensation utilities from cameraunlock-core.
        /// Uses cached Canvas scale factor (set during FindReticleParent) to avoid GetComponentInParent every frame.
        /// Precondition: CanUpdateReticle() must return true before calling.
        /// </summary>
        internal static void UpdateReticlePosition(UnityEngine.Camera cam)
        {
            // Caller must check CanUpdateReticle() first
            if (!reticleParentFound || reticleParentTransform == null)
            {
                throw new InvalidOperationException("Reticle parent transform not found. Caller must check CanUpdateReticle() before calling.");
            }

            // cam.transform.forward IS the game's aim direction because view matrix
            // modification doesn't touch the transform — only worldToCameraMatrix changes.
            // Distance doesn't matter: all points along the aim ray project to the same
            // screen pixel (eye position is unchanged). The 3-arg overload uses fixed 100f.
            Vector3 aimDir = cam.transform.forward;

            Vector2 offset = CanvasCompensation.CalculateAimScreenOffset(cam, aimDir, cachedCanvasScaleFactor);
            reticleParentTransform.anchoredPosition = offset;

            // Move interaction text elements to follow the reticle
            FindInteractElements();
            if (interactElementsFound)
            {
                foreach (var rt in interactTransforms)
                {
                    if (rt != null) rt.anchoredPosition = offset;
                }
            }
        }

        /// <summary>
        /// Reset reticle to center position (when tracking disabled)
        /// </summary>
        public static void ResetReticlePosition()
        {
            if (reticleParentFound && reticleParentTransform != null)
            {
                reticleParentTransform.anchoredPosition = Vector2.zero;
            }
            if (interactElementsFound)
            {
                foreach (var rt in interactTransforms)
                {
                    if (rt != null) rt.anchoredPosition = Vector2.zero;
                }
            }
        }

        /// <summary>
        /// Invalidate cached reticle references on scene change.
        /// Called by GameplayStateDetection when scene changes.
        /// </summary>
        internal static void InvalidateCache()
        {
            if (reticleParentFound && reticleParentTransform == null)
            {
                reticleParentFound = false;
                cachedReticleCanvas = null;
                cachedCanvasScaleFactor = 1f;
            }
            if (interactElementsFound)
            {
                bool anyInvalid = false;
                foreach (var rt in interactTransforms)
                {
                    if (rt == null) { anyInvalid = true; break; }
                }
                if (anyInvalid)
                {
                    interactElementsFound = false;
                    interactElementsSearched = false;
                    interactTransforms = null;
                }
            }
        }
    }
}
