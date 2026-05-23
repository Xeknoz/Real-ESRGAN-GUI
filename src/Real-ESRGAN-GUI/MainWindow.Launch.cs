using RealESRGAN_GUI.Services;

namespace RealESRGAN_GUI
{
    public partial class MainWindow
    {
        private void PrepareLauncherHandoff()
        {
            WindowFirstPaintGate.PrepareForFirstPaint(this);
            WindowFirstPaintGate.MarkLauncherReadyWhenStable(this);
        }
    }
}
