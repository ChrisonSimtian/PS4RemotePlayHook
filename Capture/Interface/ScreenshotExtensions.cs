using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Capture.Interface
{
    public static class ScreenshotExtensions
    {
        public static Bitmap ToBitmap(this byte[] data, int width, int height, int stride, System.Drawing.Imaging.PixelFormat pixelFormat)
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                var img = new Bitmap(width, height, stride, pixelFormat, handle.AddrOfPinnedObject());

                return img;
            }
            finally
            {
                if (handle.IsAllocated)
                    handle.Free();
            }
        }

        public static Bitmap ToJPGBitmap(this byte[] data, int width, int height, int stride, System.Drawing.Imaging.PixelFormat pixelFormat)
        {
            var img = data.ToBitmap(width, height, stride, pixelFormat);
            byte[]  byteArray = img.ToByteArray(System.Drawing.Imaging.ImageFormat.Jpeg);
            return byteArray.ToBitmap();
        }

        public static Bitmap ToBitmap(this byte[] imageBytes)
        {
            // Note: deliberately not disposing of MemoryStream, it doesn't have any unmanaged resources anyway and the GC 
            //       will deal with it. This fixes GitHub issue #19 (https://github.com/spazzarama/Direct3DHook/issues/19).
            MemoryStream ms = new MemoryStream(imageBytes);
            try
            {
                Bitmap image = (Bitmap)Image.FromStream(ms);
                return image;
            }
            catch
            {
                return null;
            }
        }

        public static byte[] ToByteCompessedArray(this Image img, System.Drawing.Imaging.ImageFormat format)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                // https://msdn.microsoft.com/de-de/library/bb882583(v=vs.110).aspx
                ImageCodecInfo jpgEncoder = GetEncoder(System.Drawing.Imaging.ImageFormat.Jpeg);
                System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
                EncoderParameters myEncoderParameters = new EncoderParameters(1);

                EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, 60L);
                myEncoderParameters.Param[0] = myEncoderParameter;
                img.Save(stream, jpgEncoder, myEncoderParameters);
                //img.Save(stream, format);
                stream.Close();
                return stream.ToArray();
            }
        }

        public static byte[] ToByteArray(this Image img, System.Drawing.Imaging.ImageFormat format)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                img.Save(stream, format);
                stream.Close();
                return stream.ToArray();
            }
        }

        private static ImageCodecInfo GetEncoder(System.Drawing.Imaging.ImageFormat format)
        {

            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();

            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        public static Bitmap ToJPGBitmap(this byte[] img, System.Drawing.Imaging.ImageFormat format)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                Image image = Image.FromStream(stream);
                return image.ToByteArray(format).ToBitmap();
            }
        }
    }
}
