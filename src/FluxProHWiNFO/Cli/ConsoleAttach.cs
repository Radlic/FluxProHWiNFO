using System.Runtime.InteropServices;
using System.Text;

namespace FluxProHWiNFO.Cli;

/// <summary>
/// The app is a WinExe (no console window at logon), so CLI modes borrow the parent's console.
/// If stdout is already redirected (e.g. piped in PowerShell) the inherited handle is kept, but we still
/// attach to the parent console to learn its code page, so characters like "°" decode correctly.
/// </summary>
internal static partial class ConsoleAttach
{
    private const int AttachParentProcess = -1;
    private const int StdOutputHandle = -11;
    private const int StdErrorHandle = -12;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int processId);

    [LibraryImport("kernel32.dll")]
    private static partial nint GetStdHandle(int stdHandle);

    [LibraryImport("kernel32.dll")]
    private static partial uint GetConsoleOutputCP();

    private static bool IsValid(nint handle) => handle != 0 && handle != -1;

    public static void Ensure()
    {
        // Grab any redirected handles before attaching, which may replace the std handles.
        Stream? redirectedOut = IsValid(GetStdHandle(StdOutputHandle)) ? Console.OpenStandardOutput() : null;
        Stream? redirectedErr = IsValid(GetStdHandle(StdErrorHandle)) ? Console.OpenStandardError() : null;

        bool attached = AttachConsole(AttachParentProcess);

        // Console.OutputEncoding is unreliable in a WinExe (it reports code page 0), so ask Windows directly.
        Encoding encoding = EncodingForCodePage(attached ? GetConsoleOutputCP() : 0);

        Stream? outStream = redirectedOut ?? (attached ? Console.OpenStandardOutput() : null);
        Stream? errStream = redirectedErr ?? (attached ? Console.OpenStandardError() : null);

        if (outStream is not null)
            Console.SetOut(new StreamWriter(outStream, encoding) { AutoFlush = true });
        if (errStream is not null)
            Console.SetError(new StreamWriter(errStream, encoding) { AutoFlush = true });

        // When writing straight to the console, start below the prompt PowerShell/cmd already printed.
        if (attached && redirectedOut is null)
            Console.WriteLine();
    }

    private static Encoding EncodingForCodePage(uint codePage)
    {
        if (codePage is not (0 or 65001))
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // OEM code pages such as 437/850
                return Encoding.GetEncoding((int)codePage);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
            {
                // fall through to UTF-8
            }
        }
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}
