// SPDX-License-Identifier: GPL-3.0-only
//
// Native.cs - the complete list of Windows APIs this program calls.
//
// READ THIS FILE FIRST if you want to audit what Echo HUD can do.
// Everything the app does outside of normal .NET code goes through here,
// and the list is deliberately short:
//
//   * user32  - read window metadata (title, class, size, position) and
//               style OUR OWN windows.
//   * dwmapi  - ask the Windows compositor (DWM) to draw a live thumbnail of
//               another window inside one of OUR windows. This is the same
//               mechanism as the Alt+Tab and taskbar previews.
//
// What is intentionally NOT here, and must never be added:
//   ReadProcessMemory / WriteProcessMemory / OpenProcess, any injection or
//   hook API (SetWindowsHookEx, CreateRemoteThread...), any input API
//   (SendInput, PostMessage/SendMessage to other windows, keybd_event...),
//   and any screen-pixel capture API (BitBlt, PrintWindow, CopyFromScreen).
//   The program never reads a single pixel of the game: Windows composes the
//   image, we only say where to put it.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace EchoHud
{
    internal static class Native
    {
        internal delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left, Top, Right, Bottom;

            public RECT(Rectangle r)
            {
                Left = r.Left;
                Top = r.Top;
                Right = r.Right;
                Bottom = r.Bottom;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            public int X, Y;
        }

        /// <summary>DWM_THUMBNAIL_PROPERTIES from dwmapi.h.</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct ThumbnailProperties
        {
            public uint Flags;
            public RECT Destination;
            public RECT Source;
            public byte Opacity;
            [MarshalAs(UnmanagedType.Bool)] public bool Visible;
            [MarshalAs(UnmanagedType.Bool)] public bool SourceClientAreaOnly;
        }

        // DWM_TNP_* flags: which fields of ThumbnailProperties are valid.
        internal const uint TnpDestination = 0x01;
        internal const uint TnpSource = 0x02;
        internal const uint TnpOpacity = 0x04;
        internal const uint TnpVisible = 0x08;
        internal const uint TnpClientOnly = 0x10;
        internal const uint TnpAll = 0x1F;

        // Extended window styles applied to our own mirror windows.
        internal const int WsExTransparent = 0x00000020; // mouse clicks pass through
        internal const int WsExToolWindow = 0x00000080;  // hidden from Alt+Tab
        internal const int WsExLayered = 0x00080000;     // supports whole-window opacity
        internal const int WsExNoActivate = 0x08000000;  // never steals keyboard focus
        internal const uint LwaAlpha = 0x2;

        // Window messages handled by our own windows.
        internal const int WmNcHitTest = 0x0084;
        internal const int WmNcRButtonUp = 0x00A5;
        internal const int WmNcLButtonDblClk = 0x00A3;
        internal const int HtCaption = 2;
        internal const int HtBottomRight = 17;

        // ---- process-wide display settings for THIS process ----
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);

        // ---- read-only window metadata ----
        [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder text, int capacity);
        [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window, out RECT rect);
        [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window, ref POINT point);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        // ---- styling of our own windows ----
        [DllImport("user32.dll")] internal static extern bool SetLayeredWindowAttributes(IntPtr ownWindow, uint colorKey, byte alpha, uint flags);

        // ---- Desktop Window Manager thumbnails ----
        [DllImport("dwmapi.dll")] internal static extern int DwmRegisterThumbnail(IntPtr destination, IntPtr source, out IntPtr thumbnail);
        [DllImport("dwmapi.dll")] internal static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
        [DllImport("dwmapi.dll")] internal static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail, ref ThumbnailProperties properties);

        internal static string Title(IntPtr window)
        {
            StringBuilder text = new StringBuilder(512);
            GetWindowText(window, text, text.Capacity);
            return text.ToString();
        }

        internal static string ClassName(IntPtr window)
        {
            StringBuilder text = new StringBuilder(256);
            GetClassName(window, text, text.Capacity);
            return text.ToString();
        }

        /// <summary>Size of a window's client area (its content, without title bar or borders).</summary>
        internal static Size ClientSize(IntPtr window)
        {
            RECT rect;
            if (!GetClientRect(window, out rect)) return Size.Empty;
            return new Size(rect.Right - rect.Left, rect.Bottom - rect.Top);
        }

        /// <summary>Where a window's client area currently sits on the desktop.</summary>
        internal static Rectangle ClientScreenBounds(IntPtr window)
        {
            Size size = ClientSize(window);
            POINT origin = new POINT();
            if (size.IsEmpty || !ClientToScreen(window, ref origin)) return Rectangle.Empty;
            return new Rectangle(origin.X, origin.Y, size.Width, size.Height);
        }

        /// <summary>
        /// Opts this process into per-monitor DPI awareness so that every
        /// coordinate we handle is a real pixel on every monitor.
        /// </summary>
        internal static void EnablePixelAccurateCoordinates()
        {
            try
            {
                // -4 = DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 (Windows 10 1703+).
                if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware();
            }
            catch (EntryPointNotFoundException)
            {
                SetProcessDPIAware();
            }
        }
    }
}
