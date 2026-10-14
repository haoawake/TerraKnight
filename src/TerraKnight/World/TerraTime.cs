namespace TerraKnight.World
{
    /// <summary>
    /// Terraria's clock on the play time (accarmor-hm sec-G2 §0.4): a day is 1440 s (86400
    /// ticks), its first 900 s daytime (dayLength 54000) and the last 540 s night (nightLength
    /// 32400); the moon phase turns at dawn (<see cref="Town.TownNpcs.MoonPhase"/>). Hallownest
    /// has no sky to show it; the werewolf and the sun and moon stones go by it. No eclipse.
    /// </summary>
    internal static class TerraTime
    {
        public const float DaySeconds = 900f, FullDaySeconds = 1440f;

        /// <summary>Dev: forces day (true) or night (false).</summary>
        public static bool? DevForceDay;

        /// <summary>In a LAN game, how far the host's clock is ahead of this save's (SetTime): everyone has the host's day.</summary>
        public static float NetOffset;

        /// <summary>Seconds into the current Terraria day.</summary>
        public static float Now => PlayerData.instance == null ? 0f : ((PlayerData.instance.playTime + NetOffset) % FullDaySeconds + FullDaySeconds) % FullDaySeconds;

        /// <summary>Main.dayTime.</summary>
        public static bool IsDay => DevForceDay ?? Now < DaySeconds;
    }
}
