using Microsoft.Win32;

namespace FluxProHWiNFO.HWiNFO;

/// <summary>Source of raw VSB registry values. Abstracted so the parser and matcher can be tested without HWiNFO.</summary>
public interface IVsbSource
{
    /// <summary>All value names and string data under the VSB key, or null if the key doesn't exist.</summary>
    IReadOnlyList<KeyValuePair<string, string?>>? ReadValues();
}

/// <summary>Reads <c>HKEY_CURRENT_USER\Software\HWiNFO64\VSB</c>.</summary>
/// <remarks>HKCU is per user: HWiNFO and this app must run as the same Windows user.</remarks>
public sealed class RegistryVsbSource : IVsbSource
{
    public const string KeyPath = @"Software\HWiNFO64\VSB";
    public const string DisplayPath = @"HKCU\" + KeyPath;

    public IReadOnlyList<KeyValuePair<string, string?>>? ReadValues()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        if (key is null)
            return null;

        var result = new List<KeyValuePair<string, string?>>();
        foreach (string name in key.GetValueNames())
        {
            // HWiNFO may delete a value between GetValueNames and GetValue; that just yields null.
            result.Add(new(name, key.GetValue(name)?.ToString()));
        }
        return result;
    }
}

public sealed class VsbReader(IVsbSource source)
{
    public VsbReader() : this(new RegistryVsbSource()) { }

    public VsbSnapshot Read()
    {
        var values = source.ReadValues();
        return values is null ? VsbSnapshot.NotFound : new VsbSnapshot(true, VsbParser.Parse(values));
    }
}
