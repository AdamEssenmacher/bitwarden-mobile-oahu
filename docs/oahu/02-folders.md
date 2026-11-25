# Step 2: folder pages as native screens

Branch `oahu/02-folders`, cut from `oahu/01-net11-upgrade` (3eba535b). Goal: wire OAHU into the app the documented way
and move Settings > Folders (`FoldersPage`) and its add/edit page (`FolderAddEditPage`) to SwiftUI and Compose screens
hosted in `OahuView`, keeping MAUI's navigation, titles, toolbar items and dialogs. The findings are in
[gaps.md](gaps.md); this file is what was done.

OAHU: preview `0.1.0-preview.g104c58c2` (AdamE.Oahu `104c58c2`, origin/main at the time), from the existing local feed
`~/RiderProjects/oahu-feed`, which already held that version (no new pack was needed).

Effort: about 2 hours 15 minutes of wall-clock time. Roughly 20 minutes reading docs, 25 minutes setup and contracts,
25 minutes screens and page changes, 50 minutes building and running (half of it diagnosing iOS modals that never
appeared, which was a step 1 bug), 15 minutes writing up.

## Results

| Target | Build | Run (DEBUG route, fake folder service) |
|---|---|---|
| `net11.0-ios`, `iossimulator-x64`, Debug | Yes | iOS 18.6 simulator "BW OAHU iPhone": list, empty state, add (toolbar + and keyboard Go), edit, save, delete (options sheet + confirm), validation alert, Close |
| `net11.0-android`, Debug | Yes | Emulator `OAHU_Pixel_API36` (read-only): list, empty state, add (Compose FAB, keyboard Go), edit with MAUI Save, delete (overflow menu + confirm), system Back |

Build and run:

```sh
cd src/App
dotnet build -f net11.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-x64
xcrun simctl install <udid> bin/Debug/net11.0-ios/iossimulator-x64/App.app
SIMCTL_CHILD_BW_OAHU_DEBUG=folders xcrun simctl launch <udid> com.8bit.bitwarden     # or folders-empty

dotnet build -f net11.0-android -c Debug -t:Install -p:AdbTarget="-s emulator-5554"
adb shell am start -n com.x8bit.bitwarden/com.x8bit.bitwarden.MainActivity --es bwOahuDebug folders
```

The iOS build stays x86_64-only (as in step 1). OAHU supports that simulator architecture, so libargon2 and AppCenter
were not touched.

## What changed

### Setup (`oahu init`)
- `dotnet tool install Oahu.Cli` into the existing local tool manifest, then
  `dotnet tool run oahu init --project src/App/App.csproj --feed ~/RiderProjects/oahu-feed`, then again with
  `--contracts src/Core.Contracts/Core.Contracts.csproj`. `init` worked; the manual setup was not needed.
- It wrote `src/App/.oahu/App/` (MSBuild import, Swift package `OahuScreens`, Gradle project with module
  `oahu-screens`, an agent guide), imported `.oahu/App/Oahu.targets` from `App.csproj` and added the feed to
  `nuget.config`.
- The Android restore inside `init` failed on an AutoFill downgrade (gaps G2); `Xamarin.AndroidX.AutoFill` is now
  1.3.0.4 in App and Core. The first Android build failed OAHUVER001 (G3); App references
  `Xamarin.Kotlin.StdLib.Jdk7`/`Jdk8` 2.4.0.1.
- `App/MauiProgram.cs` calls `UseOahu()` and logs `MethodFailed` and `Faulted` per platform.

### Where the contracts live
`src/Core.Contracts` (new, `net11.0`, nullable on, references `Oahu`), as the docs ask: a plain class library oahu-gen
can read. The view models stay in `src/Core` (`Bit.App.Pages`); `Core.csproj` references the contracts project and,
for iOS and Android, `Oahu.Maui`, because the pages are in `Core` too (G1). The project is added to
`bitwarden-mobile.sln`.

Contracts (`src/Core.Contracts/Folders.cs`):
- `IFoldersViewModel`: `Copy` (static labels), `ShowNoData`, `Folders` (keyed list of `FolderRow(Key, Name)`),
  `Open(key)`, `Add()`.
- `IFolderAddEditViewModel`: `Copy`, `Name` (editable), `FocusName`, `Task Submit()`.

### View models (INPC adapters)
Both view models became `partial`; everything else is in new `*.Oahu.cs` files:
- `FoldersPageViewModel.Oahu.cs`: `OahuKeyedList.FromCollection` over the existing `Folders` collection,
  `OahuState.FromProperty` over `ShowNoData`; `Open`/`Add` raise C# events the page handles (G6).
- `FolderAddEditPageViewModel.Oahu.cs`: `OahuEditable.FromProperty` over `Folder.Name` (watching `Folder`), `Submit`
  calls the existing `SubmitAsync` (what the Entry's Go key did).

### Pages
- `FoldersPage.xaml`: the `Label` + `CollectionView` layout is now `<oahu:OahuView x:TypeArguments="contracts:IFoldersViewModel" ViewModel="{Binding .}"/>`,
  still swapped in after load by `LoadOnAppearedAsync`. The MAUI floating button is gone (Compose draws one on
  Android; iOS keeps the toolbar items). The code-behind handles the view model's `FolderSelected`/`AddRequested` with
  the old navigation code and `DoOnce`.
- `FolderAddEditPage.xaml`: the `ScrollView` form is now an `OahuView` for `IFolderAddEditViewModel`. Toolbar items
  (Cancel, Save, Delete on Android, the options sheet on iOS) are unchanged. The native screen focuses the field when
  adding, replacing `RequestFocus(_nameEntry)`.

### Native screens (`src/App/.oahu/App/`)
- SwiftUI: `Folders.oahu-screen.swift` (inset grouped `List`, `ContentUnavailableView` for empty),
  `FolderAddEdit.oahu-screen.swift` (`Form` with a `TextField`, Go submits, focus on add).
- Compose: `Folders.oahu-screen.kt` (`LazyColumn` of `ListItem`s, empty state, FAB), `FolderAddEdit.oahu-screen.kt`
  (`OutlinedTextField` on `rememberTextFieldBinding`, Go commits then submits, focus on add), `Theme.kt` (Bitwarden
  colors, two vector icons, since the module has no material-icons dependency).
- Generated `*.oahu.swift`/`*.oahu.kt` files are committed beside them so the native projects open in Xcode and
  Android Studio without a .NET build first.

### Debug route (DEBUG only)
`src/Core/Pages/Settings/OahuDebug/`: `OahuDebugRoute` replaces the login home with a small host page when the
`BW_OAHU_DEBUG` environment variable (iOS) or `bwOahuDebug` intent extra (Android) is `folders` or `folders-empty`, and
swaps the registered `folderService` for `FakeFolderService`, an in-memory list with no server and no cryptography (its
"encrypted" names are base64 with the decrypted value preset by reflection). The host page opens `FoldersPage` modally,
as Settings > Vault does. One `#if DEBUG` hook in `App.xaml.cs` (`NavigationTarget.HomeLogin`). Nothing changes
without the variable or extra, and none of it compiles into Release.

### Step 1 fix
`src/App/Platforms/iOS/SceneDelegate.cs` hid MAUI's own scene activation handlers, so on iOS no MAUI modal page was
ever presented. It now overrides `OnActivated`, `OnResignActivation` and `OpenUrl` and calls base (gaps, "Not OAHU").

## Size

| | Lines |
|---|---|
| App C#/XAML changed in existing files (pages, view models, `App.xaml.cs`, `MauiProgram.cs`, `SceneDelegate.cs`) | about +60 / -80 |
| New C#: contracts | 61 |
| New C#: view-model contract halves | 79 |
| New C#: DEBUG route and fake service | 202 |
| Native code written by hand (Swift 79, Kotlin 182 incl. theme) | 261 |
| Generated native code (committed, not counted above) | 12 files |
| Project files: `Core.csproj`, `App.csproj`, `nuget.config`, sln, tool manifest | small |

## Security-relevant notes
- No crypto, key derivation, storage or biometric code changed. libargon2 and AppCenter are untouched.
- `FakeFolderService` and the debug route are `#if DEBUG` and only active with the launch variable or intent extra; they
  replace the folder service in the service container for that process, so they must never be enabled on a device with
  a real account.
- `Xamarin.AndroidX.AutoFill` went from 1.1.0.18 to 1.3.0.4 (the autofill service uses it); not exercised.
- `SceneDelegate` now lets MAUI see scene activation and URL opens as well as forwarding them to `AppDelegate`; the
  privacy splash, lock-on-resume and deep links were not exercised.

## Not verified
- Real accounts and the real `FolderService` (sync, encryption, server errors, the offline dialog).
- iOS 27 / arm64 simulators, devices, Release builds, app extensions at run time (they now link OahuRuntime, G10).
- Dark mode and the app's own themes on the native screens; landscape; Dynamic Type; TalkBack/VoiceOver.
- Saving from MAUI's toolbar while an Android IME is mid-composition (G12).
- Whether lifecycle callbacks now run twice on iOS after the `SceneDelegate` change.
