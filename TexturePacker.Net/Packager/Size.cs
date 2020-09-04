using System;
using System.Collections.Generic;
using System.Text;

namespace TexturePacker.Net.Packager
{
    public class Size
    {
        private bool rotated = false;

        public int Width;

        public int Height;

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
                }
            }
        }

        public Size(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }
}
