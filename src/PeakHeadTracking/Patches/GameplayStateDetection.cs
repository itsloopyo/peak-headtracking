using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeakHeadTracking.Patches
{
    /// <summary>
    /// Detects gameplay state — determines whether head tracking should be active
    /// based on scene, character presence, pause menu, and loading screen state.
    /// Uses compiled delegates for fast field access (~10-100x faster than FieldInfo.GetValue).
    /// </summary>
    internal static class GameplayStateDetection
    {
        // Loading screen detection - compiled delegate
        private static bool loadingReflectionInitialized = false;
        private static Func<bool> getIsLoading;

        // Gameplay state detection - cached reflection with compiled delegates
        private static Type characterType;
        private static bool gameplayReflectionInitialized = false;

        // Compiled delegates for fast gameplay state checks (replaces FieldInfo.GetValue)
        private static Func<object> getLocalCharacter;     // Character.localCharacter (static)
        private static Func<object> getGUIManagerInstance; // GUIManager.instance (static)
        private static Func<object, object> getPauseMenu;  // GUIManager.pauseMenu

        // Scene caching - avoid string comparison every frame
        private static string cachedSceneName = "";
        private static bool isOnTitleScene = false;
        private const string TitleSceneName = "Title";

        /// <summary>
        /// Update scene cache when scene changes. Call this from scene load handlers.
        /// </summary>
        private static void UpdateSceneCache()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene != cachedSceneName)
            {
                cachedSceneName = currentScene;
                isOnTitleScene = currentScene == TitleSceneName;

                // Invalidate reticle cache on scene change
                ReticleCompensation.InvalidateCache();
            }
        }

        /// <summary>
        /// Check if we should skip head tracking.
        /// Only enable during active gameplay (Character.localCharacter exists, not paused, not loading).
        /// Uses compiled delegates for fast field access (~10-100x faster than FieldInfo.GetValue).
        /// </summary>
        internal static bool ShouldSkipHeadTracking()
        {
            // Update scene cache and check if on Title scene
            UpdateSceneCache();
            if (isOnTitleScene)
                return true;

            if (!gameplayReflectionInitialized)
            {
                gameplayReflectionInitialized = true;

                characterType = Type.GetType(GameTypeNames.Character);
                if (characterType != null)
                {
                    var localCharacterField = characterType.GetField("localCharacter", BindingFlags.Public | BindingFlags.Static);
                    if (localCharacterField != null)
                    {
                        getLocalCharacter = ReflectionUtils.CreateStaticFieldGetter<object>(localCharacterField);
                    }
                }

                // TODO: pause menu detection is disabled due to initialization order — ReticleCompensation.InitializeReticleReflection() runs after this on first frame
                if (ReticleCompensation.GUIManagerType != null && ReticleCompensation.GUIManagerInstanceField != null)
                {
                    getGUIManagerInstance = ReflectionUtils.CreateStaticFieldGetter<object>(ReticleCompensation.GUIManagerInstanceField);

                    var pauseMenuField = ReticleCompensation.GUIManagerType.GetField("pauseMenu", BindingFlags.Public | BindingFlags.Instance);
                    if (pauseMenuField != null)
                    {
                        getPauseMenu = ReflectionUtils.CreateInstanceFieldGetter<object>(ReticleCompensation.GUIManagerType, pauseMenuField);
                    }
                }

                PeakHeadTrackingPlugin.Logger?.LogInfo($"[GameplayDetection] Compiled delegates - localCharacter: {getLocalCharacter != null}, guiInstance: {getGUIManagerInstance != null}, pauseMenu: {getPauseMenu != null}");
            }

            // Check if Character.localCharacter exists (we're in gameplay) using compiled delegate
            if (getLocalCharacter == null)
            {
                // Reflection failed during initialization - this is a fatal configuration error
                throw new InvalidOperationException(
                    "Cannot detect gameplay state: getLocalCharacter delegate was not compiled during reflection setup");
            }

            object localChar = getLocalCharacter();
            if (localChar == null)
                return true; // Skip - no local character (menu/loading)

            // Check if pause menu is active using compiled delegates
            if (getGUIManagerInstance != null && getPauseMenu != null)
            {
                object guiInstance = getGUIManagerInstance();
                if (guiInstance != null)
                {
                    object pauseMenu = getPauseMenu(guiInstance);
                    if (pauseMenu is GameObject pauseMenuGO && pauseMenuGO.activeSelf)
                        return true; // Skip - game is paused
                }
            }

            if (!loadingReflectionInitialized)
            {
                loadingReflectionInitialized = true;

                var loadingScreenHandlerType = Type.GetType(GameTypeNames.LoadingScreenHandler);
                if (loadingScreenHandlerType != null)
                {
                    var loadingPropertyInfo = loadingScreenHandlerType.GetProperty("loading", BindingFlags.Public | BindingFlags.Static);
                    if (loadingPropertyInfo != null)
                    {
                        getIsLoading = ReflectionUtils.CreateStaticPropertyGetter<bool>(loadingPropertyInfo);
                    }
                }
            }

            if (getIsLoading != null)
            {
                if (getIsLoading())
                    return true; // Skip - loading screen active
            }

            return false; // Don't skip - we're in active gameplay
        }
    }
}
