using System;
using System.Collections.Generic;
using System.Text;

namespace TexturePacker.Net.Packager
{
    public interface IPackager
    {
        int Pack(IEnumerable<ISpriteRect> rects, int width, int height);
    }
}
