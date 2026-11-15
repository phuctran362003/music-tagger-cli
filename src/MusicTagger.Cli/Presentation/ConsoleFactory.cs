using System.Text;
using MusicTagger.Core.Models;
using Spectre.Console;

namespace MusicTagger.Cli.Presentation;

/// <summary>
/// Creates the console used by the UI. Colour is auto detected by default; the user can force
/// it on or off in Settings so the app also works on terminals without ANSI support
/// (SRS FR-014, 5.5).
/// </summary>
public static class ConsoleFactory
{
    public static IAnsiConsole Create(ColorMode colorMode)
    {
        var settings = new AnsiConsoleSettings
        {
            Ansi = colorMode == ColorMode.On ? AnsiSupport.Yes : AnsiSupport.Detect,
            ColorSystem = colorMode switch
            {
                ColorMode.On => ColorSystemSupport.Standard,
                ColorMode.Off => ColorSystemSupport.NoColors,
                _ => ColorSystemSupport.Detect,
            },
            Interactive = InteractionSupport.Detect,
            Out = new AnsiConsoleOutput(Console.Out),
        };

        return AnsiConsole.Create(settings);
    }

    /// <summary>
    /// Switches the process wide console encodings to UTF-8 so Vietnamese text can be typed,
    /// piped and displayed. This must run <b>before</b> <see cref="Create"/>: the console writer
    /// keeps the code page that was active when it was captured, and a stale code page turns
    /// Unicode input into mojibake that would be stored in the tag frames.
    /// Failure is not fatal: the app keeps running with the encoding the terminal already uses
    /// and the returned message is shown once.
    /// </summary>
    public static string? ConfigureUtf8()
    {
        try
        {
            var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            if (Console.OutputEncoding.CodePage != Encoding.UTF8.CodePage)
            {
                Console.OutputEncoding = utf8;
            }

            if (Console.InputEncoding.CodePage != Encoding.UTF8.CodePage)
            {
                Console.InputEncoding = utf8;
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or PlatformNotSupportedException or System.Security.SecurityException or ArgumentException)
        {
            return "Note: the terminal does not allow switching to UTF-8. Vietnamese characters may not display correctly.";
        }
    }
}
