using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PeakHeadTracking.Patches
{
    /// <summary>
    /// Harmony patch to make the character's 3D head model follow the player's real head direction.
    ///
    /// Key insight: The game uses Animator parameters "Look X" and "Look Y" to control head direction.
    /// These are set in CharacterAnimations.Update(). We patch this with a Postfix to add our
    /// head tracking offset to these animator parameters AFTER the game sets them.
    ///
    /// Performance optimization: Uses compiled expression delegates instead of reflection for
    /// field access in the hot path (~10-100x faster than FieldInfo.GetValue).
    /// </summary>
    [HarmonyPatch]
    public static class HeadRotationPatches
    {
        // Animator parameter hashes (same as game uses)
        private static readonly int AN_LOOK_Y = Animator.StringToHash("Look Y");
        private static readonly int AN_LOOK_X = Animator.StringToHash("Look X");

        // Normalization factor - converts degrees to -1..1 range (90 degrees -> 1.0)
        private const float DegreesNormalizationFactor = 90f;

        // Cached reflection types
        private static Type characterAnimationsType;
        private static Type characterType;

        // Compiled delegate accessors (10-100x faster than FieldInfo.GetValue)
        private static Func<object, object> getCharacterFromAnimations;  // CharacterAnimations.character
        private static Func<object> getLocalCharacter;                    // Character.localCharacter (static)
        private static Func<object, object> getRefsFromCharacter;         // Character.refs
        private static Func<object, Animator> getAnimatorFromRefs;        // CharacterRefs.animator

        private static bool reflectionInitialized = false;
        private static bool reflectionFailed = false;

        // For debugging
        private static bool hasLoggedSuccess = false;

        /// <summary>
        /// Target the CharacterAnimations.Update method
        /// </summary>
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            characterAnimationsType = Type.GetType(GameTypeNames.CharacterAnimations);
            if (characterAnimationsType == null)
            {
                throw new TypeLoadException("[HeadRotation] CharacterAnimations type not found in Assembly-CSharp");
            }

            var method = characterAnimationsType.GetMethod(
                "Update",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (method == null)
            {
                throw new MissingMethodException("[HeadRotation] CharacterAnimations.Update method not found");
            }

            PeakHeadTrackingPlugin.Logger?.LogInfo("[HeadRotation] Found target method: CharacterAnimations.Update");
            return method;
        }

        /// <summary>
        /// Initialize reflection to access game types.
        /// Compiles expression delegates for field access (10-100x faster than FieldInfo.GetValue).
        /// Throws if reflection previously failed or fails during initialization.
        /// </summary>
        private static void InitializeReflection()
        {
            if (reflectionFailed)
            {
                throw new InvalidOperationException("HeadRotationPatches reflection initialization previously failed. Cannot proceed.");
            }

            if (reflectionInitialized) return;
            reflectionInitialized = true;

            // Get CharacterAnimations type and character field
            if (characterAnimationsType == null)
            {
                characterAnimationsType = Type.GetType(GameTypeNames.CharacterAnimations);
            }

            if (characterAnimationsType == null)
            {
                reflectionFailed = true;
                throw new TypeLoadException("[HeadRotation] CharacterAnimations type not found in reflection init");
            }

            var characterField = characterAnimationsType.GetField("character", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (characterField == null)
            {
                reflectionFailed = true;
                throw new MissingFieldException("[HeadRotation] CharacterAnimations.character field not found");
            }

            // Get Character type and localCharacter static field
            characterType = Type.GetType(GameTypeNames.Character);
            if (characterType == null)
            {
                reflectionFailed = true;
                throw new TypeLoadException("[HeadRotation] Character type not found");
            }

            var localCharacterField = characterType.GetField("localCharacter", BindingFlags.Public | BindingFlags.Static);
            if (localCharacterField == null)
            {
                reflectionFailed = true;
                throw new MissingFieldException("[HeadRotation] Character.localCharacter field not found");
            }

            // Get Character.refs field
            var refsField = characterType.GetField("refs", BindingFlags.Public | BindingFlags.Instance);
            if (refsField == null)
            {
                reflectionFailed = true;
                throw new MissingFieldException("[HeadRotation] Character.refs field not found");
            }

            // Get CharacterRefs.animator field
            Type refsType = refsField.FieldType;
            var animatorField = refsType.GetField("animator", BindingFlags.Public | BindingFlags.Instance);
            if (animatorField == null)
            {
                reflectionFailed = true;
                throw new MissingFieldException("[HeadRotation] CharacterRefs.animator field not found");
            }

            // Compile expression delegates for fast field access
            getCharacterFromAnimations = ReflectionUtils.CreateInstanceFieldGetter<object>(characterAnimationsType, characterField);
            getLocalCharacter = ReflectionUtils.CreateStaticFieldGetter<object>(localCharacterField);
            getRefsFromCharacter = ReflectionUtils.CreateInstanceFieldGetter<object>(characterType, refsField);
            getAnimatorFromRefs = ReflectionUtils.CreateInstanceFieldGetter<Animator>(refsType, animatorField);

            PeakHeadTrackingPlugin.Logger?.LogInfo("[HeadRotation] Reflection initialized with compiled delegates");
        }


        /// <summary>
        /// Postfix that runs AFTER CharacterAnimations.Update sets the Look X/Y animator parameters.
        /// Adds head tracking offset to turn the character's head.
        /// </summary>
        [HarmonyPostfix]
        public static void Postfix(object __instance)
        {
            // Initialize reflection on first call - throws if failed
            InitializeReflection();

            // Check if head tracking is enabled
            if (!CameraPatches.IsHeadTrackingEnabled()) return;

            // Get head tracking offset (in degrees)
            float yaw = CameraPatches.CurrentYaw;
            float pitch = CameraPatches.CurrentPitch;

            // Skip if no significant head movement
            if (Mathf.Abs(yaw) < TrackingConstants.MovementThreshold && Mathf.Abs(pitch) < TrackingConstants.MovementThreshold) return;

            // Get the character from this CharacterAnimations instance (compiled delegate)
            object animCharacter = getCharacterFromAnimations(__instance);
            if (animCharacter == null) return;

            // Get the local character (only modify our own character) (compiled delegate)
            object localCharacter = getLocalCharacter();
            if (localCharacter == null) return;

            // Check if this CharacterAnimations belongs to the local character
            if (!ReferenceEquals(animCharacter, localCharacter)) return;

            // Get the animator (compiled delegates)
            object refs = getRefsFromCharacter(localCharacter);
            if (refs == null) return;

            Animator animator = getAnimatorFromRefs(refs);
            if (animator == null) return;

            // Get current Look X/Y values that the game just set
            float currentLookX = animator.GetFloat(AN_LOOK_X);
            float currentLookY = animator.GetFloat(AN_LOOK_Y);

            // Add head tracking offset
            // Look X is horizontal (yaw) - convert degrees to roughly -1 to 1 range
            // Look Y is vertical (pitch) - the game uses a value derived from forward.y
            float yawOffset = yaw / DegreesNormalizationFactor;
            float pitchOffset = pitch / DegreesNormalizationFactor;

            float newLookX = currentLookX + yawOffset;
            float newLookY = currentLookY - pitchOffset; // Negative because pitch up should look up

            // Set the modified values (without smoothing to get immediate response)
            animator.SetFloat(AN_LOOK_X, newLookX);
            animator.SetFloat(AN_LOOK_Y, newLookY);

            if (!hasLoggedSuccess)
            {
                PeakHeadTrackingPlugin.Logger?.LogInfo($"[HeadRotation] SUCCESS! Modified animator Look params: LookX {currentLookX:F2}->{newLookX:F2}, LookY {currentLookY:F2}->{newLookY:F2}");
                hasLoggedSuccess = true;
            }
        }
    }
}

