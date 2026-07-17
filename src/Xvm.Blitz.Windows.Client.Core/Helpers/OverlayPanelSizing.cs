namespace Xvm.Blitz.Windows.Client.Core.Helpers;

public static class OverlayPanelSizing
{
    public const double BaseFontSize = 11;

    public const double BasePanelWidth = 280;

    public const double BasePanelHeight = 220;

    public const double MinScaleX = 0.67;

    public const double MaxScaleX = 2;

    public const double MinScaleY = 0.25;

    public const double MaxScaleY = 2;

    private const double MinFontScale = 0.75;

    public static double CoerceScaleX(double scale) => Math.Clamp(scale, MinScaleX, MaxScaleX);

    public static double CoerceScaleY(double scale) => Math.Clamp(scale, MinScaleY, MaxScaleY);

    public static double FontSize(double scaleY)
    {
        var coerced = CoerceScaleY(scaleY);
        if (coerced >= 1)
            return BaseFontSize * coerced;

        var progress = Math.Clamp((coerced - MinScaleY) / (1 - MinScaleY), 0, 1);
        var fontScale = MinFontScale + (1 - MinFontScale) * progress;
        return BaseFontSize * fontScale;
    }

    public static double FontScale(double scaleY) => FontSize(scaleY) / BaseFontSize;

    public static double PanelMinWidth(double scaleX, double scaleY) =>
        BasePanelWidth * CoerceScaleX(scaleX) * FontScale(scaleY);

    public static double ScaleXFromWidthDelta(double initialScaleX, double initialScaleY, double widthDelta)
    {
        var startWidth = PanelMinWidth(initialScaleX, initialScaleY);
        var newWidth = Math.Max(1, startWidth + widthDelta);
        return CoerceScaleX(newWidth / (BasePanelWidth * FontScale(initialScaleY)));
    }

    public static double ScaleYFromHeightDelta(double initialScaleY, double heightDelta) =>
        CoerceScaleY((BasePanelHeight * initialScaleY + heightDelta) / BasePanelHeight);
}
