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

    // Resize fields
    private ResizeEdge _activeEdge = ResizeEdge.None;
    private Point _resizeStartPoint;
    private double _scaleAtResizeStart;
    private Point _anchorPoint;

    private enum ResizeEdge
    {
        None, Left, Right, Top, Bottom,
        TopLeft, TopRight, BottomLeft, BottomRight
    }

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

        var point = e.GetPosition(this);
        _activeEdge = GetEdgeAtPoint(point);

        if (_activeEdge != ResizeEdge.None)
        {
            // Start resize
            _resizeStartPoint = PointToScreen(point);
            _scaleAtResizeStart = ViewModel.Scale;
            _anchorPoint = GetAnchorPoint(_activeEdge);
            CaptureMouse();
        }
        else
        {
            // Start drag
            _isDragging = true;
            _dragStartPoint = e.GetPosition(null);
            _windowStartPosition = new Point(Left, Top);
            CaptureMouse();
        }
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_activeEdge != ResizeEdge.None)
        {
            _activeEdge = ResizeEdge.None;
            ReleaseMouseCapture();
            ViewModel.X = Left;
            ViewModel.Y = Top;
        }
        else if (_isDragging)
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
        if (ViewModel.IsLocked)
        {
            Cursor = Cursors.Arrow;
            return;
        }

        var point = e.GetPosition(this);

        if (_activeEdge != ResizeEdge.None && e.LeftButton == MouseButtonState.Pressed)
        {
            // Handle resize
            var currentPoint = PointToScreen(point);
            var delta = currentPoint - _resizeStartPoint;

            var distance = _activeEdge switch
            {
                ResizeEdge.TopLeft or ResizeEdge.BottomRight =>
                    Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y),
                ResizeEdge.TopRight or ResizeEdge.BottomLeft =>
                    Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y),
                ResizeEdge.Left or ResizeEdge.Right => Math.Abs(delta.X),
                ResizeEdge.Top or ResizeEdge.Bottom => Math.Abs(delta.Y),
                _ => 0
            };

            var growing = _activeEdge switch
            {
                ResizeEdge.Right or ResizeEdge.Bottom or ResizeEdge.BottomRight =>
                    delta.X > 0 || delta.Y > 0,
                ResizeEdge.Left or ResizeEdge.Top or ResizeEdge.TopLeft =>
                    delta.X < 0 || delta.Y < 0,
                ResizeEdge.TopRight => delta.X > 0 || delta.Y < 0,
                ResizeEdge.BottomLeft => delta.X < 0 || delta.Y > 0,
                _ => false
            };

            var factor = growing ? 1 + (distance / 200) : 1 / (1 + (distance / 200));
            ViewModel.Scale = Math.Clamp(_scaleAtResizeStart * factor, 0.1, 10.0);

            // Reposition to keep anchor fixed
            RepositionForAnchor();
        }
        else if (_isDragging)
        {
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
        else
        {
            // Update cursor based on edge hover
            var edge = GetEdgeAtPoint(point);
            Cursor = GetCursorForEdge(edge);
        }
    }

    private ResizeEdge GetEdgeAtPoint(Point point)
    {
        const double edgeSize = 10;
        const double cornerSize = 16;

        var width = ActualWidth;
        var height = ActualHeight;

        var nearLeft = point.X < edgeSize;
        var nearRight = point.X > width - edgeSize;
        var nearTop = point.Y < edgeSize;
        var nearBottom = point.Y > height - edgeSize;

        var inCornerX = point.X < cornerSize || point.X > width - cornerSize;
        var inCornerY = point.Y < cornerSize || point.Y > height - cornerSize;

        if (nearTop && nearLeft && inCornerX && inCornerY) return ResizeEdge.TopLeft;
        if (nearTop && nearRight && inCornerX && inCornerY) return ResizeEdge.TopRight;
        if (nearBottom && nearLeft && inCornerX && inCornerY) return ResizeEdge.BottomLeft;
        if (nearBottom && nearRight && inCornerX && inCornerY) return ResizeEdge.BottomRight;
        if (nearLeft) return ResizeEdge.Left;
        if (nearRight) return ResizeEdge.Right;
        if (nearTop) return ResizeEdge.Top;
        if (nearBottom) return ResizeEdge.Bottom;

        return ResizeEdge.None;
    }

    private Cursor GetCursorForEdge(ResizeEdge edge) => edge switch
    {
        ResizeEdge.Left or ResizeEdge.Right => Cursors.SizeWE,
        ResizeEdge.Top or ResizeEdge.Bottom => Cursors.SizeNS,
        ResizeEdge.TopLeft or ResizeEdge.BottomRight => Cursors.SizeNWSE,
        ResizeEdge.TopRight or ResizeEdge.BottomLeft => Cursors.SizeNESW,
        _ => Cursors.Arrow
    };

    private Point GetAnchorPoint(ResizeEdge edge)
    {
        var width = ActualWidth;
        var height = ActualHeight;

        return edge switch
        {
            ResizeEdge.TopLeft => new Point(Left + width, Top + height),
            ResizeEdge.TopRight => new Point(Left, Top + height),
            ResizeEdge.BottomLeft => new Point(Left + width, Top),
            ResizeEdge.BottomRight => new Point(Left, Top),
            ResizeEdge.Left => new Point(Left + width, Top + height / 2),
            ResizeEdge.Right => new Point(Left, Top + height / 2),
            ResizeEdge.Top => new Point(Left + width / 2, Top + height),
            ResizeEdge.Bottom => new Point(Left + width / 2, Top),
            _ => new Point(Left, Top)
        };
    }

    private void RepositionForAnchor()
    {
        var width = ActualWidth;
        var height = ActualHeight;

        switch (_activeEdge)
        {
            case ResizeEdge.TopLeft:
                Left = _anchorPoint.X - width;
                Top = _anchorPoint.Y - height;
                break;
            case ResizeEdge.TopRight:
                Top = _anchorPoint.Y - height;
                break;
            case ResizeEdge.BottomLeft:
                Left = _anchorPoint.X - width;
                break;
            case ResizeEdge.Top:
                Left = _anchorPoint.X - width / 2;
                Top = _anchorPoint.Y - height;
                break;
            case ResizeEdge.Bottom:
                Left = _anchorPoint.X - width / 2;
                break;
            case ResizeEdge.Left:
                Left = _anchorPoint.X - width;
                Top = _anchorPoint.Y - height / 2;
                break;
            case ResizeEdge.Right:
                Top = _anchorPoint.Y - height / 2;
                break;
        }
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
