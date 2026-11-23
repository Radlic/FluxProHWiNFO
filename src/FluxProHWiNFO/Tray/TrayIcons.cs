using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace FluxProHWiNFO.Tray;

internal enum TraySeverity
{
    /// <summary>Display connected, HWiNFO running, both values found.</summary>
    Ok,
    /// <summary>Display connected but something is missing (HWiNFO, a sensor, bad config).</summary>
    Warning,
    /// <summary>Display not connected.</summary>
    Error,
}

/// <summary>Thermometer tray icons drawn at runtime, one colour per state.</summary>
internal sealed partial class TrayIcons : IDisposable
{
    public Icon Ok { get; } = Make(Color.FromArgb(0x1E, 0x88, 0xE5));
    public Icon Warning { get; } = Make(Color.FromArgb(0xF5, 0x9E, 0x0B));
    public Icon Error { get; } = Make(Color.FromArgb(0x75, 0x75, 0x75));

    public Icon For(TraySeverity severity) => severity switch
    {
        TraySeverity.Ok => Ok,
        TraySeverity.Warning => Warning,
        _ => Error,
    };

    public void Dispose()
    {
        Ok.Dispose();
        Warning.Dispose();
        Error.Dispose();
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint handle);

    private static Icon Make(Color color)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        using (var fill = new SolidBrush(color))
        using (var white = new SolidBrush(Color.White))
        using (var stem = RoundedRect(new RectangleF(12f, 3f, 8f, 20f), 4f))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            g.FillEllipse(fill, 0, 0, 31, 31);

            // White thermometer outline: stem plus bulb.
            g.FillPath(white, stem);
            g.FillEllipse(white, 9f, 16f, 14f, 14f);

            // Coloured "mercury" inside it.
            g.FillRectangle(fill, 14.5f, 9f, 3f, 12f);
            g.FillEllipse(fill, 12f, 19f, 8f, 8f);
        }

        nint handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone(); // owns a copy of the handle
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
