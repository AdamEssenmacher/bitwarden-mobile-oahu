using Oahu;

namespace Bit.App.Contracts;

/// <summary>
/// The app's own theme (Light, Dark, Black, Nord, Solarized Dark), read from the MAUI theme resources in C#, so native
/// screens draw the theme the user chose in Settings rather than only the system appearance (docs/oahu/gaps.md, G11).
/// Colors are 0xAARRGGBB. Every contract that wants it declares its own <c>IOahuState&lt;AppPalette&gt; Palette</c>.
/// </summary>
public sealed record AppPalette(
    string Name,
    bool IsDark,
    long Background,
    long ListHeaderBackground,
    long ListHeaderText,
    long Text,
    long Muted,
    long Primary,
    long Danger,
    long Separator,
    long PasswordNumber,
    long PasswordSpecial,
    long BoxHeaderText);
