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
        private const string AnimeVideoModelTag = "realesr-animevideov3";
        private const int DefaultAnimeVideoScale = 2;
        private const double ScaleInfoPopupGap = 6;
        private static readonly int[] SupportedScales = { 2, 3, 4 };

        private readonly DispatcherTimer _scaleInfoPopupTimer = new()
        {
            Interval = TimeSpan.FromSeconds(5),
        };
        private bool _hasShownScaleInfoAutoPopup;

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
            string format = SelectedOutputFormat();
            string threads = SelectedTag(ThreadsCombo) ?? "0";
            string gpu = SelectedTag(GpuCombo) ?? string.Empty;
            NormalizeModelAndScaleSelection(ref model, ref scale);

            ModelCombo.SelectionChanged -= OnModelChanged;
            FormatCombo.SelectionChanged -= OnFormatChanged;
            _updatingSelections = true;

            SetComboItems(ModelCombo, new[]
            {
                new ComboItem("realesrgan-x4plus",       T("ModelPhoto")),
                new ComboItem("realesrgan-x4plus-anime", T("ModelAnime")),
                new ComboItem(AnimeVideoModelTag,        T("ModelVideo")),
            }, model);

            SetComboItems(ScaleCombo, BuildScaleItemsForModel(model), scale);
            SetComboItems(FormatCombo, BuildFormatItemsForModel(model), format);

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
            FormatCombo.SelectionChanged += OnFormatChanged;
            UpdateModelDependentText();
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

        private static bool IsAnimeVideoModel(string model)
            => string.Equals(model, AnimeVideoModelTag, StringComparison.Ordinal);

        private bool IsAnimeVideoModelSelected()
            => IsAnimeVideoModel(SelectedTag(ModelCombo) ?? string.Empty);

        private string SelectedOutputFormat()
            => SelectedTag(FormatCombo) ?? "png";

        private static bool TryGetConcreteAnimeVideoScale(string model, out int scale)
        {
            scale = model switch
            {
                "realesr-animevideov3-x2" => 2,
                "realesr-animevideov3-x3" => 3,
                "realesr-animevideov3-x4" => 4,
                _ => 0,
            };

            return scale != 0;
        }

        private static void NormalizeModelAndScaleSelection(ref string model, ref string scale)
        {
            if (TryGetConcreteAnimeVideoScale(model, out int animeVideoScale))
            {
                model = AnimeVideoModelTag;

                if (string.IsNullOrWhiteSpace(scale))
                {
                    scale = animeVideoScale.ToString(CultureInfo.InvariantCulture);
                }
            }
            else if (IsAnimeVideoModel(model) && string.IsNullOrWhiteSpace(scale))
            {
                scale = DefaultAnimeVideoScale.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static int DefaultScaleFor(string model) => model switch
        {
            "realesr-animevideov3-x2" => 2,
            "realesr-animevideov3-x3" => 3,
            _                         => 4,
        };

        private ComboItem[] BuildScaleItemsForModel(string model)
            => IsAnimeVideoModel(model)
                ? BuildExplicitScaleItems()
                : BuildScaleItems(DefaultScaleFor(model));

        private ComboItem[] BuildFormatItemsForModel(string model)
            => IsAnimeVideoModel(model)
                ? BuildFrameFormatItems()
                : BuildImageFormatItems();

        private ComboItem[] BuildImageFormatItems() => new[]
        {
            new ComboItem("png",  T("FormatPng")),
            new ComboItem("jpg",  T("FormatJpg")),
            new ComboItem("webp", T("FormatWebp")),
        };

        private ComboItem[] BuildFrameFormatItems() => new[]
        {
            new ComboItem("png",  T("FrameFormatPng")),
            new ComboItem("jpg",  T("FrameFormatJpg")),
            new ComboItem("webp", T("FrameFormatWebp")),
        };

        private ComboItem[] BuildExplicitScaleItems()
            => SupportedScales
                .Select(scale => new ComboItem(scale.ToString(CultureInfo.InvariantCulture), $"{scale}x"))
                .ToArray();

        private ComboItem[] BuildScaleItems(int defaultScale)
        {
            var items = new List<ComboItem>
            {
                new(string.Empty, string.Format(CultureInfo.CurrentCulture, T("ScaleAuto"), defaultScale)),
            };

            foreach (int scale in SupportedScales)
            {
                if (scale == defaultScale) continue;
                items.Add(new ComboItem(scale.ToString(CultureInfo.InvariantCulture), $"{scale}x"));
            }

            return items.ToArray();
        }

        private void OnModelChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelections || ModelCombo.SelectedItem is not ComboItem mi) return;
            string scale = DefaultScaleTagForModel(mi.Tag);
            string format = SelectedOutputFormat();
            _updatingSelections = true;
            SetComboItems(ScaleCombo, BuildScaleItemsForModel(mi.Tag), scale);
            SetComboItems(FormatCombo, BuildFormatItemsForModel(mi.Tag), format);
            _updatingSelections = false;
            UpdateModelDependentText();
            UpdateScaleInfoHint(showTransient: true);
            RefreshFolderSummaries();
        }

        private static string DefaultScaleTagForModel(string model)
            => IsAnimeVideoModel(model)
                ? DefaultAnimeVideoScale.ToString(CultureInfo.InvariantCulture)
                : string.Empty;

        private void OnFormatChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_updatingSelections)
            {
                return;
            }

            RefreshFolderSummaries();
        }

        private void UpdateModelDependentText()
        {
            bool animeVideo = IsAnimeVideoModelSelected();
            FormatLabelText.Text = T(animeVideo ? "FrameFormatLabel" : "FormatLabel");
            HintText.Text = T(animeVideo ? "HintAnimeVideo" : "Hint");
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

            if (showTransient && !_hasShownScaleInfoAutoPopup)
            {
                _hasShownScaleInfoAutoPopup = true;
                ShowScaleInfoPopup(transient: true);
            }
        }

        private bool IsNonNativeScaleSelected()
        {
            if (ModelCombo.SelectedItem is not ComboItem modelItem)
            {
                return false;
            }

            if (IsAnimeVideoModel(modelItem.Tag))
            {
                return false;
            }

            string? selectedScale = SelectedTag(ScaleCombo);
            return !string.IsNullOrWhiteSpace(selectedScale) &&
                int.TryParse(selectedScale, NumberStyles.None, CultureInfo.InvariantCulture, out int outputScale) &&
                outputScale != DefaultScaleFor(modelItem.Tag);
        }

        private static string ResolveBackendModel(string model, string scale)
        {
            if (!IsAnimeVideoModel(model))
            {
                return model;
            }

            int animeVideoScale = ResolveAnimeVideoScale(scale);
            return "realesr-animevideov3-x" + animeVideoScale.ToString(CultureInfo.InvariantCulture);
        }

        private static string ResolveBackendScale(string model, string scale)
            => IsAnimeVideoModel(model) ? string.Empty : scale;

        private static int ResolveAnimeVideoScale(string scale)
        {
            if (int.TryParse(scale, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) &&
                SupportedScales.Contains(parsed))
            {
                return parsed;
            }

            return DefaultAnimeVideoScale;
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
            UpdateModelDependentText();
            UpdateAdvancedToggleText();
            UpdateLogToggleText();
            LogHeaderText.Text = T("LogHeader");
            ThreadsLabelText.Text = T("ThreadsLabel");
            GpuLabelText.Text = T("GpuLabel");
            TtaCheck.Content = T("Tta");
            RenderStatusText();
            RenderProgressText();
            RefreshFolderSummaries();
            ScheduleContentHeightLimitRefresh();
        }
    }
}
