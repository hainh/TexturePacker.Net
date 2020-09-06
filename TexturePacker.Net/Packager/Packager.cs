using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TexturePacker.Net.Packager
{
    public class Packager
    {
        static readonly ThreadLocal<MaxRectsBinPack> MaxRectsBinPacker = new ThreadLocal<MaxRectsBinPack>(() => new MaxRectsBinPack());

        public static List<Rect> Pack(List<Rect> rects, out int width, out int height)
        {
            DateTime packStart = DateTime.UtcNow;
            Bin[] bestResults = new Bin[(int)MaxRectsBinPack.FreeRectChoiceHeuristic.End];
            int totalArea = rects.Sum(r => r.Width * r.Height);
            int heuristic = (int)MaxRectsBinPack.FreeRectChoiceHeuristic.Start;
            int maxEdge = Math.Max(rects.Max(r => r.Width), rects.Max(r => r.Height));
            int maxWidthIncreasement = 6;//Math.Max(1, (int)Math.Sqrt(totalArea) * rects.Count / 200000);
            int maxHeightDecreasement = 1;// Math.Max(1, maxWidthIncreasement / 20);
            Parallel.For(0, Environment.ProcessorCount - 1, _ =>
            {
                while (true)
                {
                    var h = Interlocked.Increment(ref heuristic);
                    if (h >= (int)MaxRectsBinPack.FreeRectChoiceHeuristic.End
                        || (h == (int)MaxRectsBinPack.FreeRectChoiceHeuristic.RectContactPointRule && rects.Count > 100))
                    {
                        return;
                    }
                    DateTime start = DateTime.UtcNow;
                    var previousPriority = Thread.CurrentThread.Priority;
                    Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                    var method = (MaxRectsBinPack.FreeRectChoiceHeuristic)h;
                    int rounds0 = 0, rounds1 = 0, rounds2 = 0, rounds3 = 0, rounds4 = 0, rounds5 = 0, rounds6 = 0;
                    MaxRectsBinPack bin = MaxRectsBinPacker.Value;
                    List<Rect> feasibleResult = null;

                    int area = (int)(totalArea * 1.01);
                    int width = Math.Max((int)Math.Sqrt(area) / 2, maxEdge);
                    int height = area / width + 1;
                    while (true)
                    {
                        var rectsClone = new List<Rect>(rects);
                        bin.Init(width, height, true);
                        feasibleResult = bin.Insert(rectsClone, method);
                        rounds0++;
                        if (rectsClone.Count == 0)
                        {
                            break;
                        }
                        width += Math.Max((int)(width * rects.Count / 5000.0f), 1);
                    }
                    int startArea = area = width * height;
                    List<Rect> minRects = feasibleResult;
                    int minWidth = width;
                    int minHeight = height;
                    // We found an area can fit all rects, now we try to narrow this area
                    int heightDecreasement = maxHeightDecreasement;
                    int maxWidth = (int)Math.Sqrt(area);
                    width = Math.Max(maxEdge, width / 2);
                    int countLoopWidth = 0;
                    while (true)
                    {
                        countLoopWidth++;
                        height = area / width;
                        bool foundNewMinBin = false;
                        while (true)
                        {
                            var rectsClone = new List<Rect>(rects);
                            bin.Init(width, height, true);
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
                                    int widthDecreasement = Math.Max(1, maxWidthIncreasement / 20);
                                    int lastWidth = width;
                                    while (true)
                                    {
                                        width -= widthDecreasement;
                                        rectsClone = new List<Rect>(rects);
                                        bin.Init(width, height, true);
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
                        if (width == maxWidth)
                        {
                            break;
                        }
                        width = Math.Min(width + maxWidthIncreasement, maxWidth);
                    }

                    Thread.CurrentThread.Priority = previousPriority;
                    bestResults[h] = new Bin()
                    {
                        width = minWidth,
                        height = minHeight,
                        list = minRects
                    };
                    int totalRounds = rounds1 + rounds2 + rounds3 + rounds4 + rounds5 + rounds6;
                    File.AppendAllTextAsync($"{method}.log", $"{rounds0} | {rounds1}+{rounds2}+{rounds3}+{rounds4}+{rounds5}+{rounds6}" +
                        $"={totalRounds} | {countLoopWidth} | {(float)totalArea / area:P} | {(DateTime.UtcNow - start).TotalSeconds}" +
                        $" | {totalArea}|{maxWidth}|{maxEdge}| {startArea}|{minWidth}x{minHeight}={area} | {maxWidthIncreasement}x{maxHeightDecreasement}\n");
                }
            });

            var bestResult = bestResults.Min();
            width = bestResult.width;
            height = bestResult.height;
            File.AppendAllTextAsync("RectTime.log", $"{(DateTime.UtcNow - packStart).TotalSeconds}\n");
            return bestResult.list;
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
