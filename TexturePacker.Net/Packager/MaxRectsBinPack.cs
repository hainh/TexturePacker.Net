using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TexturePacker.Net.Packager
{
    public class MaxRectsBinPack
    {
		private int binWidth;
		private int binHeight;

		private bool binAllowFlip;

        private readonly List<Rect> usedRectangles;
		private readonly List<Rect> freeRectangles;

		public MaxRectsBinPack()
		{
            usedRectangles = new List<Rect>(1000);
			freeRectangles = new List<Rect>(20000);
		}

		public MaxRectsBinPack(int width, int height, bool allowFlip = true) : this()
		{
			Init(width, height, allowFlip);
        }

		public void Init(int width, int height, bool allowFlip = true)
        {
			binAllowFlip = allowFlip;
			binWidth = width;
			binHeight = height;

            usedRectangles.Clear();
            freeRectangles.Clear();

            Rect initialRect = new Rect(width, height);
			freeRectangles.Add(initialRect);
        }

		/// Inserts a single rectangle into the bin, possibly rotated.
		public Rect Insert(int width, int height, FreeRectChoiceHeuristic method)
		{
			// Unused in this function. We don't need to know the score after finding the position.
			Rect newNode = method switch
			{
				FreeRectChoiceHeuristic.RectBestShortSideFit => FindPositionForNewNodeBestShortSideFit(width, height, out _, out _),
				FreeRectChoiceHeuristic.RectBottomLeftRule => FindPositionForNewNodeBottomLeft(width, height, out _, out _),
				FreeRectChoiceHeuristic.RectContactPointRule => FindPositionForNewNodeContactPoint(width, height, out _),
				FreeRectChoiceHeuristic.RectBestLongSideFit => FindPositionForNewNodeBestLongSideFit(width, height, out _, out _),
				FreeRectChoiceHeuristic.RectBestAreaFit => FindPositionForNewNodeBestAreaFit(width, height, out _, out _),
				_ => throw new NotImplementedException()
			};

			if (newNode.Height == 0)
				return newNode;

			int numRectanglesToProcess = freeRectangles.Count;
			for (int i = 0; i < numRectanglesToProcess; ++i)
			{
				if (SplitFreeNode(freeRectangles[i], newNode))
				{
					freeRectangles.RemoveAt(i);
					--i;
					--numRectanglesToProcess;
				}
			}

			PruneFreeList();

			usedRectangles.Add(newNode);
			return newNode;
		}

		/// <summary>
		/// Inserts the given list of rectangles in an offline/batch mode, possibly rotated.
		/// </summary>
		/// <param name="rects">The list of rectangles to insert. This vector will be destroyed in the process.</param>
		/// <param name="method">The rectangle placement rule to use when packing.</param>
		/// <returns>A List contains the packed rectangles. The indices will not correspond to that of rects.</returns>
		public List<Rect> Insert(List<Rect> rects, FreeRectChoiceHeuristic method)
		{
			int rectCount = rects.Count;
			List<Rect> result = new List<Rect>(rectCount);

			while (rectCount > 0)
			{
				int bestScore1 = int.MaxValue;
				int bestScore2 = int.MaxValue;
				int bestRectIndex = -1;
				Rect bestNode = null;

				for (int i = 0; i < rectCount; ++i)
				{
                    Rect newNode = ScoreRect(rects[i].Width, rects[i].Height, method, out int score1, out int score2);

                    if (score1 < bestScore1 || (score1 == bestScore1 && score2 < bestScore2))
					{
						bestScore1 = score1;
						bestScore2 = score2;
						bestNode = newNode;
						bestRectIndex = i;
					}
				}

				if (bestRectIndex == -1)
					return result;

				bestNode.Item = rects[bestRectIndex].Item;
				PlaceRect(bestNode);
				result.Add(bestNode);
				rects.RemoveAt(bestRectIndex);
				rectCount--;
			}

			return result;
		}

		/// Places the given rectangle into the bin.
		private void PlaceRect(Rect node)
		{
			int numRectanglesToProcess = freeRectangles.Count;
			for (int i = 0; i < numRectanglesToProcess; ++i)
			{
				if (SplitFreeNode(freeRectangles[i], node))
				{
					freeRectangles.RemoveAt(i);
					--i;
					--numRectanglesToProcess;
				}
			}

			PruneFreeList();

			usedRectangles.Add(node);
		}

		/// <summary>
		/// Computes the placement score for placing the given rectangle with the given method.
		/// </summary>
		/// <param name="score1">[out] The primary placement score will be outputted here.</param>
		/// <param name="score2">[out] The secondary placement score will be outputted here. This isu sed to break ties.</param>
		/// <returns>This struct identifies where the rectangle would be placed if it were placed.</returns>
		private Rect ScoreRect(int width, int height, FreeRectChoiceHeuristic method, out int score1, out int score2)
		{
			score1 = int.MaxValue;
			score2 = int.MaxValue;
			Rect newNode = method switch
			{
				FreeRectChoiceHeuristic.RectBestShortSideFit => FindPositionForNewNodeBestShortSideFit(width, height, out score1, out score2),
				FreeRectChoiceHeuristic.RectBottomLeftRule => FindPositionForNewNodeBottomLeft(width, height, out score1, out score2),
				FreeRectChoiceHeuristic.RectContactPointRule => FindPositionForNewNodeContactPoint(width, height, out score1),
				FreeRectChoiceHeuristic.RectBestLongSideFit => FindPositionForNewNodeBestLongSideFit(width, height, out score2, out score1),
				FreeRectChoiceHeuristic.RectBestAreaFit => FindPositionForNewNodeBestAreaFit(width, height, out score1, out score2),
				_ => throw new NotImplementedException(),
			};
			if (method == FreeRectChoiceHeuristic.RectContactPointRule)
			{
				score1 = -score1; // Reverse since we are minimizing, but for contact point score bigger is better.
			}

			// Cannot fit the current rectangle.
			if (newNode.Height == 0)
			{
				score1 = int.MaxValue;
				score2 = int.MaxValue;
			}

			return newNode ?? new Rect(0, 0);
		}

		/// Computes the ratio of used surface area to the total bin area.
		public float Occupancy()
        {
			ulong usedSurfaceArea = 0;
			for (int i = 0; i < usedRectangles.Count; ++i)
				usedSurfaceArea += (ulong)(usedRectangles[i].Width * usedRectangles[i].Height);

			return (float)usedSurfaceArea / (binWidth * binHeight);
		}
		
		private Rect FindPositionForNewNodeBottomLeft(int width, int height, out int bestY, out int bestX)
		{
			Rect bestNode = null;

			bestY = int.MaxValue;
			bestX = int.MaxValue;

			for (int i = 0; i < freeRectangles.Count; ++i)
			{
				// Try to place the rectangle in upright (non-flipped) orientation.
				if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
				{
					int topSideY = freeRectangles[i].Y + height;
					if (topSideY < bestY || (topSideY == bestY && freeRectangles[i].X < bestX))
					{
						bestNode = new Rect(width, height)
						{
							X = freeRectangles[i].X,
							Y = freeRectangles[i].Y
						};
						bestY = topSideY;
						bestX = freeRectangles[i].X;
					}
				}
				if (binAllowFlip && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
				{
					int topSideY = freeRectangles[i].Y + width;
					if (topSideY < bestY || (topSideY == bestY && freeRectangles[i].X < bestX))
					{
						bestNode = new Rect(width, height)
						{
							X = freeRectangles[i].X,
							Y = freeRectangles[i].Y
						};
						bestNode.Rotated = true;
						bestY = topSideY;
						bestX = freeRectangles[i].X;
					}
				}
			}
			return bestNode ?? new Rect(0, 0);
		}

		private Rect FindPositionForNewNodeBestShortSideFit(int width, int height, out int bestShortSideFit, out int bestLongSideFit)
		{
			Rect bestNode = null;

			bestShortSideFit = int.MaxValue;
			bestLongSideFit = int.MaxValue;

			for (int i = 0; i < freeRectangles.Count; ++i)
			{
				// Try to place the rectangle in upright (non-flipped) orientation.
				if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
				{
					int leftoverHoriz = Math.Abs(freeRectangles[i].Width - width);
					int leftoverVert = Math.Abs(freeRectangles[i].Height - height);
					int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
					int longSideFit = Math.Max(leftoverHoriz, leftoverVert);

					if (shortSideFit < bestShortSideFit || (shortSideFit == bestShortSideFit && longSideFit < bestLongSideFit))
					{
						bestNode = new Rect(width, height)
						{
							X = freeRectangles[i].X,
							Y = freeRectangles[i].Y
						};
						bestShortSideFit = shortSideFit;
						bestLongSideFit = longSideFit;
					}
				}

				if (binAllowFlip && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
				{
					int flippedLeftoverHoriz = Math.Abs(freeRectangles[i].Width - height);
					int flippedLeftoverVert = Math.Abs(freeRectangles[i].Height - width);
					int flippedShortSideFit = Math.Min(flippedLeftoverHoriz, flippedLeftoverVert);
					int flippedLongSideFit = Math.Max(flippedLeftoverHoriz, flippedLeftoverVert);

					if (flippedShortSideFit < bestShortSideFit || (flippedShortSideFit == bestShortSideFit && flippedLongSideFit < bestLongSideFit))
					{
						bestNode = new Rect(width, height)
						{
							X = freeRectangles[i].X,
							Y = freeRectangles[i].Y
						};
						bestNode.Rotated = true;
						bestShortSideFit = flippedShortSideFit;
						bestLongSideFit = flippedLongSideFit;
					}
				}
			}
			return bestNode ?? new Rect(0, 0);
		}

		/// Returns 0 if the two intervals i1 and i2 are disjoint, or the length of their overlap otherwise.
		private Rect FindPositionForNewNodeBestLongSideFit(int width, int height, out int bestShortSideFit, out int bestLongSideFit)
        {
			Rect bestNode = null;

			bestShortSideFit = int.MaxValue;
			bestLongSideFit = int.MaxValue;

			for (int i = 0; i < freeRectangles.Count; ++i)
			{
				// Try to place the rectangle in upright (non-flipped) orientation.
				if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
				{
					int leftoverHoriz = Math.Abs(freeRectangles[i].Width - width);
					int leftoverVert = Math.Abs(freeRectangles[i].Height - height);
					int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
					int longSideFit = Math.Max(leftoverHoriz, leftoverVert);

					if (longSideFit < bestLongSideFit || (longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit))
					{
						bestNode = new Rect(width, height)
						{
							X = freeRectangles[i].X,
							Y = freeRectangles[i].Y
						};
						bestShortSideFit = shortSideFit;
						bestLongSideFit = longSideFit;
					}
				}

				if (binAllowFlip && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
				{
					int leftoverHoriz = Math.Abs(freeRectangles[i].Width - height);
					int leftoverVert = Math.Abs(freeRectangles[i].Height - width);
					int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);
					int longSideFit = Math.Max(leftoverHoriz, leftoverVert);

					if (longSideFit < bestLongSideFit || (longSideFit == bestLongSideFit && shortSideFit < bestShortSideFit))
					{
						bestNode = new Rect(width, height)
						{
							X = freeRectangles[i].X,
							Y = freeRectangles[i].Y
						};
						bestNode.Rotated = true;
						bestShortSideFit = shortSideFit;
						bestLongSideFit = longSideFit;
					}
				}
			}
			return bestNode ?? new Rect(0, 0);
		}

		private Rect FindPositionForNewNodeBestAreaFit(int width, int height, out int bestAreaFit, out int bestShortSideFit)
		{
			Rect bestNode = null;

			bestAreaFit = int.MaxValue;
			bestShortSideFit = int.MaxValue;

			for (int i = 0; i < freeRectangles.Count; ++i)
			{
				int areaFit = freeRectangles[i].Width * freeRectangles[i].Height - width * height;

				// Try to place the rectangle in upright (non-flipped) orientation.
				if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
				{
					int leftoverHoriz = Math.Abs(freeRectangles[i].Width - width);
					int leftoverVert = Math.Abs(freeRectangles[i].Height - height);
					int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);

					if (areaFit < bestAreaFit || (areaFit == bestAreaFit && shortSideFit < bestShortSideFit))
					{
						bestNode = new Rect(width, height)
						{
							X = freeRectangles[i].X,
							Y = freeRectangles[i].Y
						};
						bestShortSideFit = shortSideFit;
						bestAreaFit = areaFit;
					}
				}

				if (binAllowFlip && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
				{
					int leftoverHoriz = Math.Abs(freeRectangles[i].Width - height);
					int leftoverVert = Math.Abs(freeRectangles[i].Height - width);
					int shortSideFit = Math.Min(leftoverHoriz, leftoverVert);

					if (areaFit < bestAreaFit || (areaFit == bestAreaFit && shortSideFit < bestShortSideFit))
					{
						bestNode = new Rect(width, height)
						{
							X = freeRectangles[i].X,
							Y = freeRectangles[i].Y
						};
						bestNode.Rotated = true;
						bestShortSideFit = shortSideFit;
						bestAreaFit = areaFit;
					}
				}
			}
			return bestNode ?? new Rect(0, 0);
		}

		static int CommonIntervalLength(int i1start, int i1end, int i2start, int i2end)
		{
			if (i1end < i2start || i2end < i1start)
				return 0;
			return Math.Min(i1end, i2end) - Math.Max(i1start, i2start);
		}

		/// Computes the placement score for the -CP variant.
		private int ContactPointScoreNode(int x, int y, int width, int height)
		{
			int score = 0;

			if (x == 0 || x + width == binWidth)
				score += height;
			if (y == 0 || y + height == binHeight)
				score += width;

			for (int i = 0; i < usedRectangles.Count; ++i)
			{
				if (usedRectangles[i].X == x + width || usedRectangles[i].X + usedRectangles[i].Width == x)
					score += CommonIntervalLength(usedRectangles[i].Y, usedRectangles[i].Y + usedRectangles[i].Height, y, y + height);
				if (usedRectangles[i].Y == y + height || usedRectangles[i].Y + usedRectangles[i].Height == y)
					score += CommonIntervalLength(usedRectangles[i].X, usedRectangles[i].X + usedRectangles[i].Width, x, x + width);
			}
			return score;
		}

		private Rect FindPositionForNewNodeContactPoint(int width, int height, out int bestContactScore)
        {
			Rect bestNode = null;
			bestContactScore = -1;

			for (int i = 0; i < freeRectangles.Count; ++i)
			{
				// Try to place the rectangle in upright (non-flipped) orientation.
				if (freeRectangles[i].Width >= width && freeRectangles[i].Height >= height)
				{
					int score = ContactPointScoreNode(freeRectangles[i].X, freeRectangles[i].Y, width, height);
					if (score > bestContactScore)
					{
						bestNode = new Rect(width, height)
						{
							X = freeRectangles[i].X,
							Y = freeRectangles[i].Y
						};
						bestContactScore = score;
					}
				}
				if (binAllowFlip && freeRectangles[i].Width >= height && freeRectangles[i].Height >= width)
				{
					int score = ContactPointScoreNode(freeRectangles[i].X, freeRectangles[i].Y, height, width);
					if (score > bestContactScore)
					{
						bestNode = new Rect(width, height)
						{
							X = freeRectangles[i].X,
							Y = freeRectangles[i].Y
						};
						bestNode.Rotated = true;
						bestContactScore = score;
					}
				}
			}

			return bestNode ?? new Rect(0, 0);
		}

		/// @return True if the free node was split.
		private bool SplitFreeNode(Rect freeNode, Rect usedNode)
		{
			// Test with SAT if the rectangles even intersect.
			if (usedNode.X >= freeNode.X + freeNode.Width || usedNode.X + usedNode.Width <= freeNode.X ||
				usedNode.Y >= freeNode.Y + freeNode.Height || usedNode.Y + usedNode.Height <= freeNode.Y)
				return false;

			if (usedNode.X < freeNode.X + freeNode.Width && usedNode.X + usedNode.Width > freeNode.X)
			{
				// New node at the top side of the used node.
				if (usedNode.Y > freeNode.Y && usedNode.Y < freeNode.Y + freeNode.Height)
				{
					Rect newNode = new Rect(freeNode.Width, usedNode.Y - freeNode.Y)
					{
						X = freeNode.X,
						Y = freeNode.Y
					};
					freeRectangles.Add(newNode);
				}

				// New node at the bottom side of the used node.
				if (usedNode.Y + usedNode.Height < freeNode.Y + freeNode.Height)
				{
					Rect newNode = new Rect(freeNode.Width, freeNode.Y + freeNode.Height - (usedNode.Y + usedNode.Height))
					{
						X = freeNode.X,
						Y = usedNode.Y + usedNode.Height
					};
					freeRectangles.Add(newNode);
				}
			}

			if (usedNode.Y < freeNode.Y + freeNode.Height && usedNode.Y + usedNode.Height > freeNode.Y)
			{
				// New node at the left side of the used node.
				if (usedNode.X > freeNode.X && usedNode.X < freeNode.X + freeNode.Width)
				{
					Rect newNode = new Rect(usedNode.X - freeNode.X, freeNode.Height)
					{
						X = freeNode.X,
						Y = freeNode.Y
					};
					freeRectangles.Add(newNode);
				}

				// New node at the right side of the used node.
				if (usedNode.X + usedNode.Width < freeNode.X + freeNode.Width)
				{
					Rect newNode = new Rect(freeNode.X + freeNode.Width - (usedNode.X + usedNode.Width), freeNode.Height)
					{
						X = usedNode.X + usedNode.Width,
						Y = freeNode.Y
					};
					newNode.X = usedNode.X + usedNode.Width;
					freeRectangles.Add(newNode);
				}
			}

			return true;
		}

		/// <summary>
		/// Goes through the free rectangle list and removes any redundant entries.
		/// </summary>
		private void PruneFreeList()
		{

			/* 
			///  Would be nice to do something like this, to avoid a Theta(n^2) loop through each pair.
			///  But unfortunately it doesn't quite cut it, since we also want to detect containment. 
			///  Perhaps there's another way to do this faster than Theta(n^2).

			if (freeRectangles.size() > 0)
				clb::sort::QuickSort(&freeRectangles[0], freeRectangles.size(), NodeSortCmp);

			for(size_t i = 0; i < freeRectangles.size()-1; ++i)
				if (freeRectangles[i].x == freeRectangles[i+1].x &&
					freeRectangles[i].y == freeRectangles[i+1].y &&
					freeRectangles[i].width == freeRectangles[i+1].width &&
					freeRectangles[i].height == freeRectangles[i+1].height)
				{
					freeRectangles.erase(freeRectangles.begin() + i);
					--i;
				}
			*/

			/// Go through each pair and remove any rectangle that is redundant.
			for (int i = 0; i < freeRectangles.Count; ++i)
			{
				for (int j = i + 1; j < freeRectangles.Count; ++j)
				{
					if (freeRectangles[j].Contains(freeRectangles[i]))
					{
						freeRectangles.RemoveAt(i);
						--i;
						break;
					}
					if (freeRectangles[i].Contains(freeRectangles[j]))
					{
						freeRectangles.RemoveAt(j);
						--j;
					}
				}
			}
		}

		/// <summary>
		/// Specifies the different heuristic rules that can be used when deciding where to place a new rectangle.
		/// </summary>
		public enum FreeRectChoiceHeuristic
		{
			Start = 0,
			RectBestShortSideFit = Start, ///< -BSSF: Positions the rectangle against the short side of a free rectangle into which it fits the best.
			RectBestLongSideFit, ///< -BLSF: Positions the rectangle against the long side of a free rectangle into which it fits the best.
			RectBestAreaFit, ///< -BAF: Positions the rectangle into the smallest free rect into which it fits.
			RectBottomLeftRule, ///< -BL: Does the Tetris placement.
			RectContactPointRule, ///< -CP: Choosest the placement where the rectangle touches other rects as much as possible.
            End
        };
	}
}
