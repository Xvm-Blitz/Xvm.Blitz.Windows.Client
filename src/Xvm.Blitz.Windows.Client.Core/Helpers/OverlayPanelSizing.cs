namespace Xvm.Blitz.Windows.Client.Core.Helpers;

public static class OverlayPanelSizing
{
    public const double BaseFontSize = 11;

    public const double BasePanelWidth = 280;

    public const double BasePanelHeight = 220;

    public const double BaseSessionOverlayFontSize = 12;

    public const double BaseSessionOverlayPaddingHorizontal = 10;

    public const double BaseSessionOverlayPaddingVertical = 6;

    public const double BaseSessionOverlaySpacing = 10;

    public const double BaseSessionOverlayWidth = 220;

    public const double BaseSessionOverlayHeight = 36;

    public const double BaseVoiceOverlayTitleFontSize = 13;

    public const double BaseVoiceOverlayFontSize = 12;

    public const double BaseVoiceOverlayPaddingHorizontal = 10;

    public const double BaseVoiceOverlayPaddingVertical = 8;

    public const double BaseVoiceOverlaySpacing = 6;

    public const double BaseVoiceOverlayWidth = 260;

    public const double BaseVoiceOverlayHeight = 120;

    public const double MinScaleX = 0.67;

    public const double MaxScaleX = 2;

    public const double PanelMaxScaleX = 8;

    public const double MinScaleY = 0.25;

    public const double MaxScaleY = 2;

    private const double MinFontScale = 0.75;

    public static double CoerceScaleX(double scale) => Math.Clamp(scale, MinScaleX, MaxScaleX);

    public static double CoercePanelScaleX(double scale) => Math.Clamp(scale, MinScaleX, PanelMaxScaleX);

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
        BasePanelWidth * CoercePanelScaleX(scaleX) * FontScale(scaleY);

    public static double ScaleXFromWidthDelta(double initialScaleX, double initialScaleY, double widthDelta)
    {
        var startWidth = PanelMinWidth(initialScaleX, initialScaleY);
        var newWidth = Math.Max(1, startWidth + widthDelta);
        return CoercePanelScaleX(newWidth / (BasePanelWidth * FontScale(initialScaleY)));
    }

    public static double ScaleYFromHeightDelta(double initialScaleY, double heightDelta) =>
        CoerceScaleY((BasePanelHeight * initialScaleY + heightDelta) / BasePanelHeight);

    public static double SessionOverlayFontSize(double scaleY) => BaseSessionOverlayFontSize * FontScale(scaleY);

    public static (double Horizontal, double Vertical) SessionOverlayPadding(double scaleX, double scaleY)
    {
        var fontScale = FontScale(scaleY);
        return (
            BaseSessionOverlayPaddingHorizontal * CoerceScaleX(scaleX) * fontScale,
            BaseSessionOverlayPaddingVertical * fontScale);
    }

    public static double SessionOverlaySpacing(double scaleX, double scaleY) =>
        BaseSessionOverlaySpacing * CoerceScaleX(scaleX) * FontScale(scaleY);

    public static double SessionOverlayScaleXFromWidthDelta(double initialScaleX, double initialScaleY, double widthDelta)
    {
        var startWidth = BaseSessionOverlayWidth * CoerceScaleX(initialScaleX) * FontScale(initialScaleY);
        var newWidth = Math.Max(1, startWidth + widthDelta);
        return CoerceScaleX(newWidth / (BaseSessionOverlayWidth * FontScale(initialScaleY)));
    }

    public static double SessionOverlayScaleYFromHeightDelta(double initialScaleY, double heightDelta) =>
        CoerceScaleY((BaseSessionOverlayHeight * initialScaleY + heightDelta) / BaseSessionOverlayHeight);

    public static double SessionOverlayMinWidth(double scaleX, double scaleY) =>
        BaseSessionOverlayWidth * CoerceScaleX(scaleX) * FontScale(scaleY);

    public static double SessionOverlayMinHeight(double scaleY) =>
        BaseSessionOverlayHeight * CoerceScaleY(scaleY);

    public static double VoiceOverlayTitleFontSize(double scaleY) => BaseVoiceOverlayTitleFontSize * FontScale(scaleY);

    public static double VoiceOverlayFontSize(double scaleY) => BaseVoiceOverlayFontSize * FontScale(scaleY);

    public static (double Horizontal, double Vertical) VoiceOverlayPadding(double scaleX, double scaleY)
    {
        var fontScale = FontScale(scaleY);
        return (
            BaseVoiceOverlayPaddingHorizontal * CoerceScaleX(scaleX) * fontScale,
            BaseVoiceOverlayPaddingVertical * fontScale);
    }

    public static double VoiceOverlaySpacing(double scaleX, double scaleY) =>
        BaseVoiceOverlaySpacing * CoerceScaleX(scaleX) * FontScale(scaleY);

    public static double VoiceOverlayMinWidth(double scaleX, double scaleY) =>
        BaseVoiceOverlayWidth * CoerceScaleX(scaleX) * FontScale(scaleY);

    public static double VoiceOverlayMinHeight(double scaleY) =>
        BaseVoiceOverlayHeight * CoerceScaleY(scaleY);

    public static double VoiceOverlayScaleXFromWidthDelta(double initialScaleX, double initialScaleY, double widthDelta)
    {
        var startWidth = BaseVoiceOverlayWidth * CoerceScaleX(initialScaleX) * FontScale(initialScaleY);
        var newWidth = Math.Max(1, startWidth + widthDelta);
        return CoerceScaleX(newWidth / (BaseVoiceOverlayWidth * FontScale(initialScaleY)));
    }

    public static double VoiceOverlayScaleYFromHeightDelta(double initialScaleY, double heightDelta) =>
        CoerceScaleY((BaseVoiceOverlayHeight * initialScaleY + heightDelta) / BaseVoiceOverlayHeight);
}
