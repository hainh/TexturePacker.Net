﻿using System;
using System.Collections.Generic;
using System.Text;

namespace TexturePacker.Net.Packager
{
    public interface IPackager
    {
        int Pack(IEnumerable<SpriteRect> rects, int width, int height);
    }
}
