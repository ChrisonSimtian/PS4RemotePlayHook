// PS4MacroAPI (File: Internal/WindowControl.cs)
//
// Copyright (c) 2017 Komefai
//
// Visit http://komefai.com for more information
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Diagnostics;
using System.Text;

namespace TestScreenshot.Window
{
    // Source from https://github.com/komefai/PS4Macro
    public class StreamingWindow
    {

        /// <summary>
        /// Find the streaming panel of PS4 Remote Play
        /// </summary>
        /// <param name="remotePlayProcess"></param>
        /// <returns></returns>
        public static IntPtr FindStreamingPanel(Process remotePlayProcess)
        {
            // Find panel in process
            var childHandles = WindowControl.GetAllChildHandles(remotePlayProcess.MainWindowHandle);
            var panelHandle = childHandles.Find(ptr =>
            {
                var sb = new StringBuilder(50);
                WindowControl.GetClassName(ptr, sb, 50);
                var str = sb.ToString();
                return str == "WindowsForms10.Window.8.app.0.141b42a_r9_ad1" || str == "WindowsForms10.Window.8.app.0.141b42a_r10_ad1";
            });

            // Try to find the best one possible instead
            if (panelHandle == IntPtr.Zero)
            {
                IntPtr biggestPanel = IntPtr.Zero;
                Rect biggestSize = new Rect();
                foreach (var ptr in childHandles)
                {
                    Rect rect = new Rect();
                    WindowControl.GetWindowRect(ptr, ref rect);

                    if (rect.Bottom - rect.Top >= biggestSize.Bottom - biggestSize.Top)
                    {
                        biggestPanel = ptr;
                        biggestSize = rect;
                    }
                }

                panelHandle = biggestPanel;
            }

            return panelHandle;
        }

    }
}
