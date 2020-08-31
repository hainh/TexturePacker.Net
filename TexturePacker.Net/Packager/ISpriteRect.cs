using System;
using System.Collections.Generic;
using System.Text;

namespace TexturePacker.Net.Packager
{
    public interface ISpriteRect
    {
        public int Width { get; }

        public int Height { get; }

        public bool Rotated { get; set; }

        public int X { get; set; }

        public int Y { get; set; }

        public int Sheet { get; set; }
    }
}
