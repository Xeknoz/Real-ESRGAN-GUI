namespace RealESRGAN_GUI
{
    internal sealed record ComboItem(string Tag, string Display)
    {
        public override string ToString() => Display;
    }
}
