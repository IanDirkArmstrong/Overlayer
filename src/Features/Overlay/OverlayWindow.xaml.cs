using System.Windows;
using System.Windows.Input;
using Overlayer.Shared.Native;

namespace Overlayer.Features.Overlay;

public partial class OverlayWindow : Window
{
    private OverlayViewModel ViewModel => (OverlayViewModel)DataContext;

    private bool _isDragging;
    private Point _dragStartPoint;
    private Point _windowStartPosition;

    public OverlayWindow()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        MouseWheel += OnMouseWheel;
        KeyDown += OnKeyDown;
    }

    public OverlayWindow(OverlayViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += Close;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set initial position
        Left = ViewModel.X;
        Top = ViewModel.Y;

        // Load the image
        ViewModel.LoadImage();

        // Apply click-through if locked
        if (ViewModel.IsLocked)
            ApplyClickThrough();

        // Subscribe to lock changes
        ViewModel.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName == nameof(OverlayViewModel.IsLocked))
            {
                if (ViewModel.IsLocked)
                    ApplyClickThrough();
                else
                    RemoveClickThrough();
            }
        };
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.IsLocked) return;

        _isDragging = true;
        _dragStartPoint = e.GetPosition(null);
        _windowStartPosition = new Point(Left, Top);
        CaptureMouse();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();

            // Update ViewModel position
            ViewModel.X = Left;
            ViewModel.Y = Top;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var currentPoint = e.GetPosition(null);
        var offset = currentPoint - _dragStartPoint;

        var newX = _windowStartPosition.X + offset.X;
        var newY = _windowStartPosition.Y + offset.Y;

        // Apply edge snapping if enabled
        if (ViewModel.SnapToEdges)
        {
            var snapResult = ApplyEdgeSnapping(newX, newY);
            newX = snapResult.X;
            newY = snapResult.Y;
        }

        Left = newX;
        Top = newY;
    }

    private Point ApplyEdgeSnapping(double x, double y)
    {
        var screen = SystemParameters.WorkArea;
        var snapDistance = 20.0;

        // Snap to left edge
        if (Math.Abs(x) < snapDistance)
            x = 0;

        // Snap to top edge
        if (Math.Abs(y) < snapDistance)
            y = 0;

        // Snap to right edge
        var rightEdge = screen.Width - ActualWidth;
        if (Math.Abs(x - rightEdge) < snapDistance)
            x = rightEdge;

        // Snap to bottom edge
        var bottomEdge = screen.Height - ActualHeight;
        if (Math.Abs(y - bottomEdge) < snapDistance)
            y = bottomEdge;

        return new Point(x, y);
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel.IsLocked) return;

        var notches = e.Delta / 120.0;
        var factor = Math.Pow(1.03, notches);
        ViewModel.Scale = Math.Clamp(ViewModel.Scale * factor, 0.1, 10.0);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.L:
                ViewModel.LockCommand.Execute(null);
                break;
            case Key.U:
                ViewModel.UnlockCommand.Execute(null);
                break;
            case Key.Escape:
                ViewModel.CloseCommand.Execute(null);
                break;
            case Key.Add:
            case Key.OemPlus:
                ViewModel.ScaleUpCommand.Execute(null);
                break;
            case Key.Subtract:
            case Key.OemMinus:
                ViewModel.ScaleDownCommand.Execute(null);
                break;
            case Key.D0:
            case Key.NumPad0:
                ViewModel.ResetScaleCommand.Execute(null);
                break;
        }
    }

    private void ApplyClickThrough()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var extendedStyle = NativeMethods.GetWindowLongW(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongW(hwnd, NativeMethods.GWL_EXSTYLE,
            extendedStyle | NativeMethods.WS_EX_TRANSPARENT);
    }

    private void RemoveClickThrough()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var extendedStyle = NativeMethods.GetWindowLongW(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongW(hwnd, NativeMethods.GWL_EXSTYLE,
            extendedStyle & ~NativeMethods.WS_EX_TRANSPARENT);
    }
}
