using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Microsoft.Win32;

namespace RealESRGAN_GUI
{
    public partial class MainWindow
    {
        private const int WmSizing = 0x0214;
        private const int WmMoving = 0x0216;
        private const int WmEnterSizeMove = 0x0231;
        private const double HeaderPreferencePopupGap = 3;

        private void ConfigureHeaderActions()
        {
            ThemeButton.Click += OnThemeClick;
            LanguageButton.Click += OnLanguageClick;
            AboutButton.Click += OnAboutClick;
            ThemePopup.CustomPopupPlacementCallback = PlaceHeaderPreferencePopup;
            LanguagePopup.CustomPopupPlacementCallback = PlaceHeaderPreferencePopup;
            PreviewMouseDown += OnMainWindowPreviewMouseDown;
            PreviewKeyDown += OnMainWindowPreviewKeyDown;
        }

        private void ConfigurePreferencePopupWindowMessages()
        {
            if (PresentationSource.FromVisual(this) is HwndSource source)
            {
                source.AddHook(OnPreferencePopupWindowMessage);
            }
        }

        private void PopulatePreferenceCombos()
        {
            _updatingSelections = true;
            SetListItems(ThemeList, BuildThemeItems(), _themePreference);
            SetListItems(LanguageList, BuildLanguageItems(), _languagePreference);
            _updatingSelections = false;
            UpdateThemeButton();
            UpdateLanguageButtons();
            UpdateAboutButton();
        }

        private ComboItem[] BuildThemeItems() => new[]
        {
            new ComboItem("system", T("ThemeSystem")),
            new ComboItem("light",  T("ThemeLight")),
            new ComboItem("dark",   T("ThemeDark")),
        };

        private ComboItem[] BuildLanguageItems() => new[]
        {
            new ComboItem("auto", T("LanguageAuto")),
            new ComboItem("zh",   T("LanguageZh")),
            new ComboItem("en",   T("LanguageEn")),
        };

        private void PopulateComboBoxes()
        {
            string model = SelectedTag(ModelCombo) ?? "realesrgan-x4plus";
            string scale = SelectedTag(ScaleCombo) ?? string.Empty;
            string format = SelectedTag(FormatCombo) ?? "png";
            string threads = SelectedTag(ThreadsCombo) ?? "0";
            string gpu = SelectedTag(GpuCombo) ?? string.Empty;

            ModelCombo.SelectionChanged -= OnModelChanged;
            _updatingSelections = true;

            SetComboItems(ModelCombo, new[]
            {
                new ComboItem("realesrgan-x4plus",       T("ModelPhoto")),
                new ComboItem("realesrgan-x4plus-anime", T("ModelAnime")),
                new ComboItem("realesr-animevideov3-x2", T("ModelVideo2")),
                new ComboItem("realesr-animevideov3-x3", T("ModelVideo3")),
                new ComboItem("realesr-animevideov3-x4", T("ModelVideo4")),
            }, model);

            SetComboItems(ScaleCombo, BuildScaleItems(DefaultScaleFor(model)), scale);
            SetComboItems(FormatCombo, new[]
            {
                new ComboItem("png",  T("FormatPng")),
                new ComboItem("jpg",  T("FormatJpg")),
                new ComboItem("webp", T("FormatWebp")),
            }, format);

            SetComboItems(ThreadsCombo, new[]
            {
                new ComboItem("0", T("AutoRecommended")),
                new ComboItem("1", "1"),
                new ComboItem("2", "2"),
                new ComboItem("4", "4"),
                new ComboItem("8", "8"),
            }, threads);

            SetComboItems(GpuCombo, new[]
            {
                new ComboItem(string.Empty, T("AutoRecommended")),
                new ComboItem("0", "0"),
                new ComboItem("1", "1"),
                new ComboItem("2", "2"),
            }, gpu);

            _updatingSelections = false;
            ModelCombo.SelectionChanged += OnModelChanged;
        }

        private static void SetComboItems(ComboBox combo, ComboItem[] items, string selectedTag)
        {
            combo.ItemsSource = items;
            combo.DisplayMemberPath = nameof(ComboItem.Display);
            combo.SelectedItem = items.FirstOrDefault(item => item.Tag == selectedTag) ?? items[0];
        }

        private static void SetListItems(ListBox listBox, ComboItem[] items, string selectedTag)
        {
            listBox.ItemsSource = items;
            listBox.DisplayMemberPath = nameof(ComboItem.Display);
            listBox.SelectedItem = items.FirstOrDefault(item => item.Tag == selectedTag) ?? items[0];
        }

        private static string? SelectedTag(ComboBox combo)
            => combo.SelectedItem is ComboItem item ? item.Tag : null;

        private static string? SelectedTag(ListBox listBox)
            => listBox.SelectedItem is ComboItem item ? item.Tag : null;

        private static int DefaultScaleFor(string model) => model switch
        {
            "realesr-animevideov3-x2" => 2,
            "realesr-animevideov3-x3" => 3,
            _                         => 4,
        };

        private ComboItem[] BuildScaleItems(int defaultScale)
        {
            var items = new List<ComboItem>
            {
                new(string.Empty, string.Format(CultureInfo.CurrentCulture, T("ScaleAuto"), defaultScale)),
            };

            foreach (int scale in new[] { 2, 3, 4 })
            {
                if (scale == defaultScale) continue;
                items.Add(new ComboItem(scale.ToString(CultureInfo.InvariantCulture), $"{scale}x"));
            }

            return items.ToArray();
        }

        private void OnModelChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelections || ModelCombo.SelectedItem is not ComboItem mi) return;
            string scale = SelectedTag(ScaleCombo) ?? string.Empty;
            _updatingSelections = true;
            SetComboItems(ScaleCombo, BuildScaleItems(DefaultScaleFor(mi.Tag)), scale);
            _updatingSelections = false;
        }

        private void InitializeDefaults()
        {
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            _inputDir  = Path.Combine(pictures, "Real-ESRGAN_Input");
            _outputDir = Path.Combine(pictures, "Real-ESRGAN_Output");
            InputPathBox.Text  = _inputDir;
            OutputPathBox.Text = _outputDir;
            RefreshFolderSummaries();
            SetStatus("StatusReady");
            SetProgressText("ProgressZero");
        }

        private void OnThemeClick(object sender, RoutedEventArgs e)
        {
            TogglePreferencePopup(ThemePopup, ThemeList, LanguagePopup);
        }

        private void OnLanguageClick(object sender, RoutedEventArgs e)
        {
            TogglePreferencePopup(LanguagePopup, LanguageList, ThemePopup);
        }

        private void OnThemeDropdownSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelections)
            {
                return;
            }

            ClosePreferencePopups();
            string? selectedTag = SelectedTag(ThemeList);
            if (selectedTag is null ||
                string.Equals(selectedTag, _themePreference, StringComparison.Ordinal))
            {
                return;
            }

            _themePreference = selectedTag;
            ApplyThemePreference();
            PopulatePreferenceCombos();
        }

        private void OnLanguageDropdownSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelections)
            {
                return;
            }

            ClosePreferencePopups();
            string? selectedTag = SelectedTag(LanguageList);
            if (selectedTag is null ||
                string.Equals(selectedTag, _languagePreference, StringComparison.Ordinal))
            {
                return;
            }

            _languagePreference = selectedTag;
            _currentLanguage = ResolveLanguage();
            ApplyLanguage();
        }

        private void OnMainWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!ThemePopup.IsOpen && !LanguagePopup.IsOpen)
            {
                return;
            }

            if (IsWithin(e.OriginalSource as DependencyObject, ThemeButton) ||
                IsWithin(e.OriginalSource as DependencyObject, LanguageButton))
            {
                return;
            }

            ClosePreferencePopups();
        }

        private void OnMainWindowPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && ClosePreferencePopups())
            {
                e.Handled = true;
            }
        }

        private void OnPreferenceListPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && ClosePreferencePopups())
            {
                e.Handled = true;
            }
        }

        private IntPtr OnPreferencePopupWindowMessage(
            IntPtr hwnd,
            int msg,
            IntPtr wParam,
            IntPtr lParam,
            ref bool handled)
        {
            if (msg is WmEnterSizeMove or WmMoving or WmSizing)
            {
                ClosePreferencePopups();
            }

            return IntPtr.Zero;
        }

        private void ApplyPreferenceControlText()
        {
            UpdateThemeButton();
            UpdateLanguageButtons();
            UpdateAboutButton();
        }

        private void TogglePreferencePopup(Popup popup, ListBox listBox, Popup otherPopup)
        {
            bool shouldOpen = !popup.IsOpen;
            otherPopup.IsOpen = false;
            popup.IsOpen = shouldOpen;
            UpdateHeaderPreferenceActiveStates();

            if (!shouldOpen)
            {
                Keyboard.ClearFocus();
                return;
            }

            if (shouldOpen)
            {
                listBox.Focus();

                if (listBox.ItemContainerGenerator.ContainerFromItem(listBox.SelectedItem) is ListBoxItem item)
                {
                    item.Focus();
                    Keyboard.Focus(item);
                }
            }
        }

        private bool ClosePreferencePopups()
        {
            bool wasOpen = ThemePopup.IsOpen || LanguagePopup.IsOpen;
            ThemePopup.IsOpen = false;
            LanguagePopup.IsOpen = false;
            UpdateHeaderPreferenceActiveStates();
            return wasOpen;
        }

        private void UpdateHeaderPreferenceActiveStates()
        {
            ThemeButton.IsActive = ThemePopup.IsOpen;
            LanguageButton.IsActive = LanguagePopup.IsOpen;
        }

        private static CustomPopupPlacement[] PlaceHeaderPreferencePopup(
            Size popupSize,
            Size targetSize,
            Point offset)
        {
            return new[]
            {
                new CustomPopupPlacement(
                    new Point(targetSize.Width - popupSize.Width, targetSize.Height + HeaderPreferencePopupGap),
                    PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(
                    new Point(0, targetSize.Height + HeaderPreferencePopupGap),
                    PopupPrimaryAxis.Horizontal),
            };
        }

        private static bool IsWithin(DependencyObject? source, DependencyObject target)
        {
            while (source is not null)
            {
                if (ReferenceEquals(source, target))
                {
                    return true;
                }

                source = source is Visual or Visual3D
                    ? VisualTreeHelper.GetParent(source)
                    : LogicalTreeHelper.GetParent(source);
            }

            return false;
        }

        private void UpdateThemeButton()
        {
            string themeText = BuildThemeItems()
                .FirstOrDefault(item => item.Tag == _themePreference)?.Display ?? T("ThemeSystem");
            string toolTip = string.Format(CultureInfo.CurrentCulture, T("ThemeButtonTooltip"), themeText);

            ThemeButton.ToolTip = toolTip;
            AutomationProperties.SetName(ThemeButton, toolTip);
        }

        private void UpdateLanguageButtons()
        {
            string languageText = BuildLanguageItems()
                .FirstOrDefault(item => item.Tag == _languagePreference)?.Display ?? T("LanguageAuto");
            string toolTip = string.Format(CultureInfo.CurrentCulture, T("LanguageButtonTooltip"), languageText);

            LanguageButton.ToolTip = toolTip;
            AutomationProperties.SetName(LanguageButton, toolTip);
        }

        private void UpdateAboutButton()
        {
            AboutButton.ToolTip = T("About");
            AutomationProperties.SetName(AboutButton, T("About"));
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color)) return;

            Dispatcher.Invoke(() =>
            {
                if (_themePreference == "system")
                    ApplyThemePreference();

                if (_languagePreference == "auto")
                {
                    string resolved = ResolveLanguage();
                    if (resolved != _currentLanguage)
                    {
                        _currentLanguage = resolved;
                        ApplyLanguage();
                    }
                }
            });
        }

        private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(ConfigureWindowSizing);
        }

        private void ApplyThemePreference()
        {
            bool dark = _themePreference switch
            {
                "dark" => true,
                "light" => false,
                _ => App.IsSystemDarkTheme(),
            };

            App.ApplyTheme(dark);
            App.ApplyWindowTitleBarTheme(this);
        }

        private string ResolveLanguage()
        {
            if (_languagePreference is "zh" or "en")
                return _languagePreference;

            string name = CultureInfo.CurrentUICulture.Name;
            return name.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh" : "en";
        }

        private void ApplyLanguage(bool rebuildCombos = true)
        {
            ApplyApplicationLanguageResources(_currentLanguage);

            if (rebuildCombos)
            {
                PopulatePreferenceCombos();
                PopulateComboBoxes();
            }

            HeaderSubtitleText.Text = T("HeaderSubtitle");
            ApplyPreferenceControlText();
            InputTitleText.Text = T("InputTitle");
            OpenInputButton.Content = T("OpenFolder");
            BrowseInputButton.Content = T("BrowseInput");
            OutputTitleText.Text = T("OutputTitle");
            OpenOutputButton.Content = T("OpenFolder");
            BrowseOutputButton.Content = T("BrowseOutput");
            StartTitleText.Text = T("StartTitle");
            StartButton.Content = T("StartButton");
            StopButton.Content = T("StopButton");
            SettingsSectionTitleText.Text = T("SettingsSection");
            ModelLabelText.Text = T("ModelLabel");
            ScaleLabelText.Text = T("ScaleLabel");
            FormatLabelText.Text = T("FormatLabel");
            UpdateAdvancedToggleText();
            UpdateLogToggleText();
            LogHeaderText.Text = T("LogHeader");
            ThreadsLabelText.Text = T("ThreadsLabel");
            GpuLabelText.Text = T("GpuLabel");
            TtaCheck.Content = T("Tta");
            HintText.Text = T("Hint");
            RenderStatusText();
            RenderProgressText();
            RefreshFolderSummaries();
        }
    }
}
