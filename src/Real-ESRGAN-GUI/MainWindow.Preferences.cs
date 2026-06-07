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
using System.Windows.Threading;
using Microsoft.Win32;

namespace RealESRGAN_GUI
{
    public partial class MainWindow
    {
        private const double ScaleInfoPopupGap = 6;

        private readonly DispatcherTimer _scaleInfoPopupTimer = new()
        {
            Interval = TimeSpan.FromSeconds(5),
        };

        public static CustomPopupPlacementCallback ScaleInfoPopupPlacementCallback { get; } = PlaceScaleInfoPopup;

        private static CustomPopupPlacement[] PlaceScaleInfoPopup(Size popupSize, Size targetSize, Point offset)
        {
            double centeredX = (targetSize.Width - popupSize.Width) / 2 + offset.X;
            double topY = -popupSize.Height - ScaleInfoPopupGap + offset.Y;
            double rightAlignedX = targetSize.Width - popupSize.Width + offset.X;
            double belowY = targetSize.Height + ScaleInfoPopupGap + offset.Y;

            return new[]
            {
                new CustomPopupPlacement(new Point(centeredX, topY), PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(new Point(rightAlignedX, topY), PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(new Point(centeredX, belowY), PopupPrimaryAxis.Horizontal),
            };
        }

        private void ConfigureHeaderActions()
        {
            AboutButton.Click += OnAboutClick;
            ConfigureScaleInfoHint();
        }

        private void PopulatePreferenceCombos()
        {
            _updatingSelections = true;
            SetComboItems(ThemeCombo, BuildThemeItems(), _themePreference);
            SetComboItems(LanguageCombo, BuildLanguageItems(), _languagePreference);
            _updatingSelections = false;
            UpdateThemeCombo();
            UpdateLanguageCombo();
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
            UpdateScaleInfoHint(showTransient: false);
        }

        private static void SetComboItems(ComboBox combo, ComboItem[] items, string selectedTag)
        {
            combo.ItemsSource = items;
            combo.DisplayMemberPath = nameof(ComboItem.Display);
            combo.SelectedItem = items.FirstOrDefault(item => item.Tag == selectedTag) ?? items[0];
        }

        private static string? SelectedTag(ComboBox combo)
            => combo.SelectedItem is ComboItem item ? item.Tag : null;

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
            UpdateScaleInfoHint(showTransient: true);
        }

        private void ConfigureScaleInfoHint()
        {
            ScaleCombo.SelectionChanged += OnScaleChanged;
            ScaleInfoIcon.MouseEnter += OnScaleInfoIconMouseEnter;
            ScaleInfoIcon.MouseLeave += OnScaleInfoIconMouseLeave;
            _scaleInfoPopupTimer.Tick += OnScaleInfoPopupTimerTick;
            Deactivated += (_, _) => CloseScaleInfoPopup();
            LocationChanged += (_, _) => CloseScaleInfoPopup();
            SizeChanged += (_, _) => CloseScaleInfoPopup();
            StateChanged += (_, _) => CloseScaleInfoPopup();
            Closed += (_, _) => CloseScaleInfoPopup();
            UpdateScaleInfoHintText();
        }

        private void OnScaleChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelections)
            {
                return;
            }

            UpdateScaleInfoHint(showTransient: true);
        }

        private void UpdateScaleInfoHint(bool showTransient)
        {
            bool isNonNative = IsNonNativeScaleSelected();
            ScaleInfoIcon.Visibility = isNonNative ? Visibility.Visible : Visibility.Collapsed;

            if (!isNonNative)
            {
                CloseScaleInfoPopup();
                return;
            }

            UpdateScaleInfoHintText();

            if (showTransient)
            {
                ShowScaleInfoPopup(transient: true);
            }
        }

        private bool IsNonNativeScaleSelected()
        {
            if (ModelCombo.SelectedItem is not ComboItem modelItem)
            {
                return false;
            }

            string? selectedScale = SelectedTag(ScaleCombo);
            return !string.IsNullOrWhiteSpace(selectedScale) &&
                int.TryParse(selectedScale, NumberStyles.None, CultureInfo.InvariantCulture, out int outputScale) &&
                outputScale != DefaultScaleFor(modelItem.Tag);
        }

        private void UpdateScaleInfoHintText()
        {
            string helpText = T("ScaleInfoHelp");
            ScaleInfoPopupText.Text = helpText;
            AutomationProperties.SetName(ScaleInfoIcon, T("ScaleInfoAutomationName"));
            AutomationProperties.SetHelpText(ScaleInfoIcon, helpText);
        }

        private void ShowScaleInfoPopup(bool transient)
        {
            if (ScaleInfoIcon.Visibility != Visibility.Visible)
            {
                return;
            }

            UpdateScaleInfoHintText();
            ScaleInfoPopup.IsOpen = true;
            _scaleInfoPopupTimer.Stop();

            if (transient)
            {
                _scaleInfoPopupTimer.Start();
            }
        }

        private void CloseScaleInfoPopup()
        {
            _scaleInfoPopupTimer.Stop();
            ScaleInfoPopup.IsOpen = false;
        }

        private void OnScaleInfoIconMouseEnter(object sender, MouseEventArgs e)
        {
            ShowScaleInfoPopup(transient: false);
        }

        private void OnScaleInfoIconMouseLeave(object sender, MouseEventArgs e)
        {
            CloseScaleInfoPopup();
        }

        private void OnScaleInfoPopupTimerTick(object? sender, EventArgs e)
        {
            if (ScaleInfoIcon.IsMouseOver)
            {
                _scaleInfoPopupTimer.Stop();
                return;
            }

            CloseScaleInfoPopup();
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

        private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelections)
            {
                return;
            }

            string? selectedTag = SelectedTag(ThemeCombo);
            if (selectedTag is null ||
                string.Equals(selectedTag, _themePreference, StringComparison.Ordinal))
            {
                return;
            }

            _themePreference = selectedTag;
            ApplyThemePreference();
            UpdateThemeCombo();
        }

        private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelections)
            {
                return;
            }

            string? selectedTag = SelectedTag(LanguageCombo);
            if (selectedTag is null ||
                string.Equals(selectedTag, _languagePreference, StringComparison.Ordinal))
            {
                return;
            }

            _languagePreference = selectedTag;
            _currentLanguage = ResolveLanguage();
            ApplyLanguage();
        }

        private void ApplyPreferenceControlText()
        {
            UpdateThemeCombo();
            UpdateLanguageCombo();
            UpdateAboutButton();
        }

        private void UpdateThemeCombo()
        {
            string themeText = BuildThemeItems()
                .FirstOrDefault(item => item.Tag == _themePreference)?.Display ?? T("ThemeSystem");
            string toolTip = string.Format(CultureInfo.CurrentCulture, T("ThemeButtonTooltip"), themeText);

            ThemeCombo.ToolTip = toolTip;
            AutomationProperties.SetName(ThemeCombo, toolTip);
        }

        private void UpdateLanguageCombo()
        {
            string languageText = BuildLanguageItems()
                .FirstOrDefault(item => item.Tag == _languagePreference)?.Display ?? T("LanguageAuto");
            string toolTip = string.Format(CultureInfo.CurrentCulture, T("LanguageButtonTooltip"), languageText);

            LanguageCombo.ToolTip = toolTip;
            AutomationProperties.SetName(LanguageCombo, toolTip);
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
            App.ApplyWindowTitleBarTheme(this, hideTitleText: true);
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
            UpdateScaleInfoHint(showTransient: false);
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
            ScheduleContentHeightLimitRefresh();
        }
    }
}
