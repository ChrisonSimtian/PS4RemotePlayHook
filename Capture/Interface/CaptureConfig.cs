using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;

namespace Capture.Interface
{
    [Serializable]
    public class CaptureConfig
    {
        public Direct3DVersion Direct3DVersion { get; set; }

        private int captureWidth;

        /// <summary>
        /// The capture width.
        /// Min size is 1 and max size is 1280 (HD)
        /// </summary>
        public int CaptureWidth
        {
            get { return captureWidth; }
            set
            {
                if (value > 0 && value <= 1280)
                {
                    captureWidth = value;
                }
            }
        }

        private int captureHeight;

        /// <summary>
        /// The capture height
        /// Min size is 1 and max size is 720 (HD)
        /// </summary>
        public int CaptureHeight
        {
            get { return captureHeight; }
            set
            {
                if (value > 0 && value <= 720)
                {
                    captureHeight = value;
                }
            }
        }

        public CaptureConfig()
        {
            Direct3DVersion = Direct3DVersion.AutoDetect;
            CaptureWidth = 720;
            CaptureHeight = 540;
        }
    }
}
