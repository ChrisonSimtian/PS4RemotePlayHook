using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using EasyHook;
using System.IO;
using Capture.Interface;
using Capture.Hook;
using Capture;
using SharedMemory;
using TestScreenshot.Window;

namespace TestScreenshot
{
    public partial class Form1 : Form
    {
        WindowControl windowControl;

        private static readonly int MAX_SLEEP_TIME = 16;

        public Form1()
        {
            InitializeComponent();
            MainLoop();
        }

        private void Form1_Closing(object sender, CancelEventArgs e)
        {
        }


        #region image capture loop

        public void MainLoop()
        {
            Thread workerThread = new Thread(() =>
            {
                this.SimpleImageCaptureLoop();
            });
            workerThread.IsBackground = true;
            workerThread.Start();
        }


        public virtual void SimpleImageCaptureLoop()
        {
            using (var consumer = new CircularBuffer(name: "MySharedMemory", nodeCount: 4, nodeBufferSize: ((8294400 + FastStructure<MetaDataStruct>.Size)) * 2)) // Should be large enough to store 2 full hd raw image data + 4 Size of Struct
            {
                Stopwatch stopwatch = new Stopwatch();
                do
                {
                    stopwatch.Reset();
                    stopwatch.Start();
                    try
                    {
                        consumer.Read(intPtr =>
                        {
                            MetaDataStruct metaData = FastStructure.PtrToStructure<MetaDataStruct>(intPtr);
                            if (metaData.length > 0)
                            {
                                byte[] byteArray = new byte[metaData.length];
                                FastStructure.ReadArray<byte>(byteArray, intPtr, 0, byteArray.Length);

                                using (var bm = byteArray.ToBitmap(metaData.width, metaData.height, metaData.pitch, metaData.format))
                                {
                                    byte[] compressedJpgByteArray = bm.ToByteCompessedArray();
                                    Bitmap bitmap = compressedJpgByteArray.ToBitmap();

                                    pictureBox1.Invoke(new MethodInvoker(delegate ()
                                    {
                                        if (pictureBox1.Image != null)
                                        {
                                            pictureBox1.Image.Dispose();
                                        }
                                        pictureBox1.Image = bitmap;
                                    }));
                                }
                            }
                            return 0;
                        }, timeout: MAX_SLEEP_TIME);
                    }
                    catch (TimeoutException exception)
                    {
                        Console.WriteLine(exception);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }

                    int timeout = (int)(MAX_SLEEP_TIME - stopwatch.ElapsedMilliseconds);
                    Thread.Sleep(timeout >= 0 ? timeout : 0);
                } while (true);
            }
        }
        #endregion

        private void btnInject_Click(object sender, EventArgs e)
        {
            if (_captureProcess == null)
            {
                btnInject.Enabled = false;
                AttachProcess();
            }
            else
            {
                HookManager.RemoveHookedProcess(_captureProcess.Process.Id);
                _captureProcess.CaptureInterface.Disconnect();
                _captureProcess = null;
            }

            if (_captureProcess != null)
            {
                btnInject.Text = "Detach";
                btnInject.Enabled = true;
            }
            else
            {
                btnInject.Text = "Inject";
                btnInject.Enabled = true;
            }
        }


        private void btnChangeRes_Click(object sender, EventArgs e)
        {
            if (_captureProcess != null)
            {
                _captureProcess.CaptureInterface.ChangeResolution((int)numUpDownWidth.Value, (int)numUpDownHeight.Value);
                
            }
            if (windowControl != null)
            {
                Size size = windowControl.GetWindowSize();
                if (size.Width < (int)numUpDownWidth.Value || size.Height < (int)numUpDownHeight.Value)
                {
                    windowControl.ResizeWindow(new Size((int)numUpDownWidth.Value, (int)numUpDownHeight.Value), false);
                }
            }

        }

        int processId = 0;
        Process _process;
        CaptureProcess _captureProcess;
        private void AttachProcess()
        {
            string exeName = Path.GetFileNameWithoutExtension(textBox1.Text);

            Process[] processes = Process.GetProcessesByName(exeName);
            foreach (Process process in processes)
            {
                // Simply attach to the first one found.

                // If the process doesn't have a mainwindowhandle yet, skip it (we need to be able to get the hwnd to set foreground etc)
                if (process.MainWindowHandle == IntPtr.Zero)
                {
                    continue;
                }

                // Skip if the process is already hooked (and we want to hook multiple applications)
                if (HookManager.IsHooked(process.Id))
                {
                    continue;
                }

                Direct3DVersion direct3DVersion = Direct3DVersion.Direct3D9;

                if (rbDirect3D9.Checked)
                {
                    direct3DVersion = Direct3DVersion.Direct3D9;
                }

                CaptureConfig captureConfig = new CaptureConfig()
                {
                    Direct3DVersion = direct3DVersion,
                    CaptureWidth = 720,
                    CaptureHeight = 540
                };

                processId = process.Id;
                _process = process;

                var captureInterface = new CaptureInterface();
                captureInterface.RemoteMessage += new MessageReceivedEvent(CaptureInterface_RemoteMessage);
                _captureProcess = new CaptureProcess(process, captureConfig, captureInterface);

                break;
            }
            Thread.Sleep(50);

            if (_captureProcess == null)
            {
                MessageBox.Show("No executable found matching: '" + exeName + "'");
            }
            else
            {
                IntPtr windowHandle = StreamingWindow.FindStreamingPanel(_captureProcess.Process);
                windowControl = new WindowControl(_captureProcess.Process.MainWindowHandle, windowHandle);

                Size size = windowControl.GetWindowSize();
                if (size.Width < (int)numUpDownWidth.Value || size.Height < (int)numUpDownHeight.Value)
                {
                    windowControl.ResizeWindow(new Size((int)numUpDownWidth.Value, (int)numUpDownHeight.Value), false);
                }
            }
        }

        /// <summary>
        /// Display messages from the target process
        /// </summary>
        /// <param name="message"></param>
        void CaptureInterface_RemoteMessage(MessageReceivedEventArgs message)
        {
            txtDebugLog.Invoke(new MethodInvoker(delegate ()
            {
                txtDebugLog.Text = String.Format("{0}\r\n{1}", message, txtDebugLog.Text);
            })
            );
        }
    }
}