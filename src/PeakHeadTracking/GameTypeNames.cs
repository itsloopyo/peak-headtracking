namespace PeakHeadTracking
{
    /// <summary>
    /// Centralized constants for game type names used in reflection.
    /// If the game refactors type names, only this file needs to be updated.
    /// </summary>
    public static class GameTypeNames
    {
        private const string AssemblyName = "Assembly-CSharp";

        /// <summary>
        /// Full type name for Character class (for Type.GetType).
        /// </summary>
        public const string Character = "Character, " + AssemblyName;

        /// <summary>
        /// Full type name for CharacterAnimations class (for Type.GetType).
        /// </summary>
        public const string CharacterAnimations = "CharacterAnimations, " + AssemblyName;

        /// <summary>
        /// Full type name for GUIManager class (for Type.GetType).
        /// </summary>
        public const string GUIManager = "GUIManager, " + AssemblyName;

        /// <summary>
        /// Full type name for LoadingScreenHandler class (for Type.GetType).
        /// </summary>
        public const string LoadingScreenHandler = "LoadingScreenHandler, " + AssemblyName;

        /// <summary>
        /// Short type name for CameraQuad class (for AccessTools.TypeByName).
        /// </summary>
        public const string CameraQuad = "CameraQuad";
    }
}
