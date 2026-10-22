using System;

namespace PocketRoulette
{
    public static class EventTiming
    {
        public static float SpawnAfter(float deadlineSeconds, float nativeFuse) => Math.Max(0, deadlineSeconds - nativeFuse);
        public static float Prewarm(float deadlineSeconds, float nativeFuse) => Math.Max(0, nativeFuse - deadlineSeconds);
        public static float CursedDelay(float min, float max, double random, double luck, double bias) =>
            min + (max - min) * (float)Math.Pow(Math.Max(0, Math.Min(1, random)), 1 / (1 + Math.Max(0, luck * bias)));
    }
}
