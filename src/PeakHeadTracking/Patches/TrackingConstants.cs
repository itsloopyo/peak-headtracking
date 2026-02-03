namespace PeakHeadTracking.Patches
{
    /// <summary>
    /// Shared constants for head tracking processing
    /// </summary>
    internal static class TrackingConstants
    {
        /// <summary>
        /// Skip processing when head movement is below this threshold (degrees)
        /// </summary>
        internal const float MovementThreshold = 0.1f;
    }
}
