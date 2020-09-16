using System;
using System.Collections.Generic;
using System.Text;

namespace TexturePacker.Net.Packager
{
    public class Rect// : Size
    {
        public int Width;

        public int Height;

        public int X;

        public int Y;

        public int FarX;

        public int FarY;

        public int SheetIndex { get; set; }

        public object Item { get; set; }

        private bool rotated = false;

        public bool Rotated
        {
            get => rotated;
            set
            {
                if (rotated != value)
                {
                    rotated = value;
                    int temp = Width;
                    Width = Height;
                    Height = temp;
                    FarX = X + Width;
                    FarY = Y + Height;
                }
            }
        }

        public Rect(int x, int y, int width, int height)// : base(width, height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            FarX = x + width;
            FarY = y + height;
        }

        public static readonly Rect Empty = new Rect(0, 0, 0, 0);

        /// <summary>
        /// Is this <see cref="Rect"/> contains <paramref name="rect"/>
        /// </summary>
        /// <param name="rect">A Rect to check</param>
        /// <returns>true if <paramref name="rect"/> is completely inside this <see cref="Rect"/></returns>
        public bool Contains(Rect rect)
        {
            return rect.X >= this.X && rect.Y >= this.Y
                && rect.FarX <= this.FarX
                && rect.FarY <= this.FarY;
        }
    }
}
