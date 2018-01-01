using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using EasyHook;
using System.Runtime.Remoting;
using System.Drawing.Imaging;
using Capture.Interface;
using SharedMemory;
using System.Threading;
using System.Runtime.ExceptionServices;

namespace Capture.Hook
{
    internal abstract class BaseDXHook : SharpDX.Toolkit.Component, IDXHook
    {
        protected readonly ClientCaptureInterfaceEventProxy InterfaceEventProxy = new ClientCaptureInterfaceEventProxy();

        protected volatile int captureWidth;

        protected volatile int captureHeight;

        private readonly BufferReadWrite producer;

        public BaseDXHook(CaptureInterface ssInterface, int captureWidth, int captureHeight)
        {
            this.Interface = ssInterface;
            this.producer = new BufferReadWrite(name: "MySharedMemory");
            this.captureWidth = captureWidth;
            this.captureHeight = captureHeight;
            Interface.ResolutionChanged += InterfaceEventProxy.ResolutionChangeHandler;
            InterfaceEventProxy.ResolutionChange += new ResolutionChangeEvent(InterfaceEventProxy_ResolutionChange);
        }

        ~BaseDXHook()
        {
            Dispose(false);
        }

        void InterfaceEventProxy_ResolutionChange(ResolutionChangeEventArgs args)
        {
            new Thread(() =>
            {
                this.captureWidth = args.Width;
                this.captureHeight = args.Height;
            }).Start();

        }

        int _processId = 0;
        protected int ProcessId
        {
            get
            {
                if (_processId == 0)
                {
                    _processId = RemoteHooking.GetCurrentProcessId();
                }
                return _processId;
            }
        }

        protected virtual string HookName
        {
            get
            {
                return "BaseDXHook";
            }
        }

        protected void DebugMessage(string message)
        {
#if DEBUG
            try
            {
                Interface.Message(MessageType.Debug, HookName + ": " + message);
            }
            catch (RemotingException)
            {
                // Ignore remoting exceptions
            }
#endif
        }

        protected IntPtr[] GetVTblAddresses(IntPtr pointer, int numberOfMethods)
        {
            return GetVTblAddresses(pointer, 0, numberOfMethods);
        }

        protected IntPtr[] GetVTblAddresses(IntPtr pointer, int startIndex, int numberOfMethods)
        {
            List<IntPtr> vtblAddresses = new List<IntPtr>();
            IntPtr vTable = Marshal.ReadIntPtr(pointer);
            for (int i = startIndex; i < startIndex + numberOfMethods; i++)
            {
                vtblAddresses.Add(Marshal.ReadIntPtr(vTable, i * IntPtr.Size)); // using IntPtr.Size allows us to support both 32 and 64-bit processes
            }

            return vtblAddresses.ToArray();
        }

        /// <summary>
        /// Process the frame based on the.
        /// </summary>
        /// <param name="width">image width</param>
        /// <param name="height">image height</param>
        /// <param name="pitch">data pitch (bytes per row)</param>
        /// <param name="format">target format</param>
        /// <param name="pBits">IntPtr to the image data</param>
        [HandleProcessCorruptedStateExceptions]
        protected void ProcessFrame(int width, int height, int pitch, PixelFormat format, IntPtr pBits)
        {

            if (format == PixelFormat.Undefined)
            {
                DebugMessage("Unsupported render target format");
                return;
            }

            if (pBits == IntPtr.Zero)
            {
                DebugMessage("No image data");
                return;
            }

            // Copy the image data from the buffer
            int size = height * pitch;
            var data = new byte[size];

            try
            {
                Marshal.Copy(pBits, data, 0, size);
                if (producer.AcquireWriteLock(1)) // skip frame if we cannot aquire lock immediately
                {
                    MetaDataStruct metaData = new MetaDataStruct(data.Length, width, height, pitch, format);
                    producer.Write(ref metaData);
                    producer.Write(data, FastStructure<MetaDataStruct>.Size);
                    producer.ReleaseWriteLock();
                }
            }
            catch (TimeoutException)
            {
                // If we could not acquire write lock skip frame
            }
            catch (AccessViolationException)
            {
                // In this specifc case we are ignoring CSE (corrupted state exception)
                // It could happen during Window resizing.
                // If someone knows are better way please feel free to contribute
                // Because there is a timout in the resizing hook this exception should never be thrown
            }
            finally
            {
                producer.ReleaseWriteLock();
            }

        }

        #region IDXHook Members

        public CaptureInterface Interface
        {
            get;
            set;
        }

        private CaptureConfig _config;
        public CaptureConfig Config
        {
            get { return _config; }
            set
            {
                _config = value;
            }
        }

        protected List<Hook> Hooks = new List<Hook>();
        public abstract void Hook();

        public abstract void Cleanup();

        #endregion

        #region IDispose Implementation

        protected override void Dispose(bool disposeManagedResources)
        {
            // Only clean up managed objects if disposing (i.e. not called from destructor)
            if (disposeManagedResources)
            {
                try
                {
                    Cleanup();
                }
                catch { }

                try
                {
                    // Uninstall Hooks
                    if (Hooks.Count > 0)
                    {
                        // First disable the hook (by excluding all threads) and wait long enough to ensure that all hooks are not active
                        foreach (var hook in Hooks)
                        {
                            // Lets ensure that no threads will be intercepted again
                            hook.Deactivate();
                        }

                        System.Threading.Thread.Sleep(100);

                        // Now we can dispose of the hooks (which triggers the removal of the hook)
                        foreach (var hook in Hooks)
                        {
                            hook.Dispose();
                        }

                        Hooks.Clear();
                    }
                }
                catch { }
            }
            if (this.producer != null)
            {
                this.producer.Dispose();
            }
            base.Dispose(disposeManagedResources);
        }
        #endregion
    }
}