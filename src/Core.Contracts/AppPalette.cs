using Oahu;

namespace Bit.App.Contracts;

/// <summary>
/// The app's own theme (Light, Dark, Black, Nord, Solarized Dark), read from the MAUI theme resources in C#, so native
/// screens draw the theme the user chose in Settings rather than only the system appearance (docs/oahu/gaps.md, G11).
/// Every contract declares its own <c>IOahuState&lt;AppPalette&gt; Palette</c> (OAHU native-screens.md, "Styling":
/// screens hosted page by page in OahuView are each their own root). Step 7: colors are <see cref="OahuColor"/>.
/// </summary>
public sealed record AppPalette(
    string Name,
    bool IsDark,
    OahuColor Background,
    OahuColor ListHeaderBackground,
    OahuColor ListHeaderText,
    OahuColor Text,
    OahuColor Muted,
    OahuColor Primary,
    OahuColor Danger,
    OahuColor Separator,
    OahuColor PasswordNumber,
    OahuColor PasswordSpecial,
    OahuColor BoxHeaderText);
