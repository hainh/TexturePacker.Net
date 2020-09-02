using System;
using System.Collections.Generic;
using System.Text;

namespace TexturePacker.Net.Packager
{
    public class Rect : Size
    {
        public int X { get; set; }

        public int Y { get; set; }

        public int SheetIndex { get; set; }

        public object Item { get; set; }

        public Rect(int width, int height) : base(width, height)
        {
        }

        /// <summary>
        /// Is this <see cref="Rect"/> contains <paramref name="rect"/>
        /// </summary>
        /// <param name="rect">A Rect to check</param>
        /// <returns>true if <paramref name="rect"/> is completely inside this <see cref="Rect"/></returns>
        public bool Contains(Rect rect)
        {
            return rect.X >= this.X && rect.Y >= this.Y
                && rect.X + rect.Width <= this.X + this.Width
                && rect.Y + rect.Height <= this.Y + this.Height;
        }
    }
}
