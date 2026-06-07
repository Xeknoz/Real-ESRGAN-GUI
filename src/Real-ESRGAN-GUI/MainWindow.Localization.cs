namespace RealESRGAN_GUI
{
    public partial class MainWindow
    {
        private static readonly string[] ApplicationResourceTextKeys =
        {
        };

        private string T(string key)
        {
            return TextForLanguage(_currentLanguage, key);
        }

        private static void ApplyApplicationLanguageResources(string language)
        {
            if (System.Windows.Application.Current is not { } application)
            {
                return;
            }

            foreach (string key in ApplicationResourceTextKeys)
            {
                application.Resources[key] = TextForLanguage(language, key);
            }
        }

        internal static string TextForLanguage(string language, string key)
        {
            bool en = language == "en";
            return en ? EnglishText(key) : ChineseText(key);
        }

        private static string ChineseText(string key) => key switch
        {
            "NoticeTitle" => "提示",
            "NoticeAlreadyRunning" => "Real-ESRGAN GUI 已经在运行中。",
            "NoticeOk" => "确定",
            "LaunchFailedTitle" => "启动失败",
            "LauncherMissing" => "无法找到 Launcher.exe。",
            "HeaderSubtitle" => "图片清晰化工作台",
            "ThemeSystem" => "跟随系统",
            "ThemeLight" => "浅色",
            "ThemeDark" => "深色",
            "ThemeButtonTooltip" => "主题：{0}",
            "LanguageAuto" => "自动识别",
            "LanguageZh" => "简体中文",
            "LanguageEn" => "English",
            "LanguageButtonTooltip" => "语言：{0}",
            "About" => "关于",
            "AboutTitle" => "关于 Real-ESRGAN GUI",
            "AboutDescription" => "用于本地图片清晰化的桌面工具。",
            "VersionLabel" => "当前版本",
            "LicenseSection" => "许可证",
            "LicenseMissing" => "未找到许可证文件。",
            "OpenRepository" => "打开 GitHub 仓库",
            "OpenRepositoryFailed" => "无法打开 GitHub 仓库链接。",
            "Close" => "关闭",
            "InputTitle" => "图片来源",
            "OutputTitle" => "保存位置",
            "OpenFolder" => "打开文件夹",
            "BrowseInput" => "选择图片文件夹",
            "BrowseOutput" => "选择保存文件夹",
            "StartTitle" => "开始处理",
            "StartButton" => "开始清晰化",
            "StopButton" => "停止",
            "SettingsSection" => "处理方式",
            "ModelLabel" => "图片类型",
            "ScaleLabel" => "放大倍数",
            "FormatLabel" => "保存格式",
            "Advanced" => "高级设置",
            "Log" => "日志",
            "LogHeader" => "输出日志",
            "ThreadsLabel" => "线程数",
            "GpuLabel" => "GPU 设备",
            "Tta" => "质量增强（较慢）",
            "Hint" => "请先根据图片内容选择类型，模型不匹配可能影响效果；其余设置保持默认即可。",
            "ModelPhoto" => "照片 / 人像",
            "ModelAnime" => "动漫 / 插画",
            "ModelVideo2" => "动漫视频（2x）",
            "ModelVideo3" => "动漫视频（3x）",
            "ModelVideo4" => "动漫视频（4x）",
            "ScaleAuto" => "模型默认（{0}x）",
            "ScaleInfoAutomationName" => "非原生倍率说明",
            "ScaleInfoHelp" => "选择非原生倍率时，程序会先按模型默认倍率处理，再调整到你选择的尺寸。",
            "FormatPng" => "PNG（清晰，推荐）",
            "FormatJpg" => "JPG（文件更小）",
            "FormatWebp" => "WebP（网页友好）",
            "AutoRecommended" => "自动（推荐）",
            "PickFolderTitle" => "选择文件夹",
            "MissingExe" => "找不到主程序：\n{0}\n\n请确保 realesrgan-ncnn-vulkan.exe 在 engine 目录内。",
            "InputAccessError" => "无法创建/访问输入文件夹。",
            "OutputAccessError" => "无法创建/访问输出文件夹。",
            "NoImagesFound" => "输入文件夹中没有支持的图片 (png/jpg/jpeg/bmp/webp/tif/tiff)。\n请添加图片后再开始处理。",
            "InputSummaryNone" => "尚未选择文件夹",
            "OutputSummaryNone" => "尚未选择保存位置",
            "FolderCreateOnStart" => "开始时会自动创建文件夹",
            "FolderUnreadable" => "无法读取文件夹内容",
            "InputNoImages" => "未发现支持的图片",
            "OutputNoFiles" => "暂无输出结果",
            "InputCount" => "发现 {0} 张可处理图片",
            "OutputCount" => "已有 {0} 个结果文件",
            "StatusReady" => "就绪",
            "StatusStarting" => "正在准备处理",
            "StatusProcessing" => "正在处理...",
            "StatusProcessingFiles" => "未完成 {1} 个，已完成 {0} 个",
            "StatusStopped" => "已停止",
            "StatusStoppedFinal" => "已停止，已完成 {0}/{1} 个文件",
            "StatusDone" => "完成，输出 {0} 个文件",
            "StatusFailed" => "失败 (代码 {0})",
            "StatusPartial" => "部分完成，已处理 {0}/{1} 个文件",
            "StatusError" => "错误: {0}",
            "CurrentFileProgress" => "当前文件 {0}/{1}：{2:0.00}%",
            "ProgressZero" => "0%",
            "ProgressPreparing" => "准备中",
            "ProgressStopped" => "已停止",
            "ProgressIncomplete" => "未完成",
            "ProgressError" => "出错",
            _ => key,
        };

        private static string EnglishText(string key) => key switch
        {
            "NoticeTitle" => "Notice",
            "NoticeAlreadyRunning" => "Real-ESRGAN GUI is already running.",
            "NoticeOk" => "OK",
            "LaunchFailedTitle" => "Launch Failed",
            "LauncherMissing" => "Launcher.exe could not be found.",
            "HeaderSubtitle" => "Image upscaling workspace",
            "ThemeSystem" => "System",
            "ThemeLight" => "Light",
            "ThemeDark" => "Dark",
            "ThemeButtonTooltip" => "Theme: {0}",
            "LanguageAuto" => "Auto",
            "LanguageZh" => "简体中文",
            "LanguageEn" => "English",
            "LanguageButtonTooltip" => "Language: {0}",
            "About" => "About",
            "AboutTitle" => "About Real-ESRGAN GUI",
            "AboutDescription" => "A desktop tool for local image upscaling.",
            "VersionLabel" => "Version",
            "LicenseSection" => "Licenses",
            "LicenseMissing" => "No license files were found.",
            "OpenRepository" => "Open GitHub repository",
            "OpenRepositoryFailed" => "Could not open the GitHub repository link.",
            "Close" => "Close",
            "InputTitle" => "Image source",
            "OutputTitle" => "Save to",
            "OpenFolder" => "Open folder",
            "BrowseInput" => "Choose image folder",
            "BrowseOutput" => "Choose output folder",
            "StartTitle" => "Run",
            "StartButton" => "Start upscaling",
            "StopButton" => "Stop",
            "SettingsSection" => "Processing",
            "ModelLabel" => "Image type",
            "ScaleLabel" => "Scale",
            "FormatLabel" => "Format",
            "Advanced" => "Advanced settings",
            "Log" => "Log",
            "LogHeader" => "Output log",
            "ThreadsLabel" => "Threads",
            "GpuLabel" => "GPU device",
            "Tta" => "Enhanced quality (slower)",
            "Hint" => "Choose the image type first. A mismatched model can reduce quality; the other defaults are usually fine.",
            "ModelPhoto" => "Photo / portrait",
            "ModelAnime" => "Anime / illustration",
            "ModelVideo2" => "Anime video (2x)",
            "ModelVideo3" => "Anime video (3x)",
            "ModelVideo4" => "Anime video (4x)",
            "ScaleAuto" => "Model default ({0}x)",
            "ScaleInfoAutomationName" => "Non-native scale details",
            "ScaleInfoHelp" => "When you choose a non-native scale, the app processes at the model default first, then adjusts the result to your selected size.",
            "FormatPng" => "PNG (clear, recommended)",
            "FormatJpg" => "JPG (smaller files)",
            "FormatWebp" => "WebP (web friendly)",
            "AutoRecommended" => "Auto (recommended)",
            "PickFolderTitle" => "Choose a folder",
            "MissingExe" => "Main program not found:\n{0}\n\nPlease make sure realesrgan-ncnn-vulkan.exe is in the engine directory.",
            "InputAccessError" => "Cannot create or access the input folder.",
            "OutputAccessError" => "Cannot create or access the output folder.",
            "NoImagesFound" => "No supported images were found in the input folder (png/jpg/jpeg/bmp/webp/tif/tiff).\nAdd images before starting.",
            "InputSummaryNone" => "No folder selected",
            "OutputSummaryNone" => "No output folder selected",
            "FolderCreateOnStart" => "The folder will be created when processing starts",
            "FolderUnreadable" => "Cannot read this folder",
            "InputNoImages" => "No supported images found",
            "OutputNoFiles" => "No output files yet",
            "InputCount" => "{0} supported images found",
            "OutputCount" => "{0} result files already here",
            "StatusReady" => "Ready",
            "StatusStarting" => "Preparing to process",
            "StatusProcessing" => "Processing...",
            "StatusProcessingFiles" => "Remaining {1}, completed {0}",
            "StatusStopped" => "Stopped",
            "StatusStoppedFinal" => "Stopped, {0}/{1} files completed",
            "StatusDone" => "Done, exported {0} files",
            "StatusFailed" => "Failed (code {0})",
            "StatusPartial" => "Partial, {0}/{1} files completed",
            "StatusError" => "Error: {0}",
            "CurrentFileProgress" => "Current file {0}/{1}: {2:0.00}%",
            "ProgressZero" => "0%",
            "ProgressPreparing" => "Preparing",
            "ProgressStopped" => "Stopped",
            "ProgressIncomplete" => "Incomplete",
            "ProgressError" => "Error",
            _ => key,
        };
    }
}
