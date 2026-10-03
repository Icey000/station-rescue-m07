using System;

namespace UnityAgentLab
{
    /// <summary>One immutable plan per run. Random choices never change during play.</summary>
    public sealed class StationPlan
    {
        private static int lastExit = -1;
        public int Seed { get; }
        public ModuleKind RequiredKind { get; }
        public int ExitIndex { get; }
        public int BlueShelf { get; }
        public int OrangeShelf { get; }

        private StationPlan(int seed, int exitCount, int shelfCount, bool avoidRepeat)
        {
            if (exitCount < 1 || shelfCount < 2) throw new ArgumentException("Station needs gates and shelves.");
            Seed = seed;
            var random = new Random(seed);
            RequiredKind = (ModuleKind)random.Next(2);
            int selected = random.Next(exitCount);
            if (avoidRepeat && exitCount > 1 && selected == lastExit)
                selected = (selected + 1 + random.Next(exitCount - 1)) % exitCount;
            ExitIndex = selected;
            BlueShelf = random.Next(shelfCount);
            int other = random.Next(shelfCount - 1);
            OrangeShelf = other >= BlueShelf ? other + 1 : other;
            if (avoidRepeat) lastExit = selected;
        }

        public static StationPlan Create(int seed, int exitCount, int shelfCount)
            => new StationPlan(seed, exitCount, shelfCount, false);
        public static StationPlan NewRun(int exitCount, int shelfCount)
            => new StationPlan(Environment.TickCount ^ Guid.NewGuid().GetHashCode(), exitCount, shelfCount, true);
        public static string Name(ModuleKind kind)
            => kind == ModuleKind.BlueCircle ? "蓝色圆柱电芯" : "橙色方盒电芯";
        public static string ShortName(ModuleKind kind)
            => kind == ModuleKind.BlueCircle ? "● 蓝色圆柱" : "■ 橙色方盒";
    }
}
