using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Capture.Interface;
using SharpDX.Direct3D9;
using System.Timers;

namespace Capture.Hook
{
    internal class DXHookD3D9 : BaseDXHook
    {
        Timer privateDataTimer;
        private static volatile bool IS_FREEING_PRIVATE_DATA = false;

        public DXHookD3D9(CaptureInterface ssInterface, int captureWidth, int captureHeight)
            : base(ssInterface, captureWidth, captureHeight)
        {
            privateDataTimer = new Timer();
            privateDataTimer.Elapsed += PrivateDataTimer_Elapsed;
            privateDataTimer.Interval = 1500;
        }

        private void PrivateDataTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            privateDataTimer.Stop();
            IS_FREEING_PRIVATE_DATA = false;
        }

        List<IntPtr> id3dSwapChainFunctionAddresses = new List<IntPtr>();
        List<IntPtr> id3dSurfaceFunctionAddresses = new List<IntPtr>();

        Hook<Direct3D9SwapChain_PresentDelegate> Direct3D9SwapChain_PresentHook = null;
        Hook<Direct3D9Surface_FreePrivateDataDelegate> Direct3D9Surface_FreePrivateDataHook = null;

        object _lockRenderTarget = new object();

        bool _resourcesInitialised;
        bool _renderTargetCopyLocked = false;
        Surface _renderTargetCopy;
        Surface _resolvedTarget;

        protected override string HookName
        {
            get
            {
                return "DXHookD3D9";
            }
        }

        const int D3D9_DEVICE_METHOD_COUNT = 119;
        const int D3D9Ex_DEVICE_METHOD_COUNT = 15;
        const int D3D9_SURFACE_METHOD_COUNT = 17;
        const int D3D9_SWAP_CHAIN_METHOD_COUNT = 10;

        public override void Hook()
        {
            this.DebugMessage("Hook: Begin");
            // First we need to determine the function address for IDirect3DDevice9
            Device device;
            id3dSwapChainFunctionAddresses = new List<IntPtr>();
            id3dSurfaceFunctionAddresses = new List<IntPtr>();
            this.DebugMessage("Hook: Before device creation");
            using (Direct3D d3d = new Direct3D())
            {
                using (var renderForm = new System.Windows.Forms.Form())
                {
                    using (device = new Device(d3d, 0, SharpDX.Direct3D9.DeviceType.Hardware, renderForm.Handle, CreateFlags.HardwareVertexProcessing, new SharpDX.Direct3D9.PresentParameters(1, 1)))
                    {
                        this.DebugMessage("Hook: Device created");

                        using (Surface surface = Surface.CreateOffscreenPlain(device, 1, 1, Format.A8R8G8B8, Pool.SystemMemory))
                        {
                            this.DebugMessage("Hook: Surface created");
                            id3dSurfaceFunctionAddresses.AddRange(GetVTblAddresses(surface.NativePointer, D3D9_SURFACE_METHOD_COUNT));
                        }

                        using (SwapChain swapChain = device.GetSwapChain(0))
                        {
                            this.DebugMessage("Hook: SwapChain created");
                            id3dSwapChainFunctionAddresses.AddRange(GetVTblAddresses(swapChain.NativePointer, D3D9_SWAP_CHAIN_METHOD_COUNT));
                        }
                    }
                }
            }

            Direct3D9Surface_FreePrivateDataHook = new Hook<Direct3D9Surface_FreePrivateDataDelegate>(
                id3dSurfaceFunctionAddresses[(int)Direct3DSurface9FunctionOrdinals.FreePrivateData],
                new Direct3D9Surface_FreePrivateDataDelegate(SurfaceFreePrivateDataHook),
                this);

            Direct3D9SwapChain_PresentHook = new Hook<Direct3D9SwapChain_PresentDelegate>(
                    id3dSwapChainFunctionAddresses[(int)DirectXSwapChainFunctionOrdinals.Present],
                    new Direct3D9SwapChain_PresentDelegate(SwapChainPresentHook),
                    this);


            /*
             * Don't forget that all hooks will start deactivated...
             * The following ensures that all threads are intercepted:
             * Note: you must do this for each hook.
             */

            Direct3D9Surface_FreePrivateDataHook.Activate();
            Hooks.Add(Direct3D9Surface_FreePrivateDataHook);

            Direct3D9SwapChain_PresentHook.Activate();
            Hooks.Add(Direct3D9SwapChain_PresentHook);

            this.DebugMessage("Hook: End");
        }

        /// <summary>
        /// Just ensures that the surface we created is cleaned up.
        /// </summary>
        public override void Cleanup()
        {
            lock (_lockRenderTarget)
            {
                _resourcesInitialised = false;

                RemoveAndDispose(ref _renderTargetCopy);
                _renderTargetCopyLocked = false;

                RemoveAndDispose(ref _resolvedTarget);
            }
        }

        /********************/
        /*** hook methods ***/
        /********************/

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        delegate int Direct3D9SwapChain_PresentDelegate(IntPtr swapChainPtr, ref SharpDX.Rectangle pSourceRect, ref SharpDX.Rectangle pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion, uint dwFlags);

        int SwapChainPresentHook(IntPtr swapChainPtr, ref SharpDX.Rectangle pSourceRect, ref SharpDX.Rectangle pDestRect, IntPtr hDestWindowOverride, IntPtr pDirtyRegion, uint dwFlags)
        {
            SwapChain swapChain = (SwapChain)swapChainPtr;

            if (!IS_FREEING_PRIVATE_DATA)
                DoCaptureRenderTarget(swapChain.GetBackBuffer(0).Device, swapChain.GetBackBuffer(0), "SwapChainPresentHook");

            return Direct3D9SwapChain_PresentHook.Original(swapChainPtr, ref pSourceRect, ref pDestRect, hDestWindowOverride, pDirtyRegion, dwFlags); ;
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        delegate int Direct3D9Surface_FreePrivateDataDelegate(IntPtr surfacePtr, ref Guid refguid);

        int SurfaceFreePrivateDataHook(IntPtr surfacePtr, ref Guid refguid)
        {
            IS_FREEING_PRIVATE_DATA = true;
            privateDataTimer.Stop();
            privateDataTimer.Start();
            return Direct3D9Surface_FreePrivateDataHook.Original(surfacePtr, ref refguid);
        }


        /***********************/
        /*** private methods ***/
        /***********************/

        /// <summary>
        /// Implementation of capturing from the render target of the Direct3D9 Device (or DeviceEx)
        /// </summary>
        /// <param name="device"></param>
        private void DoCaptureRenderTarget(Device device, Surface surface, string hook)
        {
            try
            {
                #region Frame capturing

                // If the resource is initialised we can capture the frame
                if (this._resourcesInitialised)
                {
                    // The GPU has finished copying data to _renderTargetCopy, we can now lock
                    // the data and access it on another thread.

                    // Lock the render target
                    SharpDX.Rectangle rect;
                    SharpDX.DataRectangle lockedRect = LockRenderTarget(_renderTargetCopy, out rect);
                    _renderTargetCopyLocked = true;

                    // Copy the data from the render target
                    System.Threading.Tasks.Task.Factory.StartNew(() =>
                    {
                        lock (_lockRenderTarget)
                        {
                            ProcessFrame(rect.Width, rect.Height, lockedRect.Pitch, _renderTargetCopy.Description.Format.ToPixelFormat(), lockedRect.DataPointer);
                        }
                    });
                }
                int width;
                int height;

                if ((surface.Description.Width > this.captureWidth || surface.Description.Height > this.captureHeight))
                {
                    if (surface.Description.Width > this.captureWidth)
                    {
                        width = this.captureWidth;
                        height = (int)Math.Round((surface.Description.Height * ((double)this.captureWidth / (double)surface.Description.Width)));
                    }
                    else
                    {
                        height = this.captureHeight;
                        width = (int)Math.Round((surface.Description.Width * ((double)this.captureHeight / (double)surface.Description.Height)));
                    }
                }
                else
                {
                    width = surface.Description.Width;
                    height = surface.Description.Height;
                }


                // int width = this.captureWidth;
                // int height = this.captureHeight;

                //int width = surface.Description.Width;
                //int height = surface.Description.Height;

                //int width = 1280; // 720p
                //int height = 720;

                //int width = 720; // 540p // recommended
                //int height = 540;

                //int width = 640; // 360p
                //int height = 360;

                //int width = 350; // 250p
                //int height = 250;

                // If existing _renderTargetCopy, ensure that it is the correct size and format
                if (_renderTargetCopy != null && (_renderTargetCopy.Description.Width != width || _renderTargetCopy.Description.Height != height || _renderTargetCopy.Description.Format != surface.Description.Format))
                {
                    // Cleanup resources
                    Cleanup();
                }

                // Ensure that we have something to put the render target data into
                if (!_resourcesInitialised || _renderTargetCopy == null)
                {
                    CreateResources(device, width, height, surface.Description.Format);
                }

                // Resize from render target Surface to resolvedSurface (also deals with resolving multi-sampling)
                device.StretchRectangle(surface, _resolvedTarget, TextureFilter.None);

                // If the render target is locked from a previous request unlock it
                if (_renderTargetCopyLocked)
                {
                    // Wait for the the ProcessCapture thread to finish with it
                    lock (_lockRenderTarget)
                    {
                        if (_renderTargetCopyLocked)
                        {
                            _renderTargetCopy.UnlockRectangle();
                            _renderTargetCopyLocked = false;
                        }
                    }
                }

                // Copy data from resolved target to our render target copy
                device.GetRenderTargetData(_resolvedTarget, _renderTargetCopy);

                #endregion
            }
            catch (Exception e)
            {
                this.Cleanup();
                DebugMessage(e.ToString());
            }
        }


        private SharpDX.DataRectangle LockRenderTarget(Surface _renderTargetCopy, out SharpDX.Rectangle rect)
        {
            rect = new SharpDX.Rectangle(0, 0, _renderTargetCopy.Description.Width, _renderTargetCopy.Description.Height);
            return _renderTargetCopy.LockRectangle(rect, LockFlags.ReadOnly);
        }

        private void CreateResources(Device device, int width, int height, Format format)
        {
            if (_resourcesInitialised) return;
            _resourcesInitialised = true;

            // Create offscreen surface to use as copy of render target data
            _renderTargetCopy = ToDispose(Surface.CreateOffscreenPlain(device, width, height, format, Pool.SystemMemory));

            // Create our resolved surface (resizing if necessary and to resolve any multi-sampling)
            _resolvedTarget = ToDispose(Surface.CreateRenderTarget(device, width, height, format, MultisampleType.None, 0, false));
        }

        protected override void Dispose(bool disposeManagedResources)
        {
            privateDataTimer.Dispose();
            base.Dispose(disposeManagedResources);
        }
    }
}
