# OAHU gap log

A running list of the places where adopting OAHU in this app needed something the docs or tools did not give: a doc
that was wrong or unclear, a missing feature, a failing tool, or an app-side shim. Later steps append here. Each entry
names the step that found it.

Severity: **blocker** (no app-side way past it), **friction** (cost real time or needed a shim), **papercut** (small,
annoying), **doc** (the docs were wrong, missing or misleading). "Verified" means seen on a simulator, emulator or in
build output; "inferred" means read from code or docs and not exercised.

OAHU used so far: preview `0.1.0-preview.g104c58c2` (AdamE.Oahu commit `104c58c2`, origin/main on 2026-11-25), from
the local feed `~/RiderProjects/oahu-feed`.

## Step 2 (folders)

### G1. `oahu init` and the MAUI guide assume pages and view models live in the app project (friction)
- Trying to: host native screens from pages that live in a MAUI class library (`src/Core`, `UseMaui`, targets
  `net11.0-ios;net11.0-android`), next to their view models, with `src/App` only the thin executable.
- What happened: `oahu init --project src/App/App.csproj` adds `Oahu.Maui`, the contract `ProjectReference` and the
  `OahuGen` items to `src/App` only (`src/App/.oahu/App/Oahu.targets`). The pages that use `OahuView<T>` and the view
  models that implement the contracts are in `src/Core`, which then fails to compile. `docs/consumer/maui-migration.md`
  shows the view model in the app project (`App/NoteViewModel.Oahu.cs`) and never mentions a library holding pages.
  `init` has no option for "the project that holds the pages".
- Workaround: hand-edited `src/Core/Core.csproj`: a `ProjectReference` to `src/Core.Contracts` and a mobile-only
  `PackageReference` to `Oahu.Maui`. The preview version string is now written in three places (`Oahu.targets`,
  `Core.Contracts.csproj`, `Core.csproj`); `init` defines no MSBuild property such as `$(OahuVersion)` to reuse, so a
  feed update means finding them all by hand.

### G2. `init`'s Compose packages collide with the app's pinned AndroidX packages (friction)
- Trying to: run `oahu init` on an app that pins older AndroidX bindings.
- What happened: the Android restore inside `init` failed: `error NU1605: Warning As Error: Detected package
  downgrade: Xamarin.AndroidX.AutoFill from 1.3.0.4 to 1.1.0.18 ... App -> Xamarin.AndroidX.Compose.UI 1.11.3.1 ->
  Xamarin.AndroidX.Compose.UI.Android 1.11.3.1 -> Xamarin.AndroidX.AutoFill (>= 1.3.0.4)`. `init` reported it clearly,
  succeeded anyway and printed the align command to run later, as `Oahu.Cli.md` says it does.
- Workaround: raised `Xamarin.AndroidX.AutoFill` to 1.3.0.4 in `src/App/App.csproj` and `src/Core/Core.csproj`, then
  reran `init`. Behavior change of that bump is untested (autofill service).

### G3. First Android build fails OAHUVER001 after `init` said it was aligned (friction)
- Trying to: build the Android target right after `init` reported "Aligned oahu-screens with the restored NuGet
  bindings: 170 Maven constraint(s)".
- What happened: `error OAHUVER001: Xamarin.Kotlin.StdLib.Jdk7 resolves to 1.9.0 through another package's
  dependency, but oahu.versions.json has kotlin 2.4.0 (NuGet 2.4.0.R (any binding revision), e.g. 2.4.0.1). Reference
  Xamarin.Kotlin.StdLib.Jdk7 at 2.4.0.R ... directly` (same for Jdk8). They come from `Xamarin.Firebase.Messaging` ->
  `Xamarin.Firebase.Encoders.JSON`. The message is good and says exactly what to do, but neither `init` nor
  `oahu doctor` checks it, so it only shows up in the first full build.
- Workaround: direct references to `Xamarin.Kotlin.StdLib.Jdk7` and `Jdk8` 2.4.0.1 in `src/App/App.csproj`.

### G4. `oahu generate` silently generates nothing until a screen file exists (friction)
- Trying to: generate the Swift and Kotlin models after writing the contracts, to see their API before writing screens.
- What happened: `dotnet tool run oahu generate --project src/App/App.csproj --framework net11.0-ios` printed
  `oahu-gen: : 0 file(s) written or removed.`; `--json` reported `"current": true, "files": []`, no diagnostics.
  Generation only happens for contracts that have a `{Name}.oahu-screen.swift`/`.kt` file
  (`docs/consumer/oahu-gen.md`: "one provider per screen source it discovers"), so the model a screen is written
  against does not exist until a screen file referring to it exists. Nothing in the output says "2 contracts found,
  0 screens; create Folders.oahu-screen.swift". `init`'s "Next for ios" line names the file convention but not the
  consequence.
- Workaround: wrote the screen files blind from the docs' member mapping, then generated, then corrected them.

### G5. A keyed-list row without `IOahuKeyed` gives a plain compiler error, not an OAHU one (papercut)
- `public sealed record FolderRow(string Key, string Name);` used in `IOahuKeyedList<FolderRow>` fails with
  `CS0311 ... There is no implicit reference conversion from 'FolderRow' to 'Oahu.IOahuKeyed'`. `contracts.md` does
  say rows implement `IOahuKeyed`, so this is only a discoverability point. Fixed by adding `: IOahuKeyed`.

### G6. Native intents land on the view model, but this app navigates from page code-behind (friction)
- Trying to: keep the MAUI page's navigation (row tap pushes `FolderAddEditPage` modally; Android's add button) while
  the tap happens in the native screen.
- What happened: contract methods are called on the bound view model. In this app (as in many Xamarin.Forms-era apps)
  navigation lives in the page's event handlers (`RowSelected`, `AddButton_Clicked`, with a `DoOnce` debounce), not the
  view model. Neither `maui.md` nor `maui-migration.md` covers routing a native intent back to the page.
- Workaround: the view model's contract half raises plain C# events (`FolderSelected`, `AddRequested`) that the page
  subscribes to (`src/Core/Pages/Settings/FoldersPageViewModel.Oahu.cs`, `FoldersPage.xaml.cs`). Small, but it is a
  shim every such page will need.

### G7. Static labels need a "copy" record per screen (papercut)
- The native screens cannot read the app's `AppResources` resx, so "There are no folders to list.", "Add Folder" and
  "Name" go through `IOahuState<FoldersCopy>`/`IOahuState<FolderAddEditCopy>` (made with `new StateSource<T>(...)`).
  This is exactly what `docs/consumer/localization.md` prescribes, and it worked; it is boilerplate per screen, and the
  app's own culture handling (`SetCulture()`) and `OahuCulture` were not reconciled (not checked: a non-English UI).

### G8. `init` derives an odd Kotlin package and generic names (papercut)
- For `src/App/App.csproj`, `init` chose Kotlin package `com.oahu.appapp`, Gradle root `oahu-native`, module
  `oahu-screens` and Swift package `OahuScreens`. The doubled `appapp` looks like a bug in name derivation. There is no
  init option to choose them. Kept as generated.

### G9. `init` writes machine-relative feed paths; the guide uses environment variables (doc)
- `maui-migration.md` ("Set up the feed and folders") tells you to export `OAHU_FEED`/`OAHU_VERSION` and use
  `%OAHU_FEED%/nuget`, `System.getenv("OAHU_FEED")` and an absolute path in `Package.swift`. `init` instead wrote
  relative paths into committed files: `nuget.config` (`../oahu-feed/nuget`), `Package.swift`
  (`../../../../../../../oahu-feed/swift/0.1.0-preview.g104c58c2/OahuRuntime`) and `settings.gradle.kts`
  (`../../../../../../oahu-feed/maven`). These work only when the feed sits beside the repository. The guide says "If you
  use the CLI, use its generated MSBuild and native build files", so both are "right", but the two setups differ more
  than the guide suggests. `init` also wrote the `nuget.config` line with different indentation from its neighbours.

### G10. `Oahu.Maui` in a library shared with iOS app extensions links the extensions against OahuRuntime (friction, inferred)
- `src/Core` is also referenced by the three iOS extensions (autofill, action, share). Because `Core` now references
  `Oahu.Maui` (G1), each `.appex` binary links `@rpath/OahuRuntime.framework/OahuRuntime` (seen with `otool -L`); it
  resolves through `@executable_path/../../Frameworks` to the host app's copy. The extensions were not launched, so it
  is not known whether they load, nor what OAHU costs inside the autofill extension's memory limit. Nothing in the docs
  covers app extensions. A cleaner split would keep OAHU out of the library the extensions use, which this app's
  layout (pages in `Core`) does not allow without moving pages.

### G11. The app's own theme does not reach native screens (friction)
- Bitwarden has an in-app theme setting (Light, Dark, Black, Nord, Solarized) applied through MAUI resources. Native
  screens follow only the system appearance; `maui.md` "Known limits" says no environment values reach a hosted screen.
- Workaround: the Compose screens use a small `BitwardenTheme` with Bitwarden's primary colors
  (`src/App/.oahu/App/android/oahu-screens/src/main/kotlin/Theme.kt`); Swift hard-codes the blue. A user on "Nord" gets
  system-styled native screens. Passing a theme record through every contract would work but is per-screen boilerplate.

### G12. A MAUI toolbar action cannot flush a Compose text field's pending edit (friction, inferred)
- Save is a MAUI toolbar item; the name field is a Compose `TextFieldState`. `native-screens.md` says text during an
  IME composition is not proposed until the composition ends, and `rememberTextFieldBinding().commit()` sends pending
  text, but only native code can call `commit()`. Tapping MAUI's Save while Gboard is still composing a word could save
  the previous text. Not reproduced: `adb shell input text` commits immediately, and the save in the run had the full
  text. The keyboard's Go key does call `commit()` before `submit()`.

### G13. A whole-page iOS screen stops at the bottom safe area (papercut)
- The SwiftUI `List` background ends above the home indicator, leaving a strip of the MAUI page's white background
  below it (visible in the grouped-list screenshots). MAUI lays the `OahuView` out inside the safe area; the screen
  cannot extend under it. Not fixed.

### G14. `oahu doctor` text output is mostly Gradle resolution lines (papercut)
- Without `--json`, `doctor` printed over a hundred `Gradle resolved androidx.compose...` lines before the one warning
  that mattered (`OAHUCLI011`, no emulator running). `--json` is clean.

### Worked, worth recording
- **x86_64 simulator**: supported. `OahuRuntime.xcframework` in `Oahu.iOS` has an `ios-arm64_x86_64-simulator` slice,
  and `OahuSwiftPackage.targets` builds the screen package for `x86_64` when `RuntimeIdentifier=iossimulator-x64`.
  The app builds and runs on the iOS 18.6 simulator under Rosetta with no change, so libargon2 and AppCenter were
  left alone.
- `OahuView<T>` declared in a page's `ResourceDictionary` and swapped into a `ContentView` after the page's async load
  (Bitwarden's `LoadOnAppearedAsync` pattern) worked on both platforms.
- `OahuEditable.FromProperty` over a nested, non-notifying property (`Folder.Name`, watched through `Folder`) worked:
  edits settle by reading back.
- `OahuKeyedList.FromCollection` over the app's `ExtendedObservableCollection` (`ResetWithRange` removes items one by
  one then raises `Reset`) updated the list correctly after add, rename and delete, including empty to one row.
- MAUI dialogs (`DisplayAlert`, action sheet, the app's loading overlay and toasts) over a page hosting a native screen
  worked on both platforms.

### Not OAHU (found while testing; recorded so later steps don't re-diagnose)
- MAUI modal pages never appeared on iOS: the step 1 `SceneDelegate` exported `sceneDidBecomeActive:`,
  `sceneWillResignActive:` and `scene:openURLContexts:` on new methods, hiding `MauiUISceneDelegate`'s handlers, so MAUI
  never saw the window activate and `PushModalAsync` returned without presenting. Fixed by overriding `OnActivated`,
  `OnResignActivation` and `OpenUrl` and calling base (`src/App/Platforms/iOS/SceneDelegate.cs`). This affected every
  modal in the app on iOS, not just these pages.
- iOS "more" toolbar item (`more_vert.png`) draws no icon (the item is there and tappable at the far right). Probably the
  `MT7156` duplicate-resource warnings for `more_vert*.png`; untouched.
- Android Debug: a plain `adb install` of the APK crashes at start (`Failed to load System.Private.CoreLib.dll`) because
  Debug uses fast deployment; install with `dotnet build -t:Install`.
- Android navigates to the login home twice at start, so the debug route builds its host page twice.
