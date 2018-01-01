using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Capture.Interface
{
    [Serializable]   
    public class ResolutionChangeEventArgs: MarshalByRefObject
    {
        public int Width { get; set; }
        public int Height { get; set; }

        public ResolutionChangeEventArgs(int  width, int height)
        {
            Width = width;
            Height = height;
        }

        public override string ToString()
        {
            return String.Format("Resultion is: {0} : {1}", Width, Height);
        }
    }
}