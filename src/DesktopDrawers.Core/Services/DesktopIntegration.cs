namespace DesktopDrawers.Core.Services;

public enum DesktopMode { Compatibility, Integrated }

// Native checks detect structural failures; they cannot prove that DWM rendered pixels.
public sealed record DesktopProbe
{
    public bool ParentValid { get; init; } = true;
    public bool ParentMatches { get; init; } = true;
    public bool AncestorsVisible { get; init; } = true;
    public bool WpfVisible { get; init; } = true;
    public bool NativeVisible { get; init; } = true;
    public bool SizeValid { get; init; } = true;
    public bool OnScreen { get; init; } = true;
    public bool WithinParent { get; init; } = true;
    public bool ChildStyle { get; init; } = true;
    public bool PopupStyle { get; init; }
    public bool TopmostStyle { get; init; }
    public bool Cloaked { get; init; }
    public bool SurfaceOnTop { get; init; } = true;
    public bool ExplorerLayerAbove { get; init; }
    public string[] Failures(bool expectedVisible)
    {
        var failures = new List<string>();
        if (!ParentValid || !ParentMatches) failures.Add("Padre inválido o cambiado");
        if (!AncestorsVisible || Cloaked) failures.Add("Capa de Explorer oculta o cloaked");
        if (!WpfVisible || (expectedVisible && !NativeVisible)) failures.Add("Ventana no visible");
        if (!SizeValid || !OnScreen || !WithinParent) failures.Add("Tamaño o coordenadas fuera de la superficie visible");
        if (!ChildStyle || PopupStyle || TopmostStyle) failures.Add("Estilos incompatibles");
        if (expectedVisible && (!SurfaceOnTop || ExplorerLayerAbove)) failures.Add("Otra capa de Explorer cubre el cajón");
        return failures.ToArray();
    }
}

// One automatic integrated trial per explicit request. Failures latch compatibility;
// shell events never re-enable integration or reset this latch.
public sealed class IntegrationSession(DesktopMode mode)
{
    public DesktopMode Mode { get; private set; } = mode;
    public bool FallbackUsed { get; private set; }
    public void Select(DesktopMode mode) { Mode = mode; FallbackUsed = false; }
    public void FailIntegrated()
    {
        if (Mode == DesktopMode.Integrated) { Mode = DesktopMode.Compatibility; FallbackUsed = true; }
    }
}
