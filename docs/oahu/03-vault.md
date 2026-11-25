# Step 3: the vault list and item view as native screens

Branch `oahu/03-vault`, cut from `oahu/02-folders` (9bcb449c). Goal: move the vault list (`GroupingsPage`, used for
"My Vault" and every drill-down) and the read-only item view (`CipherDetailsPage`) to SwiftUI and Compose screens
hosted in `OahuView`, keeping MAUI's tabs, navigation, titles, toolbar items and dialogs, and find where OAHU makes
that hard. The findings are in [gaps.md](gaps.md) (G15–G23, plus notes under G3, G6, G7, G11 and G13); this file is
what was done.

OAHU: the same preview as step 2, `0.1.0-preview.g104c58c2`, from `~/RiderProjects/oahu-feed`.

Effort: about 1 hour 40 minutes of wall-clock time. Roughly 25 minutes reading the pages and docs, 35 minutes
contracts, view-model halves and the debug fakes, 20 minutes the four native screens, 20 minutes building and running
on both platforms (the Swift and Kotlin compiled first time; Android needed the version alignment, two Coil AARs and a
MAUI `Picker` fix), 10 minutes writing up. Android measurements were taken while other sessions kept the Mac at load
average 20–200.

## Results

| Target | Build | Run (DEBUG vault route, fake vault) |
|---|---|---|
| `net11.0-ios`, `iossimulator-x64`, Debug | Yes | iOS 18.6 simulator "BW OAHU iPhone": vault list with sections, website icons and icon-font glyphs; verification codes ticking and rolling over, copy with toast; drill-down three deep (filter "Example Org" > Engineering > Production > item); item view for login, card, view-only org login and a re-prompt login (master-password dialog, then reveal); reveal and re-hide password, card number and hidden field with colored runs; password history link; context menu (View/Edit/Options) and the "more" action sheet; vault filter sheet; pull to refresh through a background sync; empty vault with "Add an Item"; Nord theme; 2,020-item vault |
| `net11.0-android`, Debug | Yes | Emulator `OAHU_Pixel_API36` (read-only): vault list with Coil icons, MAUI FAB over the Compose screen; verification codes ticking, copy; item view with password and hidden field revealed; long-press menu and Options sheet; pull to refresh through a background sync; Nord theme; `FLAG_SECURE` blacking `screencap`; 2,020-item vault with a three-level drill-down |

Build and run:

```sh
cd src/App
dotnet build -f net11.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-x64
xcrun simctl install <udid> bin/Debug/net11.0-ios/iossimulator-x64/App.app
SIMCTL_CHILD_BW_OAHU_DEBUG=vault xcrun simctl launch <udid> com.8bit.bitwarden
#   also SIMCTL_CHILD_BW_OAHU_THEME=nord, SIMCTL_CHILD_BW_OAHU_OPEN=card-visa, ...=vault-large, ...=vault-empty

dotnet build -f net11.0-android -c Debug -t:Install -p:AdbTarget="-s emulator-5554"
adb shell am start -n com.x8bit.bitwarden/com.x8bit.bitwarden.MainActivity --es bwOahuDebug vault \
    --es bwOahuTheme nord --es bwOahuSecure 1 --es bwOahuDrill 1
```

## What changed

### Contracts (`src/Core.Contracts`)
- `Groupings.cs`: `IGroupingsViewModel` — `Copy`, `Palette`, `VaultFilter` (the "Vault: All" bar or null),
  `Empty` (text and whether to offer "Add an Item", or null), `IsLoading`, `IsRefreshing`, `Rows` (keyed list of the
  union `GroupingsRow`: `SectionHeader`, `Grouping`, `CipherItem`, `TotpItem`), and methods `Select`, `ShowOptions`,
  `Edit`, `CopyTotp`, `Task Refresh()`, `ChooseVaultFilter`, `Add`.
- `CipherDetails.cs`: `ICipherDetailsViewModel` — `Copy`, `Palette`, `Details` (one record: login, card or identity
  part, URIs, notes, custom fields as a union, attachments, footer lines), `Totp` (its own state, so the per-second
  tick doesn't re-send the item), and methods for copy, reveal (password, card number, card code, hidden field),
  check password, launch/copy URI, download attachment and the password history link. Hidden values are a
  `SecretValue` whose `Revealed` runs are null until shown.
- `AppPalette.cs`: the app theme's colors, shared by both contracts.

### View models (existing classes, now `partial`)
- `GroupingsPageViewModel.Oahu.cs`: states over the existing properties with `OahuState.FromProperty`;
  `GroupingsRowsProjection` turns `GroupedItems` into keyed rows (G15); methods reuse `CipherOptionsCommand`,
  `VaultFilterCommand`, the TOTP items' `CopyCommand` and `LoadAsync`; row taps and "Add an Item" go back to the page
  (G6).
- `CipherDetailsPageViewModel.Oahu.cs`: builds the `CipherDetails` record from the mutable `CipherView` and the
  view model's show/hide flags on each notification (published only when it differs), and the `TotpDisplay`; methods
  call the existing commands.
- `src/Core/Utilities/OahuPalette.cs`: one `StateSource<AppPalette>` read from the MAUI theme resources, re-read on
  `updatedTheme`.

### Pages
- `GroupingsPage.xaml`: the filter bar, empty state, `RefreshView` and `ExtendedCollectionView` with its four templates
  are one `OahuView<IGroupingsViewModel>`, still swapped in by `LoadOnAppearedAsync`. The toolbar items, the Android
  FAB and the `AccountSwitchingOverlayView` stay MAUI views in the same `AbsoluteLayout`, over the native screen.
  Code-behind: `RowSelected` is now `SelectItemAsync`, called from the view model's `ItemSelected`.
- `CipherDetailsPage.xaml`: the 660-line `ScrollView` (and its custom-field templates) is one
  `OahuView<ICipherDetailsViewModel>`. Toolbar items, the Android edit FAB and all dialogs unchanged.

### Native screens (`src/App/.oahu/App/`)
- SwiftUI: `Groupings.oahu-screen.swift` (plain `List` with `Section`s rebuilt from the flat rows, `.refreshable` with
  `OahuWait`, context menu and swipe action, `AsyncImage` icons, `TotpRing`), `CipherDetails.oahu-screen.swift`
  (inset grouped `List`, colored revealed values), `BitwardenTheme.swift` (font registration, palette colors, icon,
  ring). `Package.swift` now declares `resources: [.process("Resources")]` for `bwi-font.ttf` (G17).
- Compose: `Groupings.oahu-screen.kt` (`PullToRefreshBox`, `LazyColumn` with `stickyHeader`s, long-press
  `DropdownMenu`, bottom padding for the MAUI FAB), `CipherDetails.oahu-screen.kt`, `BitwardenPalette.kt` (font, palette,
  Coil `SubcomposeAsyncImage`, ring). `build.gradle.kts` adds `coil-compose` and `coil-network-okhttp` 3.5.0;
  `res/font/bwi_font.ttf`. `oahu-host-versions.gradle` re-aligned with `oahu versions --align-android` (G3).
- Generated files for the two new contracts are committed beside them, as in step 2.

### App project and MAUI
- `src/App/App.csproj`: Coil's AARs and dependencies for Android, from `hosting.md` plus two (G18).
- `src/Core/MauiProgram.cs`: the Android `Picker` handler is registered for `Picker` as well as `IPicker` (a MAUI 11
  crash on the Generator tab, "Not OAHU").

### Debug route (DEBUG only, `src/Core/Pages/Settings/OahuDebug/`)
- `vault`, `vault-large`, `vault-empty` show the app's real `TabsPage` over `FakeVault` (about 25 items: logins with
  website URIs, Android/iOS app URIs, TOTP with 30- and 60-second periods, a re-prompt item, a view-only org item,
  custom fields of all four types, attachments, password history, a passkey; two cards; an identity; two notes;
  deleted items; five folders with nesting; two organizations with nested collections; plus 2,000 generated logins for
  `vault-large`). All values are made up.
- Fakes for the cipher, folder, collection, organization, policy, sync, vault timeout, event, audit and password
  re-prompt services (`FakeVaultServices.cs`; the re-prompt accepts "debug"), and `OahuDebugStateService`, a wrapper
  over the real state service that reports a signed-in, unlocked account and forwards everything else (171 members
  generated by `gen_decorator.py`). The fake sync changes the vault and announces `syncCompleted` from a pool thread.
- Options: theme, open an item, screen-capture protection, drill down (see the file header).

## Left in MAUI
- **Account switching overlay** (`AccountSwitchingOverlayView`) and the avatar toolbar item: a MAUI overlay over the
  native screen, as asked; not opened in testing.
- **Search** (`CiphersPage`, opened from the toolbar): unchanged.
- **Every dialog and pushed page**: the options action sheet, vault filter sheet, master-password re-prompt, check
  password result, attachment download prompts, add/edit item, password history, attachments, collections and share.
- **Toolbar items and floating buttons** on both pages.

## Not done or not verified
- Live theme change while a native screen is open (the debug state pins the theme); the step 2 folder screens still
  ignore the app theme.
- iOS `.swipeActions` on cipher rows (built, not exercised); Android vault filter sheet (same code as iOS, which was
  exercised); the account overlay.
- Attachment download, Check password, Clone/Move/Collections/Delete from the item view, and launching URIs were not
  tapped. Restore and Delete on trashed items were not tried.
- Android performance numbers are from a heavily loaded host (G19, "Probed and fine").
- Real accounts, real sync, Release builds, devices, iOS 27/arm64 simulators, TalkBack/VoiceOver, Dynamic Type.

## Size

| | Lines |
|---|---|
| App C#/XAML changed in existing files (two pages, two view models, `MauiProgram.cs`, `App.csproj`) | +44 / -801 (the details XAML alone -660) |
| New C#: contracts (`Groupings.cs`, `CipherDetails.cs`, `AppPalette.cs`) | 295 |
| New C#: view-model contract halves and `OahuPalette` | 649 |
| New C#: DEBUG route and fakes (171 of them generated forwarders) | 1,126 |
| Native code written by hand: Swift 693, Kotlin 646 | 1,339 |
| Generated native code (committed, not counted above) | 40 files new or regenerated |

## Security-relevant notes
- No crypto, key derivation, secure storage or biometric code changed. The fake vault never decrypts anything: its
  `EncString`s have their decrypted value preset by reflection, as step 2's fake folders did.
- Passwords, card numbers, security codes and hidden custom fields cross to the native screens only while revealed,
  and copying stays in C# (G22). TOTP codes cross for display, as MAUI shows them. Website hostnames go to
  `icons.bitwarden.net` exactly as the MAUI page's `CachedImage` sends them.
- The DEBUG route replaces eleven services and wraps the state service for the process, and repoints the Android device
  action service's state service by reflection; it must never run on a device with a real account. It is `#if DEBUG`
  and needs the launch variable or extra.
- Android `FLAG_SECURE` (the "allow screen capture" setting) covers the native screens (verified). iOS has no screen
  capture protection in this app, before or after.
- Coil and OkHttp are new native dependencies in the Android app (AARs from Maven Central).
