using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TexturePacker.Net.Packager
{
    public class Packager
    {
        static readonly ThreadLocal<MaxRectsBinPack> MaxRectsBinPacker = new ThreadLocal<MaxRectsBinPack>(() => new MaxRectsBinPack());

        public static List<Rect> Pack(List<Rect> rects, Option option, out int width, out int height, ProgressRef progress)
        {
            if (rects.Count == 0)
            {
                progress.ForceAllDone();
                width = height = 0;
                return null;
            }
            int maxSide = option.MaxSide;
            bool allowRotation = option.AllowRotation;
            progress.MethodCount = (int)MaxRectsBinPack.FreeRectChoiceHeuristic.End;

            DateTime packStart = DateTime.UtcNow;
            Bin[] bestResults = new Bin[(int)MaxRectsBinPack.FreeRectChoiceHeuristic.End];
            int totalArea = rects.Sum(r => r.Width * r.Height);
            int heuristic = (int)MaxRectsBinPack.FreeRectChoiceHeuristic.Start;
            int maxEdge = Math.Max(rects.Max(r => r.Width), rects.Max(r => r.Height));
            int maxWidthIncrement = Math.Max(1, Math.Min(option.MaxWidthInrement, (int)Math.Sqrt(totalArea) / 1024 + (int)Math.Pow(rects.Count / 50, 3)));
            int maxHeightDecreasement = Math.Max(1, maxWidthIncrement / 20);
            Parallel.For(0, Environment.ProcessorCount - 0, _ =>
            {
                while (true)
                {
                    var h = Interlocked.Increment(ref heuristic);
                    if (h >= (int)MaxRectsBinPack.FreeRectChoiceHeuristic.End
                        || (h == (int)MaxRectsBinPack.FreeRectChoiceHeuristic.RectContactPointRule && rects.Count > 100))
                    {
                        if (h < (int)MaxRectsBinPack.FreeRectChoiceHeuristic.End)
                        {
                            progress.IncreaseMethodsStarted();
                        }
                        return;
                    }
                    DateTime start = DateTime.UtcNow;
                    var method = (MaxRectsBinPack.FreeRectChoiceHeuristic)h;
                    int rounds0 = 0, rounds1 = 0, rounds2 = 0, rounds3 = 0, rounds4 = 0, rounds5 = 0, rounds6 = 0;
                    MaxRectsBinPack bin = MaxRectsBinPacker.Value;
                    List<Rect> feasibleResult = null;

                    int area = (int)(totalArea * 1.01);
                    // To limit height not exceed "maxSide", I include area/maxSide
                    int width = Math.Max(Math.Max(area / maxSide, (int)Math.Sqrt(area) / 2), maxEdge);
                    int height = area / width;
                    while (true)
                    {
                        width += Math.Min(maxSide, Math.Max((int)(width * rects.Count / 5000.0f), 1));
                        if (width >= maxSide)
                        {
                            width = maxSide;
                            height += Math.Max(height / 100, 1);
                            if  (height >= maxSide)
                            {
                                height = maxSide;
                            }
                        }

                        var rectsClone = new List<Rect>(rects);
                        bin.Init(width, height, allowRotation);
                        feasibleResult = bin.Insert(rectsClone, method);
                        rounds0++;
                        if (rectsClone.Count == 0 || (height == maxSide && width == maxSide))
                        {
                            ShrinkRect(feasibleResult, out width, out height);
                            int heightDec = 2;
                            while (true)
                            {
                                rectsClone = new List<Rect>(rects);
                                bin.Init(width, height, allowRotation);
                                var newFeasibleResult = bin.Insert(rectsClone, method);
                                rounds0++;
                                if (rectsClone.Count == 0)
                                {
                                    height -= heightDec;
                                    feasibleResult = newFeasibleResult;
                                }
                                else if (heightDec > 1)
                                {
                                    height += heightDec;
                                    heightDec /= 2;
                                    height -= heightDec;
                                }
                                else
                                {
                                    break;
                                }
                            }
                            break;
                        }
                    }
                    progress.IncreaseFull(rounds0);
                    if (feasibleResult.Count != rects.Count)
                    {
                        progress.IncreaseDone(rounds0);
                        return;
                    }
                    Thread.Sleep(1);
                    Debug.WriteLine($"{progress.Progress:P}");
                    ShrinkRect(feasibleResult, out width, out height);
                    int startArea = area = width * height;
                    List<Rect> minRects = feasibleResult;
                    int minWidth = width;
                    int minHeight = height;
                    // We found an area can fit all rects, now we try to narrow this area
                    int heightDecreasement = maxHeightDecreasement;
                    int maxWidth = (int)Math.Sqrt(area);
                    width = Math.Max(maxEdge, area / maxSide);
                    int countLoopWidth = 0;
                    progress.IncreaseFull((maxWidth - width) / maxWidthIncrement + 1);
                    progress.IncreaseDone(rounds0);
                    progress.IncreaseMethodsStarted();
                    while (true)
                    {
                        countLoopWidth++;
                        if (countLoopWidth % 20 == 0)
                        {
                            Thread.Sleep(1);
                            Debug.WriteLine($"{progress.Progress:P}");
                        }
                        height = area / width;
                        bool foundNewMinBin = false;
                        while (true)
                        {
                            var rectsClone = new List<Rect>(rects);
                            bin.Init(width, height, allowRotation);
                            feasibleResult = bin.Insert(rectsClone, method);
                            if (rectsClone.Count == 0)
                            {
                                rounds1++;
                                if (width * height < area)
                                {
                                    foundNewMinBin = true;
                                    minRects = feasibleResult;
                                    area = width * height;
                                    minWidth = width;
                                    minHeight = height;
                                }
                                height -= heightDecreasement;
                            }
                            else if (heightDecreasement > 1)
                            {
                                rounds2++;
                                height += heightDecreasement;
                                heightDecreasement /= 2;
                                height -= heightDecreasement;
                            }
                            else
                            {
                                if (foundNewMinBin)
                                {
                                    // Cannot decrease more height, try decrease some width
                                    height += 1;
                                    int widthDecreasement = Math.Max(1, maxWidthIncrement / 20);
                                    int lastWidth = width;
                                    while (true)
                                    {
                                        width -= widthDecreasement;
                                        rectsClone = new List<Rect>(rects);
                                        bin.Init(width, height, allowRotation);
                                        feasibleResult = bin.Insert(rectsClone, method);
                                        if (rectsClone.Count == 0)
                                        {
                                            rounds4++;
                                            if (width * height < area)
                                            {
                                                minRects = feasibleResult;
                                                area = width * height;
                                                minWidth = width;
                                                minHeight = height;
                                            }
                                        }
                                        else if (widthDecreasement > 1)
                                        {
                                            rounds5++;
                                            // Go back and decrease the decreasement
                                            width += widthDecreasement;
                                            widthDecreasement /= 2;
                                        }
                                        else
                                        {
                                            rounds6++;
                                            break;
                                        }
                                    }
                                    width = lastWidth;
                                }
                                else
                                {
                                    rounds3++;
                                }

                                heightDecreasement = maxHeightDecreasement;
                                break;
                            }
                        }
                        progress.IncrementDone();
                        if (width == maxWidth)
                        {
                            break;
                        }
                        width = Math.Min(width + maxWidthIncrement, maxWidth);
                    }

                    bestResults[h] = new Bin()
                    {
                        width = minWidth,
                        height = minHeight,
                        list = minRects
                    };
                    int totalRounds = rounds1 + rounds2 + rounds3 + rounds4 + rounds5 + rounds6;
                    File.AppendAllTextAsync($"{method}.log", $"{rounds0} | {rounds1}+{rounds2}+{rounds3}+{rounds4}+{rounds5}+{rounds6}" +
                        $"={totalRounds} | {countLoopWidth} | {(float)totalArea / area:P} | {(DateTime.UtcNow - start).TotalSeconds}" +
                        $" | {totalArea}|{maxWidth}|{maxEdge}| {startArea}|{minWidth}x{minHeight}={area} | {maxWidthIncrement}x{maxHeightDecreasement}\n");
                }
            });

            progress.ForceAllDone();
            var bestResult = bestResults.Min();
            width = bestResult.width;
            height = bestResult.height;
            File.AppendAllTextAsync("RectTime.log", $"{(DateTime.UtcNow - packStart).TotalSeconds}\n");
            return bestResult.list;
        }

        static void ShrinkRect(List<Rect> rects, out int width, out int height)
        {
            width = int.MinValue;
            height = int.MinValue;
            for (int i = rects.Count - 1; i >= 0; i--)
            {
                Rect rect = rects[i];
                if (rect.FarX > width)
                {
                    width = rect.FarX;
                }
                if (rect.FarY > height)
                {
                    height = rect.FarY;
                }
            }
        }
    }

    public class Option
    {
        public int MaxSide { get; set; }

        public bool AllowRotation { get; set; }

        public byte MaxWidthInrement { get; set; }

        public Option()
        {
            MaxWidthInrement = 100;
            MaxSide = 2048;
            AllowRotation = true;
        }
    }

    [DebuggerDisplay("{ToString(),nq}, ")]
    public class ProgressRef
    {
        private int Done = 0;
        private int Full = 1;
        private int MethodsStarted;
        public int MethodCount;

        private readonly object lock1 = new object();
        private readonly object lock2 = new object();

        public void IncreaseMethodsStarted()
        {
            Interlocked.Increment(ref MethodsStarted);
        }

        public void IncreaseFull(int amount)
        {
            lock (lock1)
            {
                Full += amount;
            }
        }

        public void IncrementDone()
        {
            Interlocked.Increment(ref Done);
        }

        public void IncreaseDone(int amount)
        {
            lock (lock2)
            {
                Done += amount;
            }
        }

        public void ForceAllDone()
        {
            AllDone = true;
        }

        public bool AllDone { get; private set; }

        float lastProgress;

        public float Progress
        {
            get
            {
                float p = (float)Done / Full * MethodsStarted / MethodCount;
                lastProgress = p > lastProgress ? p : lastProgress;
                return lastProgress;
            }
        }

        public override string ToString()
        {
            return $"{lastProgress:P}|{Done}|{Full}|{MethodsStarted}|{MethodCount}";
        }
    }

    struct Bin : IComparable<Bin>
    {
        public int width;
        public int height;
        public List<Rect> list;

        public int CompareTo([AllowNull] Bin other)
        {
            if (list == null)
            {
                return int.MaxValue;
            }
            if (other.list == null)
            {
                return int.MinValue;
            }
            return width * height - other.width * other.height;
        }
    }
}
