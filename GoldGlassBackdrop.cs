using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace TailscaleCapsule;

// Uses the system's real desktop blur; high contrast and disabled transparency
// are respected by DesktopAcrylicController's native fallback.
public sealed class GoldGlassBackdrop : SystemBackdrop
{
    readonly Color accent;
    public GoldGlassBackdrop(Color accent) { this.accent = accent; }
    DesktopAcrylicController? controller;
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(target, xamlRoot);
        if (!DesktopAcrylicController.IsSupported()) return;
        controller = new DesktopAcrylicController
        {
            TintColor = Color.FromArgb(255, (byte)(accent.R * .52 + 20), (byte)(accent.G * .52 + 20), (byte)(accent.B * .52 + 20)),
            TintOpacity = .60f,
            LuminosityOpacity = .60f,
            FallbackColor = Color.FromArgb(255, (byte)(accent.R * .22 + 20), (byte)(accent.G * .22 + 20), (byte)(accent.B * .22 + 20))
        };
        controller.SetSystemBackdropConfiguration(GetDefaultSystemBackdropConfiguration(target, xamlRoot));
        controller.AddSystemBackdropTarget(target);
    }
    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        controller?.RemoveSystemBackdropTarget(target); controller?.Dispose(); controller = null;
        base.OnTargetDisconnected(target);
    }
}
