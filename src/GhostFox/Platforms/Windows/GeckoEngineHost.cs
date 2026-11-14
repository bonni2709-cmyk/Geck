using System.Runtime.InteropServices;
using GhostFox.Core.Engines;
using GhostFox.Core.Models;
using Microsoft.UI.Dispatching;
using Grid = Microsoft.Maui.Controls.Grid;

namespace GhostFox.Platforms.Windows;

/// <summary>
/// Встраивает WinForms-контрол GeckoWebBrowser в MAUI/WinUI3 окно:
/// SetParent(childHwnd, mainWindowHwnd) + синхронизация позиции каждого контрола
/// со своим MAUI-контейнером. Контейнеров может быть несколько: основной (вкладки)
/// и PiP-оверлей (Модуль 3). Виден только активный движок вкладок + видимые оверлеи.
/// </summary>
internal sealed class GeckoEngineHost : IBrowserEngineHost, IEngineSwitcher
{
    private sealed class Entry
    {
        public required GeckoFxEngine Engine;
        public required Microsoft.UI.Xaml.FrameworkElement Element;
        public bool IsOverlay;
        public bool OverlayVisible = true;
    }

    private readonly List<Entry> _entries = new();
    private GeckoFxEngine? _visibleMain;
    private DispatcherQueueTimer? _syncTimer;

    public IEngine CreateAndAttach(object hostContainer, BrowserSettings settings, bool isOverlay = false)
    {
        if (!GeckoRuntime.EnsureInitialized())
            return new PlaceholderEngine();
        if (hostContainer is not Grid grid || grid.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement element)
            return new PlaceholderEngine();

        var engine = new GeckoFxEngine(settings);
        _entries.Add(new Entry { Engine = engine, Element = element, IsOverlay = isOverlay });
        if (!isOverlay) _visibleMain ??= engine;
        EnsureParented(engine);
        StartSyncLoop();
        return engine;
    }

    public void ShowEngine(IEngine? engine)
    {
        _visibleMain = engine as GeckoFxEngine;
        ApplyVisibility();
    }

    public void SetOverlayVisibility(IEngine engine, bool visible)
    {
        var entry = _entries.FirstOrDefault(e => ReferenceEquals(e.Engine, engine));
        if (entry != null) entry.OverlayVisible = visible;
        ApplyVisibility();
    }

    private void ApplyVisibility()
    {
        foreach (var e in _entries)
            if (e.Engine.NativeControl.IsHandleCreated)
                ShowWindow(e.Engine.NativeControl.Handle, IsEntryVisible(e) ? SW_SHOW : SW_HIDE);
    }

    private bool IsEntryVisible(Entry e) => e.IsOverlay ? e.OverlayVisible : ReferenceEquals(e.Engine, _visibleMain);

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
        if (_syncTimer != null) return;
        var queue = DispatcherQueue.GetForCurrentThread();
        if (queue == null) return;
        _syncTimer = queue.CreateTimer();
        _syncTimer.Interval = TimeSpan.FromMilliseconds(200);
        _syncTimer.IsRepeating = true;
        _syncTimer.Tick += (_, _) => SyncBounds();
        _syncTimer.Start();
    }

    private void SyncBounds()
    {
        try
        {
            if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
                    is not Microsoft.UI.Xaml.Window window || window.Content is not Microsoft.UI.Xaml.UIElement root)
                return;

            foreach (var entry in _entries)
            {
                if (!entry.Engine.NativeControl.IsHandleCreated) EnsureParented(entry.Engine);
                if (!IsEntryVisible(entry))
                {
                    ShowWindow(entry.Engine.NativeControl.Handle, SW_HIDE);
                    continue;
                }
                var element = entry.Element;
                var point = element.TransformToVisual(root).TransformPoint(new global::Windows.Foundation.Point(0, 0));
                var scale = element.XamlRoot?.RasterizationScale ?? 1.0;
                SetWindowPos(entry.Engine.NativeControl.Handle, IntPtr.Zero,
                    (int)(point.X * scale), (int)(point.Y * scale),
                    Math.Max(0, (int)(element.ActualWidth * scale)),
                    Math.Max(0, (int)(element.ActualHeight * scale)),
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
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
