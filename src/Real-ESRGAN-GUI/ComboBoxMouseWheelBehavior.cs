using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace RealESRGAN_GUI;

public static class ComboBoxMouseWheelBehavior
{
    public static readonly DependencyProperty RouteClosedWheelToParentProperty =
        DependencyProperty.RegisterAttached(
            "RouteClosedWheelToParent",
            typeof(bool),
            typeof(ComboBoxMouseWheelBehavior),
            new PropertyMetadata(false, OnRouteClosedWheelToParentChanged));

    public static void SetRouteClosedWheelToParent(DependencyObject element, bool value)
    {
        element.SetValue(RouteClosedWheelToParentProperty, value);
    }

    public static bool GetRouteClosedWheelToParent(DependencyObject element)
    {
        return (bool)element.GetValue(RouteClosedWheelToParentProperty);
    }

    private static void OnRouteClosedWheelToParentChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ComboBox comboBox)
        {
            return;
        }

        comboBox.PreviewMouseWheel -= OnPreviewMouseWheel;

        if (e.NewValue is true)
        {
            comboBox.PreviewMouseWheel += OnPreviewMouseWheel;
        }
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.IsDropDownOpen)
        {
            return;
        }

        e.Handled = true;

        ScrollViewer? scrollViewer = FindParent<ScrollViewer>(comboBox);
        if (scrollViewer is null)
        {
            return;
        }

        var forwardedArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = UIElement.MouseWheelEvent,
            Source = comboBox,
        };

        scrollViewer.RaiseEvent(forwardedArgs);
    }

    private static T? FindParent<T>(DependencyObject element)
        where T : DependencyObject
    {
        DependencyObject? current = element;

        while (current is not null)
        {
            current = GetParent(current);
            if (current is T typedParent)
            {
                return typedParent;
            }
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject element)
    {
        if (element is Visual or Visual3D)
        {
            return VisualTreeHelper.GetParent(element);
        }

        return LogicalTreeHelper.GetParent(element);
    }
}
