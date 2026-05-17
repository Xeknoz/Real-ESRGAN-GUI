using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace RealESRGAN_GUI;

public static class ScrollBarContextMenuBehavior
{
    public static readonly DependencyProperty UseLocalizedContextMenuProperty =
        DependencyProperty.RegisterAttached(
            "UseLocalizedContextMenu",
            typeof(bool),
            typeof(ScrollBarContextMenuBehavior),
            new PropertyMetadata(false, OnUseLocalizedContextMenuChanged));

    private static readonly DependencyProperty LastRightClickPointProperty =
        DependencyProperty.RegisterAttached(
            "LastRightClickPoint",
            typeof(Point),
            typeof(ScrollBarContextMenuBehavior),
            new PropertyMetadata(new Point(double.NaN, double.NaN)));

    public static void SetUseLocalizedContextMenu(DependencyObject element, bool value)
    {
        element.SetValue(UseLocalizedContextMenuProperty, value);
    }

    public static bool GetUseLocalizedContextMenu(DependencyObject element)
    {
        return (bool)element.GetValue(UseLocalizedContextMenuProperty);
    }

    private static void OnUseLocalizedContextMenuChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ScrollBar scrollBar)
        {
            return;
        }

        scrollBar.PreviewMouseRightButtonDown -= OnPreviewMouseRightButtonDown;
        scrollBar.ContextMenuOpening -= OnContextMenuOpening;

        if (e.NewValue is true)
        {
            scrollBar.PreviewMouseRightButtonDown += OnPreviewMouseRightButtonDown;
            scrollBar.ContextMenuOpening += OnContextMenuOpening;
            EnsureContextMenu(scrollBar);
        }
        else if (IsManagedMenu(scrollBar.ContextMenu))
        {
            scrollBar.ContextMenu = null;
        }
    }

    private static void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is ScrollBar scrollBar)
        {
            scrollBar.SetValue(LastRightClickPointProperty, e.GetPosition(scrollBar));
        }
    }

    private static void OnContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is ScrollBar scrollBar)
        {
            EnsureContextMenu(scrollBar);
        }
    }

    private static void EnsureContextMenu(ScrollBar scrollBar)
    {
        ContextMenu menu = IsManagedMenu(scrollBar.ContextMenu)
            ? scrollBar.ContextMenu
            : new ContextMenu();

        menu.Tag = typeof(ScrollBarContextMenuBehavior);
        menu.PlacementTarget = scrollBar;
        menu.Items.Clear();

        AddScrollHereItem(menu, scrollBar);

        if (scrollBar.Orientation == Orientation.Horizontal)
        {
            AddCommandItem(menu, scrollBar, "ScrollBarMenuLeftEdge", ScrollBar.ScrollToLeftEndCommand);
            AddCommandItem(menu, scrollBar, "ScrollBarMenuRightEdge", ScrollBar.ScrollToRightEndCommand);
            AddCommandItem(menu, scrollBar, "ScrollBarMenuPageLeft", ScrollBar.PageLeftCommand);
            AddCommandItem(menu, scrollBar, "ScrollBarMenuPageRight", ScrollBar.PageRightCommand);
            AddCommandItem(menu, scrollBar, "ScrollBarMenuScrollLeft", ScrollBar.LineLeftCommand);
            AddCommandItem(menu, scrollBar, "ScrollBarMenuScrollRight", ScrollBar.LineRightCommand);
        }
        else
        {
            AddCommandItem(menu, scrollBar, "ScrollBarMenuTop", ScrollBar.ScrollToTopCommand);
            AddCommandItem(menu, scrollBar, "ScrollBarMenuBottom", ScrollBar.ScrollToBottomCommand);
            AddCommandItem(menu, scrollBar, "ScrollBarMenuPageUp", ScrollBar.PageUpCommand);
            AddCommandItem(menu, scrollBar, "ScrollBarMenuPageDown", ScrollBar.PageDownCommand);
            AddCommandItem(menu, scrollBar, "ScrollBarMenuScrollUp", ScrollBar.LineUpCommand);
            AddCommandItem(menu, scrollBar, "ScrollBarMenuScrollDown", ScrollBar.LineDownCommand);
        }

        scrollBar.ContextMenu = menu;
    }

    private static bool IsManagedMenu(ContextMenu? menu)
    {
        return Equals(menu?.Tag, typeof(ScrollBarContextMenuBehavior));
    }

    private static void AddScrollHereItem(ContextMenu menu, ScrollBar scrollBar)
    {
        var item = new MenuItem
        {
            Header = Text("ScrollBarMenuScrollHere"),
            CommandTarget = scrollBar,
        };

        item.Click += (_, _) => ScrollHere(scrollBar);
        menu.Items.Add(item);
    }

    private static void AddCommandItem(ContextMenu menu, ScrollBar scrollBar, string textKey, ICommand command)
    {
        menu.Items.Add(new MenuItem
        {
            Header = Text(textKey),
            Command = command,
            CommandTarget = scrollBar,
        });
    }

    private static void ScrollHere(ScrollBar scrollBar)
    {
        if (scrollBar.GetValue(LastRightClickPointProperty) is not Point point ||
            double.IsNaN(point.X) ||
            double.IsNaN(point.Y))
        {
            return;
        }

        Track? track = FindVisualChild<Track>(scrollBar);
        if (track is null || scrollBar.Maximum <= scrollBar.Minimum)
        {
            return;
        }

        double trackLength = scrollBar.Orientation == Orientation.Horizontal
            ? track.ActualWidth
            : track.ActualHeight;
        double thumbLength = scrollBar.Orientation == Orientation.Horizontal
            ? track.Thumb.ActualWidth
            : track.Thumb.ActualHeight;
        double clickOffset = scrollBar.Orientation == Orientation.Horizontal ? point.X : point.Y;
        double movableLength = Math.Max(0, trackLength - thumbLength);

        if (movableLength <= 0)
        {
            return;
        }

        double ratio = (clickOffset - thumbLength / 2) / movableLength;
        ratio = Math.Clamp(ratio, 0, 1);

        if (track.IsDirectionReversed)
        {
            ratio = 1 - ratio;
        }

        double value = scrollBar.Minimum + (scrollBar.Maximum - scrollBar.Minimum) * ratio;
        scrollBar.SetCurrentValue(RangeBase.ValueProperty, value);
    }

    private static T? FindVisualChild<T>(DependencyObject element)
        where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(element, i);
            if (child is T typed)
            {
                return typed;
            }

            T? nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static string Text(string key)
    {
        return Application.Current?.Resources[key] as string ?? key;
    }
}
