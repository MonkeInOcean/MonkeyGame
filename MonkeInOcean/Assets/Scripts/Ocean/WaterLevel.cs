namespace Ocean
{
    /// <summary>
    /// Canonical world-space height of the ocean surface.
    /// This is the single source of truth the rendering stack (ocean mesh,
    /// underwater renderer feature, caustics) keys off. Gameplay scripts
    /// (PlayerMovement, UnderwaterFog, the seeders) historically stored their
    /// own serialized copies — keep those in sync with this value in the
    /// inspector. OceanController defaults its surface height from here.
    /// </summary>
    public static class WaterLevel
    {
        /// <summary>World Y of the flat rest-height of the ocean.</summary>
        public const float SurfaceY = 95f;
    }
}
