using System;
using System.Collections.Generic;
using System.Text;

namespace TexturePacker.Net.Packager
{
    public class Size
    {
        public int Width { get; }

        public int Height { get; }

        public Size(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }
}
