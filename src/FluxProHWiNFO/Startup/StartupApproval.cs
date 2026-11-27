namespace FluxProHWiNFO.Startup;

/// <summary>
/// Task Manager's per-entry Enabled/Disabled state, stored as a 12-byte binary value under
/// <c>...\Explorer\StartupApproved\Run</c>. Observed on Windows 11: enabled = <c>02</c> and 11 zero bytes;
/// disabled = an odd first byte (<c>01</c>, <c>03</c>, <c>07</c>), then 3 zero bytes and the FILETIME it was disabled.
/// </summary>
public static class StartupApproval
{
    /// <summary>The value Task Manager writes when an entry is enabled.</summary>
    public static byte[] Enabled => [0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>
    /// True unless Task Manager marked the entry disabled. A missing value counts as enabled, as it does for
    /// Windows: most apps never write one, and their Run entries still start at sign-in.
    /// </summary>
    public static bool IsEnabled(byte[]? value) => value is null || (value.Length > 0 && (value[0] & 1) == 0);
}
