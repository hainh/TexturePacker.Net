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
            rects.Sort((a, b) => a.Width - b.Width);
            Bin[] bestResults = new Bin[(int)MaxRectsBinPack.FreeRectChoiceHeuristic.End];
            int totalArea = rects.Sum(r => r.Width * r.Height);
            int heuristic = (int)MaxRectsBinPack.FreeRectChoiceHeuristic.Start;
            int maxSize = Math.Max(rects.Max(r => r.Width), rects.Max(r => r.Height));
            int maxIncreasement = Math.Max(1, (int)Math.Sqrt(totalArea) / 1000);
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
                    int area = (int)(totalArea * 1.01);
                    int width = (int)Math.Sqrt(area) / 2;
                    int height = area / width + 1;
                    int rounds0 = 0, rounds1 = 0, rounds2 = 0, rounds3 = 0, rounds4 = 0, rounds5 = 0;
                    MaxRectsBinPack bin = MaxRectsBinPacker.Value;
                    List<Rect> feasibleResult = null;
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
                        width += Math.Max(width / 100, 1);
                    }
                    area = width * height;
                    List<Rect> minRects = feasibleResult;
                    int minWidth = width;
                    int minHeight = height;
                    // We found an area can fit all rects, now we try to narrow this area
                    int heightDecreasement = maxIncreasement;
                    int maxWidth = (int)Math.Sqrt(area);
                    width = Math.Max(maxSize, width / 2);
                    while (true)
                    {
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
                                    int widthDecreasement = Math.Max(1, maxIncreasement / 2);
                                    int lastWidth = width;
                                    while (true)
                                    {
                                        width -= widthDecreasement;
                                        rectsClone = new List<Rect>(rects);
                                        bin.Init(width, height, true);
                                        feasibleResult = bin.Insert(rectsClone, method);
                                        if (rectsClone.Count == 0)
                                        {
                                            rounds3++;
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
                                            rounds4++;
                                            // Go back and decrease the decreasement
                                            width += widthDecreasement;
                                            widthDecreasement /= 2;
                                        }
                                        else
                                        {
                                            rounds5++;
                                            break;
                                        }
                                    }
                                    width = lastWidth;
                                }

                                heightDecreasement = maxIncreasement;
                                break;
                            }
                        }
                        if (width == maxWidth)
                        {
                            break;
                        }
                        width = Math.Min(width + maxIncreasement, maxWidth);
                    }

                    Thread.CurrentThread.Priority = previousPriority;
                    bestResults[h] = new Bin()
                    {
                        width = minWidth,
                        height = minHeight,
                        list = minRects
                    };
                    File.AppendAllTextAsync($"{method}.log", $"{rounds0}, {rounds1}, {rounds2}, {rounds3}, {rounds4}, {rounds5} | {(float)totalArea / area:P} {(DateTime.UtcNow - start).TotalSeconds} | {start.TimeOfDay}\n");
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
