// Copyright © John Gietzen. All Rights Reserved. This source is subject to the GPL license. Please see license.md for more information.

namespace Codec.UI.WinForms
{
    using Silk.NET.Core.Contexts;
    using Silk.NET.OpenGL;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;

    public class SilkControl : UserControl
    {
        private IntPtr hdc;
        private IntPtr hglrc;

        protected GL gl;
        private Timer timer;

        public SilkControl()
        {
            this.SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            this.timer = new Timer { Interval = 1000 / 60 };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            this.CreateGLContext();
            this.gl = GL.GetApi(new SilkNativeContext());
            this.timer.Tick += this.Tick;
            this.timer.Enabled = true;
            wglMakeCurrent(this.hdc, this.hglrc);
            this.Initialize();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            this.timer.Tick -= this.Tick;
            this.timer.Enabled = false;
            this.DestroyGLContext();
            base.OnHandleDestroyed(e);
            wglMakeCurrent(0, 0);
        }

        private void Tick(object? sender, EventArgs e)
        {
            wglMakeCurrent(this.hdc, this.hglrc);
            this.Update();
            this.Render();
            SwapBuffers(this.hdc);
        }

        protected virtual void Initialize() { }
        protected virtual void Update() { }
        protected virtual void Render() { }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.timer.Dispose();
            }

            base.Dispose(disposing);
        }

        class SilkNativeContext : INativeContext
        {
            [DllImport("opengl32.dll")]
            static extern IntPtr wglGetProcAddress(string name);

            [DllImport("kernel32.dll")]
            static extern IntPtr GetModuleHandle(string name);

            [DllImport("kernel32.dll")]
            static extern IntPtr GetProcAddress(IntPtr module, string name);

            private readonly nint opengl32 = GetModuleHandle("opengl32.dll");

            public nint GetProcAddress(string proc, int? slot = null)
            {
                if (this.TryGetProcAddress(proc, out var addr, slot))
                {
                    return addr;
                }

                return 0;
            }

            public bool TryGetProcAddress(string proc, out nint addr, int? slot = null)
            {
                addr = wglGetProcAddress(proc);

                if (addr == 0 || addr == 1 || addr == 2 || addr == 3 || addr == -1)
                {
                    addr = GetProcAddress(this.opengl32, proc);
                }

                return addr != 0;
            }

            public void Dispose()
            {
            }
        }

        private void CreateGLContext()
        {
            this.hdc = GetDC(this.Handle);

            PIXELFORMATDESCRIPTOR pfd = new()
            {
                nSize = (ushort)Marshal.SizeOf<PIXELFORMATDESCRIPTOR>(),
                nVersion = 1,
                dwFlags =
                    PFD_DRAW_TO_WINDOW |
                    PFD_SUPPORT_OPENGL |
                    PFD_DOUBLEBUFFER,
                iPixelType = PFD_TYPE_RGBA,
                cColorBits = 32,
                cDepthBits = 24,
                cStencilBits = 8,
                iLayerType = PFD_MAIN_PLANE,
            };

            int pf = ChoosePixelFormat(this.hdc, ref pfd);
            SetPixelFormat(this.hdc, pf, ref pfd);

            this.hglrc = wglCreateContext(this.hdc);
        }

        private void DestroyGLContext()
        {
            if (this.hglrc != IntPtr.Zero)
            {
                wglDeleteContext(this.hglrc);
            }

            if (this.hdc != IntPtr.Zero)
            {
                ReleaseDC(this.Handle, this.hdc);
            }
        }

        #region Constants

        private const uint PFD_DRAW_TO_WINDOW = 0x00000004;
        private const uint PFD_SUPPORT_OPENGL = 0x00000020;
        private const uint PFD_DOUBLEBUFFER = 0x00000001;
        private const byte PFD_TYPE_RGBA = 0;
        private const sbyte PFD_MAIN_PLANE = 0;

        #endregion

        #region Structs

        [StructLayout(LayoutKind.Sequential)]
        struct PIXELFORMATDESCRIPTOR
        {
            public ushort nSize;
            public ushort nVersion;
            public uint dwFlags;
            public byte iPixelType;
            public byte cColorBits;
            public byte cRedBits;
            public byte cRedShift;
            public byte cGreenBits;
            public byte cGreenShift;
            public byte cBlueBits;
            public byte cBlueShift;
            public byte cAlphaBits;
            public byte cAlphaShift;
            public byte cAccumBits;
            public byte cAccumRedBits;
            public byte cAccumGreenBits;
            public byte cAccumBlueBits;
            public byte cAccumAlphaBits;
            public byte cDepthBits;
            public byte cStencilBits;
            public byte cAuxBuffers;
            public sbyte iLayerType;
            public byte bReserved;
            public uint dwLayerMask;
            public uint dwVisibleMask;
            public uint dwDamageMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public UIntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int pt_x;
            public int pt_y;
        }

        #endregion

        #region P/Invoke

        [DllImport("user32.dll")]
        static extern bool PeekMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        static extern int ChoosePixelFormat(IntPtr hdc, ref PIXELFORMATDESCRIPTOR pfd);

        [DllImport("gdi32.dll")]
        static extern bool SetPixelFormat(IntPtr hdc, int format, ref PIXELFORMATDESCRIPTOR pfd);

        [DllImport("gdi32.dll")]
        static extern bool SwapBuffers(IntPtr hdc);

        [DllImport("opengl32.dll")]
        static extern IntPtr wglCreateContext(IntPtr hdc);

        [DllImport("opengl32.dll")]
        static extern bool wglDeleteContext(IntPtr hglrc);

        [DllImport("opengl32.dll")]
        static extern bool wglMakeCurrent(IntPtr hdc, IntPtr hglrc);

        #endregion
    }
}
