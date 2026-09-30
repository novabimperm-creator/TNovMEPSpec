using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shell;

namespace TNovMEPSpec
{
    /// <summary>
    /// Сворачивание безрамочного окна (как в окнах «Вопросы» и «Чек-лист проекта» TNovUtils):
    /// в панель задач и в полупрозрачную полоску с заголовком. Скрываются все строки
    /// корневой сетки, кроме нулевой (шапки).
    /// </summary>
    internal sealed class MEPSpecWindowCollapse
    {
        const double CollapsedOpacity = 0.55;

        readonly Window _window;
        readonly Grid _root;
        readonly ColumnDefinition _titleColumn;
        readonly Button _minimizeButton;
        readonly Button _collapseButton;
        readonly Button _closeButton;

        bool _isCollapsed;
        double _restoreHeight;
        double _restoreWidth;
        double _restoreMinHeight;
        double _restoreMinWidth;
        ResizeMode _restoreResizeMode;

        public MEPSpecWindowCollapse(Window window, Grid root, ColumnDefinition titleColumn,
            Button minimizeButton, Button collapseButton, Button closeButton)
        {
            _window = window;
            _root = root;
            _titleColumn = titleColumn;
            _minimizeButton = minimizeButton;
            _collapseButton = collapseButton;
            _closeButton = closeButton;
        }

        /// <summary>Вызывать из SourceInitialized: без WS_MINIMIZEBOX окно WindowStyle=None не сворачивается.</summary>
        public void OnSourceInitialized()
        {
            var hwnd = new WindowInteropHelper(_window).Handle;
            long ex = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            ex = (ex | WS_EX_APPWINDOW) & ~(long)WS_EX_TOOLWINDOW;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(ex));

            long style = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
            style |= WS_MINIMIZEBOX;
            SetWindowLongPtr(hwnd, GWL_STYLE, new IntPtr(style));
        }

        public void Minimize() => _window.WindowState = WindowState.Minimized;

        public void Toggle()
        {
            if (_isCollapsed) Expand();
            else Collapse();
        }

        public void OnMouseEnter()
        {
            if (_isCollapsed) _window.Opacity = 1;
        }

        public void OnMouseLeave()
        {
            if (_isCollapsed) _window.Opacity = CollapsedOpacity;
        }

        void SetContentVisibility(Visibility visibility)
        {
            foreach (UIElement child in _root.Children)
                if (Grid.GetRow(child) > 0)
                    child.Visibility = visibility;
        }

        void Collapse()
        {
            if (_window.WindowState == WindowState.Maximized)
                _window.WindowState = WindowState.Normal;

            _restoreHeight = _window.ActualHeight;
            _restoreWidth = _window.ActualWidth;
            _restoreMinHeight = _window.MinHeight;
            _restoreMinWidth = _window.MinWidth;
            _restoreResizeMode = _window.ResizeMode;

            SetContentVisibility(Visibility.Collapsed);
            _minimizeButton.Visibility = Visibility.Collapsed;
            _closeButton.Visibility = Visibility.Collapsed;
            _titleColumn.Width = GridLength.Auto;
            _collapseButton.Content = "v";
            _collapseButton.ToolTip = "Развернуть";
            _collapseButton.Margin = new Thickness(12, 0, 0, 0);

            _window.MinHeight = 0;
            _window.MinWidth = 0;
            _window.UpdateLayout();
            _window.SizeToContent = SizeToContent.WidthAndHeight;
            _window.ResizeMode = ResizeMode.NoResize;
            _window.Opacity = _window.IsMouseOver ? 1 : CollapsedOpacity;

            var chrome = WindowChrome.GetWindowChrome(_window);
            if (chrome != null)
            {
                chrome.CaptionHeight = 0;
                chrome.ResizeBorderThickness = new Thickness(0);
            }

            _isCollapsed = true;
        }

        void Expand()
        {
            _window.SizeToContent = SizeToContent.Manual;
            SetContentVisibility(Visibility.Visible);
            _minimizeButton.Visibility = Visibility.Visible;
            _closeButton.Visibility = Visibility.Visible;
            _titleColumn.Width = new GridLength(1, GridUnitType.Star);
            _collapseButton.Content = "^";
            _collapseButton.ToolTip = "Свернуть в полоску";
            _collapseButton.Margin = new Thickness(0, 0, 6, 0);

            _window.MinHeight = _restoreMinHeight;
            _window.MinWidth = _restoreMinWidth;
            _window.Width = _restoreWidth;
            _window.Height = _restoreHeight;
            _window.ResizeMode = _restoreResizeMode;
            _window.Opacity = 1;

            var chrome = WindowChrome.GetWindowChrome(_window);
            if (chrome != null)
            {
                chrome.CaptionHeight = 30;
                chrome.ResizeBorderThickness = new Thickness(5);
            }

            _isCollapsed = false;
        }

        const int GWL_STYLE = -16;
        const int GWL_EXSTYLE = -20;
        const int WS_MINIMIZEBOX = 0x00020000;
        const int WS_EX_APPWINDOW = 0x00040000;
        const int WS_EX_TOOLWINDOW = 0x00000080;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }
}
