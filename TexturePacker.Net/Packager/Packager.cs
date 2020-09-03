using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
            Bin[] bestResults = new Bin[(int)MaxRectsBinPack.FreeRectChoiceHeuristic.End];
            int totalArea = rects.Sum(r => r.Width * r.Height);
            int heuristic = (int)MaxRectsBinPack.FreeRectChoiceHeuristic.Start - 1;
            int maxSizeWidth = rects.Max(r => r.Width);
            int maxSizeHeight = rects.Max(r => r.Height);
            int maxIncreasement = Math.Max(1, (int)Math.Sqrt(totalArea) / 100);
            Parallel.For(0, Environment.ProcessorCount, _ =>
            {
                var h = Interlocked.Increment(ref heuristic);
                if (h >= (int)MaxRectsBinPack.FreeRectChoiceHeuristic.End)
                {
                    return;
                }
                var method = (MaxRectsBinPack.FreeRectChoiceHeuristic)h;
                int area = (int)(totalArea * 1.01);
                int width = (int)Math.Sqrt(area) / 2;
                int height = area / width + 1;
                var bin = MaxRectsBinPacker.Value;
                List<Rect> feasibleResult = null;
                while (true)
                {
                    var rectsClone = new List<Rect>(rects);
                    bin.Init(width, height, true);
                    feasibleResult = bin.Insert(rectsClone, method);
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
                int maxWidth = Math.Min(area, width * 2);
                width = Math.Max(maxSizeWidth, width / 2);
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
                                int widthDecreasement = maxIncreasement / 2;
                                int lastWidth = width;
                                while (true)
                                {
                                    width -= widthDecreasement;
                                    rectsClone = new List<Rect>(rects);
                                    bin.Init(width, height, true);
                                    feasibleResult = bin.Insert(rectsClone, method);
                                    if (rectsClone.Count == 0)
                                    {
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
                                        // Go back and decrease the decreasement
                                        width += widthDecreasement;
                                        widthDecreasement /= 2;
                                    }
                                    else
                                    {
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

                bestResults[h] = new Bin()
                {
                    width = minWidth,
                    height = minHeight,
                    list = minRects
                };
            });

            var bestResult = bestResults.Min();
            width = bestResult.width;
            height = bestResult.height;
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
                return int.MinValue;
            }
            if (other.list == null)
            {
                return int.MaxValue;
            }
            return width * height - other.width * other.height;
        }
    }
}
