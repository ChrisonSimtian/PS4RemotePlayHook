using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;

namespace Capture
{
    public struct MetaDataStruct
    {
        public int length;

        public int width;

        public int height;

        public int pitch;

        public PixelFormat format;

        public MetaDataStruct(int length, int width, int height, int pitch, PixelFormat format)
        {
            this.length = length;
            this.width = width;
            this.height = height;
            this.pitch = pitch;
            this.format = format;
        }
    }
}
