using System;
using System.Reflection;
using CameraUnlock.Core.Reflection;
using UnityEngine.SceneManagement;

namespace PeakHeadTracking.Patches
{
    /// <summary>
    /// Detects gameplay state - determines whether head tracking should be active
    /// based on scene, character presence, pause menu, and loading screen state.
    /// Uses compiled delegates for fast field access (~10-100x faster than FieldInfo.GetValue).
    /// </summary>
    internal static class GameplayStateDetection
    {
        private static bool reflectionInitialized = false;

        private static Func<object> getLocalCharacter; // Character.localCharacter (static)
        private static Func<bool> getInPauseMenu;      // GUIManager.InPauseMenu (static)
        private static Func<bool> getIsLoading;        // LoadingScreenHandler.loading (static)

        // Scene.name allocates a new string on every read, so the title check follows the
        // active scene change event instead of reading the name each frame.
        private static bool isOnTitleScene = false;
        private const string TitleSceneName = "Title";

        private static void OnActiveSceneChanged(Scene previous, Scene next)
        {
            isOnTitleScene = next.name == TitleSceneName;
            ReticleCompensation.InvalidateCache();
        }

        private static void InitializeReflection()
        {
            reflectionInitialized = true;

            isOnTitleScene = SceneManager.GetActiveScene().name == TitleSceneName;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            var characterType = Type.GetType(GameTypeNames.Character);
            var localCharacterField = characterType?.GetField("localCharacter", BindingFlags.Public | BindingFlags.Static);
            if (localCharacterField != null)
            {
                getLocalCharacter = CompiledGetters.ForStaticField(localCharacterField);
            }

            var guiManagerType = Type.GetType(GameTypeNames.GUIManager);
            var inPauseMenuProperty = guiManagerType?.GetProperty("InPauseMenu", BindingFlags.Public | BindingFlags.Static);
            if (inPauseMenuProperty != null)
            {
                getInPauseMenu = CompiledGetters.ForStaticProperty<bool>(inPauseMenuProperty);
            }

            var loadingScreenHandlerType = Type.GetType(GameTypeNames.LoadingScreenHandler);
            var loadingProperty = loadingScreenHandlerType?.GetProperty("loading", BindingFlags.Public | BindingFlags.Static);
            if (loadingProperty != null)
            {
                getIsLoading = CompiledGetters.ForStaticProperty<bool>(loadingProperty);
            }

            PeakHeadTrackingPlugin.Logger?.LogInfo($"[GameplayDetection] Compiled delegates - localCharacter: {getLocalCharacter != null}, inPauseMenu: {getInPauseMenu != null}, loading: {getIsLoading != null}");
        }

        /// <summary>
        /// Check if we should skip head tracking.
        /// Only enable during active gameplay (Character.localCharacter exists, not paused, not loading).
        /// </summary>
        internal static bool ShouldSkipHeadTracking()
        {
            if (!reflectionInitialized)
            {
                InitializeReflection();
            }

            if (isOnTitleScene)
                return true;

            if (getLocalCharacter == null)
            {
                throw new InvalidOperationException(
                    "Cannot detect gameplay state: Character.localCharacter was not found in Assembly-CSharp");
            }

            if (getLocalCharacter() == null)
                return true; // Skip - no local character (menu/loading)

            if (getInPauseMenu != null && getInPauseMenu())
                return true; // Skip - game is paused

            if (getIsLoading != null && getIsLoading())
                return true; // Skip - loading screen active

            return false; // Don't skip - we're in active gameplay
        }
    }
}
