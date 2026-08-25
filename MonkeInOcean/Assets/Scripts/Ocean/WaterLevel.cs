namespace Ocean
{
    /// <summary>
    /// Canonical world-space height of the ocean surface — the single source of
    /// truth for both rendering (ocean mesh, underwater renderer feature,
    /// caustics) and gameplay (PlayerMovement, the seeders, animals). All of
    /// those read this directly instead of keeping their own serialized copies,
    /// so the water line can never drift out of sync. Change it here only.
    /// </summary>
    public static class WaterLevel
    {
        /// <summary>World Y of the flat rest-height of the ocean.</summary>
        public const float SurfaceY = 95f;
    }
}
