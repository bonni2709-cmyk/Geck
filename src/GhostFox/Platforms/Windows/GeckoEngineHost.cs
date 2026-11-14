using System.Runtime.InteropServices;
using GhostFox.Core.Engines;
using GhostFox.Core.Models;
using Microsoft.UI.Dispatching;
using Grid = Microsoft.Maui.Controls.Grid;

namespace GhostFox.Platforms.Windows;

/// <summary>
/// Встраивает WinForms-контрол GeckoWebBrowser в MAUI/WinUI3 окно:
/// SetParent(childHwnd, mainWindowHwnd) + периодическая синхронизация позиции
/// с MAUI Grid-плейсхолдером (EngineHost в MainPage). На вкладку — свой контрол,
/// виден только активный (изоляция вкладок на уровне отдельных GeckoWebBrowser).
/// </summary>
internal sealed class GeckoEngineHost : IBrowserEngineHost, IEngineSwitcher
{
    private readonly List<GeckoFxEngine> _engines = new();
    private GeckoFxEngine? _visible;
    private Grid? _placeholder;
    private DispatcherQueueTimer? _syncTimer;

    public IEngine CreateAndAttach(object hostContainer, BrowserSettings settings)
    {
        _placeholder = hostContainer as Grid;
        if (!GeckoRuntime.EnsureInitialized())
            return new PlaceholderEngine();

        var engine = new GeckoFxEngine(settings);
        _engines.Add(engine);
        _visible ??= engine;
        EnsureParented(engine);
        StartSyncLoop();
        return engine;
    }

    public void ShowEngine(IEngine? engine)
    {
        _visible = engine as GeckoFxEngine;
        foreach (var e in _engines)
            if (e.NativeControl.IsHandleCreated)
                ShowWindow(e.NativeControl.Handle, e == _visible ? SW_SHOW : SW_HIDE);
    }

    private void EnsureParented(GeckoFxEngine engine)
    {
        var control = engine.NativeControl;
        if (!control.IsHandleCreated) _ = control.Handle; // форсим создание HWND
        var parent = GetMainWindowHandle();
        if (parent != IntPtr.Zero && control.IsHandleCreated)
            SetParent(control.Handle, parent);
    }

    private void StartSyncLoop()
    {
        if (_syncTimer != null || _placeholder?.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement element)
            return;
        var queue = DispatcherQueue.GetForCurrentThread();
        if (queue == null) return;
        _syncTimer = queue.CreateTimer();
        _syncTimer.Interval = TimeSpan.FromMilliseconds(200);
        _syncTimer.IsRepeating = true;
        _syncTimer.Tick += (_, _) => SyncBounds(element);
        _syncTimer.Start();
    }

    private void SyncBounds(Microsoft.UI.Xaml.FrameworkElement element)
    {
        try
        {
            if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
                    is not Microsoft.UI.Xaml.Window window || window.Content is not Microsoft.UI.Xaml.UIElement root)
                return;

            var point = element.TransformToVisual(root).TransformPoint(new global::Windows.Foundation.Point(0, 0));
            var scale = element.XamlRoot?.RasterizationScale ?? 1.0;
            var x = (int)(point.X * scale);
            var y = (int)(point.Y * scale);
            var w = Math.Max(0, (int)(element.ActualWidth * scale));
            var h = Math.Max(0, (int)(element.ActualHeight * scale));

            foreach (var engine in _engines)
            {
                if (!engine.NativeControl.IsHandleCreated) EnsureParented(engine);
                if (engine == _visible)
                    SetWindowPos(engine.NativeControl.Handle, IntPtr.Zero, x, y, w, h,
                        SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                else
                    ShowWindow(engine.NativeControl.Handle, SW_HIDE);
            }
        }
        catch { /* окно могло быть в процессе закрытия */ }
    }

    private static IntPtr GetMainWindowHandle()
    {
        if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
                is Microsoft.UI.Xaml.Window window)
            return WinRT.Interop.WindowNative.GetWindowHandle(window);
        return IntPtr.Zero;
    }

    private const int SW_HIDE = 0;
    private const int SW_SHOW = 5;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
