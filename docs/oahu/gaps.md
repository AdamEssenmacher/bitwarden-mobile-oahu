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
- Seen again in step 3: adding Coil to the Gradle screen module (G18) pulled OkHttp, whose POM brings
  `kotlin-stdlib-jdk7`/`jdk8` 1.9.0 into the module's runtime classpath, and the build failed with `error OAHUVER005:
  Gradle resolved org.jetbrains.kotlin:kotlin-stdlib-jdk7 1.9.0; NuGet restored Xamarin.Kotlin.StdLib 2.4.0.1`. The
  `oahu-host-versions.gradle` that `init` wrote in step 2 still pinned `kotlin-stdlib-jdk7:1.9.0` ("Xamarin.Kotlin.StdLib.Jdk7
  1.9.0 in the .NET host"): it was aligned before the app pinned Jdk7/Jdk8 to 2.4.0.1, and nothing said it was stale
  until a Gradle dependency used it. `dotnet tool run oahu versions --align-android`, as the `OAHUAPK007` warning said,
  fixed it (3 changed constraints).

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
- Seen again in step 3: both pages navigate from code-behind (`RowSelected`, `AddButton_Clicked`,
  `PasswordHistory_Tapped`), so `GroupingsPageViewModel.Oahu.cs` raises `ItemSelected`/`AddRequested` and
  `CipherDetailsPageViewModel.Oahu.cs` raises `PasswordHistoryRequested` for the pages to handle. `RowSelected` became
  `SelectItemAsync(IGroupingsPageListItem)` so the old body is reused (`src/Core/Pages/Vault/GroupingsPage/GroupingsPage.xaml.cs`).

### G7. Static labels need a "copy" record per screen (papercut)
- The native screens cannot read the app's `AppResources` resx, so "There are no folders to list.", "Add Folder" and
  "Name" go through `IOahuState<FoldersCopy>`/`IOahuState<FolderAddEditCopy>` (made with `new StateSource<T>(...)`).
  This is exactly what `docs/consumer/localization.md` prescribes, and it worked; it is boilerplate per screen, and the
  app's own culture handling (`SetCulture()`) and `OahuCulture` were not reconciled (not checked: a non-English UI).
- Seen again in step 3: `GroupingsCopy` (9 strings) and `CipherDetailsCopy` (32 strings) are copy records again.
  Two app-specific twists: GroupingsPage upper-cases section titles on iOS only (`#if IOS ... ToUpperInvariant`), so
  platform presentation is decided in C# and arrives finished; and the `Filter` key the XAML used
  (`{u:I18n Filter}`) does not exist in `AppResources.resx` (MAUI showed the key itself), found only because the copy
  record needed a C# property (`FilterByVault` used instead; an app bug, not OAHU).

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
- Seen again in step 3, and worked around for the new screens: a shared `AppPalette` record
  (`src/Core.Contracts/AppPalette.cs`) read from the MAUI theme resources (`src/Core/Utilities/OahuPalette.cs`, one
  `StateSource<AppPalette>` re-read on the app's `updatedTheme` message) and declared as `IOahuState<AppPalette>
  Palette` on both contracts. With `BW_OAHU_THEME=nord` the vault list and item view drew Nord colors on iOS and Android
  (verified, screenshots), and `.environment(\.colorScheme, ...)`/a Material `darkColorScheme` made system controls
  (context menus, pull-to-refresh) follow. The step 2 folder screens still use the system appearance. A theme change
  while a screen is open was not exercised (the debug state service pins the theme). See G16 for the cost.

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
- Not seen in step 3: the vault list sits above the tab bar and the item view's background fills to the bottom on
  both themes tried.

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

## Step 3 (vault)

Pages: the vault list (`GroupingsPage`, "My Vault" and every drill-down) and the item view (`CipherDetailsPage`).
Same OAHU preview as step 2. Details of what was built are in [03-vault.md](03-vault.md).

### G15. Keyed lists have no sections, and `FromCollection` can't key a row by its section (friction)
- Trying to: show GroupingsPage's grouped list (Favorites, Types, Folders, Collections, Items, No Folder, Trash, each
  with a header and a count) from its existing `ObservableRangeCollection<IGroupingsPageListItem> GroupedItems`
  (header items followed by their rows, rebuilt with new objects on every load).
- What happened: `inpc.md` says it plainly ("Grouped collections have no adapter: a keyed list has no sections.
  Flatten the groups into one list (give each row its group, or add header rows)"). Flattening was easy: a union
  `GroupingsRow` with a `SectionHeader` case. Keying was not: the same cipher appears in two sections (a favorite with
  no folder is in Favorites and in No Folder), so the cipher id alone is a duplicate key, which faults the list. The
  `FromCollection` mapper gets only the item, not its position or the header above it, and the items are new objects on
  every load, so `OahuKeys.Identity` would replace every row on every reload (2,000 rows after each sync).
- Workaround: `GroupingsRowsProjection` in `src/Core/Pages/Vault/GroupingsPage/GroupingsPageViewModel.Oahu.cs` (about
  130 lines): listens to `CollectionChanged`, walks the collection tracking the current header, keys rows
  `"<section>|c|<cipher id>"`, keeps its own key-to-item dictionary for the methods (what `TryGetItem` would have done),
  subscribes to each TOTP item's `PropertyChanged` and calls `KeyedListSource.Reconcile`/`Update`. On the native side
  SwiftUI wants `Section`s, so `GroupingsSection.split` regroups the flat `KeyedList` into sections on every body
  evaluation (`Groupings.oahu-screen.swift`); Compose's `LazyColumn` takes the flat list directly with `stickyHeader`.
  A `FromCollection` overload whose mapper also gets the index, or a grouped adapter (an `IEnumerable<IGrouping>`, or a
  "header predicate"), would have removed the class.
- Measured (DEBUG timing log in the projection, Android emulator, Debug): 2,014 rows mapped in 28 ms and reconciled
  in 2 ms on first load; 43–85 rows in under 10 ms.

### G16. No value is shared by every screen: each contract re-declares theme and glyphs (friction)
- Trying to: draw both new screens (and, later, every converted page) in the app's own theme.
- What happened: OAHU has no ambient or app-wide state reaching screens (`maui.md`, "Known limits": no environment
  values), and a contract can't extend a base interface (OAHU001), so "every screen gets the palette" is one
  `IOahuState<AppPalette> Palette` member per contract, one line per view model implementation, and one
  `PaletteTheme`/`.environment` wrapper per screen. The step 2 folder screens don't have it, so the app now has two
  looks for one theme setting. The same applies to anything app-wide (the icon-font glyphs the screens' own buttons
  use, G21; a font scale; a "screen capture allowed" flag).
- Workaround: one shared `StateSource<AppPalette>` (`src/Core/Utilities/OahuPalette.cs`) returned by every contract.
  It works (G11), but nothing makes a new contract include it.

### G17. Adding a resource or a library means editing files `init` manages, and `init` then refuses to run (friction)
- Trying to: bundle Bitwarden's icon font in the Swift package and use Coil in the Gradle module, as `hosting.md`
  describes ("declare them on the target (`resources: [.process("Resources")]` ...)"; "Android: third-party libraries").
- What happened: both live in files `init` wrote and records by hash in `.oahu/App/setup.json`
  (`ios/OahuScreens/Package.swift`, `android/oahu-screens/build.gradle.kts`), and the managed `AGENTS.md` says "Never
  edit ... the files `.oahu/App/setup.json` lists". After the edits, `dotnet tool run oahu init --project App.csproj
  --dry-run` stops with `oahu: .../ios/OahuScreens/Package.swift already exists or has local changes. Move your changes
  aside before retrying.` (no diagnostic code). `oahu doctor --json` reports nothing about it. So the documented way to
  add a font or an image loader leaves an app that can't take the next `init` (a version upgrade, a deployment-target
  change) without hand-merging.
- Workaround: edited both files anyway (comments mark the lines). Not resolved: the next `init` rerun.

### G18. The Coil recipe in `hosting.md` doesn't cover Coil's Compose `AsyncImage` (doc)
- Trying to: load website icons (`https://icons.bitwarden.net/<host>/icon.png`) in Compose, following
  `hosting.md` "Android: third-party libraries" (four `AndroidMavenLibrary` items for Coil 3.5.0 and five packages).
- What happened: the doc lists the AARs but not the Gradle dependency they correspond to. With
  `implementation("io.coil-kt.coil3:coil-compose:3.5.0")` (the usual Compose entry point, with the singleton image
  loader) the build failed: `error XA4241: Java dependency 'io.coil-kt.coil3:coil-compose-android:3.5.0' is not
  satisfied.` Adding `coil-compose-android` and `coil-android` fixed it. Before that, the OkHttp it brings tripped
  `OAHUVER005` (G3, seen again). Once built, it worked: icons loaded on the emulator (GitHub, Google, Amazon, Microsoft,
  Apple, Wikipedia), with the font glyph while loading and on failure.
- Workaround: two more `AndroidMavenLibrary` items in `src/App/App.csproj`. On iOS, SwiftUI's `AsyncImage` needed
  nothing.

### G19. A value that changes every second in each row re-sends whole rows, one turn per row (friction)
- Trying to: keep the verification-code drill-down's per-row countdown and code (GroupingsPageTOTPListItem ticks every
  second through `TotpTickAsync`, which posts one `BeginInvokeOnMainThread` per row and sets three properties).
- What happened: the only per-row channel is the row itself, so every tick replaces every TOTP row (`Update` per row,
  equal rows skipped), each in its own main-loop turn because the view model posts per item. It works and stays
  correct: codes roll over at the period, the 60-second code ticks independently, the ring turns red under 7 seconds,
  and ticking stops when the page is left (CPU back to 0%). Cost grows with the row count: iOS simulator (x86_64 under
  Rosetta) about 20% CPU with 84 rows ticking; Android emulator 2–4% with 4 rows and 73–120% with 84 rows. The host
  Mac was heavily loaded (load average 20–200 from other sessions) during the Android numbers, so they are only an
  indication, and the split between OAHU delivery, Compose recomposition and the view model's own TOTP work was not
  measured.
- Workaround: none needed for correctness. The better OAHU shape would be a row carrying the period and code, with the
  countdown drawn natively from the clock (`TimelineView`, a `LaunchedEffect` ticker), or one `IOahuState` of "seconds
  left per period" shared by all rows; both change what the existing view model does. The item view does it the cheap
  way already: the code is its own `IOahuState<TotpDisplay?>`, so a tick re-sends 6 fields, not the whole item.

### G20. Several pushed instances of one contract under one Android activity share a saved-state key (friction, inferred)
- Trying to: push GroupingsPage three deep (Types > Login, a folder, a nested collection), each with its own view
  model and `OahuView<IGroupingsViewModel>`.
- What happened: it worked on both platforms (each level loaded its own rows; Back restored the one below). But
  `native-screens.md` says every ID-less `ComposeView` under one owner shares a saved-state key and asks for "a positive
  Android view ID declared in the app's resources" per logical screen instance. A drill-down has no fixed depth, so a
  fixed set of declared IDs doesn't fit, and `View.generateViewId()` is what the doc warns against. Each Compose screen
  here uses `rememberLazyListState()` (saveable), so the scroll position of covered levels is what would be mixed up
  after a recreation; MAUI drops that bundle anyway (`maui.md`), so nothing visible happened. Not set.

### G21. The icon font and its glyphs are copied into each native library (papercut)
- `bwi-font.ttf` now exists three times (`src/Core/Resources/Fonts`, the Swift package's `Resources`, the Gradle
  module's `res/font/bwi_font.ttf`), and the screens' own buttons (copy, eye, launch, download, paperclip, collection)
  repeat `BitwardenIcons` code points in `BitwardenTheme.swift` and `BitwardenPalette.kt`. Row glyphs come from C#
  (`Glyph` fields). On iOS the font must be registered in code (`CTFontManagerRegisterFontsForURL` with `Bundle.module`,
  as `hosting.md` says); it rendered on both platforms. A font from the MAUI app (`MauiFont`) can't be reused, and there
  is no OAHU place to put assets both native libraries share.

### G22. Nothing in OAHU marks a value as sensitive; keeping secrets off the native side is contract design (doc)
- Trying to: decide whether passwords, card numbers, security codes and hidden custom fields must cross before the
  user reveals them, and whether OAHU logs or records them.
- What happened: they need not cross. `SecretValue(Masked, Revealed, ...)` carries the mask only, and the value (as
  runs of letters, digits and symbols for coloring) only while shown; copy happens in C# by id. Verified on both
  platforms: the eye button reveals and re-hides; on a re-prompt item (`Bank of Example`) the master-password dialog
  came first. Checked: the app's stdout and the simulator's unified log for the run contained none of the fake secrets
  (`B4nk-Vault#9`, `ghp_FAKE...`, the card number); `debugging.md` says `OahuDiagnostics` sends metadata only and a
  session recording holds what native code sent (here only keys, ids and indexes), not what C# sent. Not covered
  anywhere in the docs: that a revealed value stays in the native model's memory until replaced (as in MAUI); that C#
  preview fixtures (`[OahuPreview]`) write their captured values into generated Swift/Kotlin catalogs, which a team
  would commit (not tried here); and that a fault message might include a value (not observed). A short "sensitive
  values" section in `contracts.md` or `debugging.md` would have saved the reading.
- Also seen: the TOTP code is shown on the item view before re-prompt, exactly as MAUI shows it.

### G23. A view model's nested item view models don't reach `FromProperty` (papercut)
- `CipherDetailsPageViewModel.Fields` holds `HiddenCustomFieldItemViewModel`s, which raise `ShowHiddenValue` on
  themselves. The details state is `OahuState.FromProperty(this, [...])` over the page view model, so a toggle would not
  re-publish. `ToggleField` awaits the field's toggle and then raises `PropertyChanged(nameof(Fields))` on the page view
  model (`CipherDetailsPageViewModel.Oahu.cs`). The INPC guide covers item notifications only for `FromCollection`.

### Probed and fine
- **Heterogeneous rows**: one union with four cases (`SectionHeader`, `Grouping`, `CipherItem`, `TotpItem`) in one
  keyed list; Swift `switch` and Kotlin `when` are exhaustive. No case names collided with SwiftUI/Compose names.
- **Pull to refresh with a flag that outlives the call**: `Refresh()` sets `Refreshing` before its first await;
  with "sync on refresh" the sync runs on a thread-pool thread and the flag clears only when `syncCompleted` reloads the
  page. `OahuWait.run { model.refresh() } until: { !model.isRefreshing }` held the iOS spinner for the whole sync and
  `PullToRefreshBox` did the same on Android; the renamed and added items appeared (both platforms, also with 2,014
  rows on iOS).
- **Background-thread reloads**: the fake sync announces `syncCompleted` from a pool thread
  (`OAHUDEBUG sync completed on thread 10 (main: False)`); the page marshals the reload, the view model's
  `PropertyChanged` for `Loading`/`ShowNoData` etc. go through `FromProperty`, and `GroupedItems` is replaced on the
  main thread, so no OAHU fault. The projection still hops to the main thread if `CollectionChanged` ever arrives off it.
- **One view model class, many pushed instances**: three GroupingsPage levels deep and back on both platforms (G20 is
  only about Android saved state).
- **Large list**: 2,020 ciphers. iOS: the 2,013-row Logins page appeared within a second of the tap and scrolled with
  icons loading; pull to refresh reconciled it. Android: rows ready 1.7 s after the push (28 ms of that in the
  projection); scrolling was janky (`gfxinfo` 90th percentile 250 ms), but on a Debug build in an emulator on a host at
  load average 20–200, so not attributable. Most of the first-load time on both is the app decrypting 2,020 fake ciphers.
- **Images by URL**: `AsyncImage` (iOS) and Coil (Android, after G18) with the icon-font glyph as placeholder and on
  error; `icons.bitwarden.net` was reached for the fake hosts.
- **Custom icon font**: rendered on both platforms (G21 is the duplication).
- **App themes on the second and third screens**: Nord applied to the list and item view on both platforms (G11, G16).
- **Screen-capture protection**: with the app's "allow screen capture" off, Android sets `FLAG_SECURE` on the
  activity's window; `adb exec-out screencap` returned an all-black image while the native vault list was showing
  (verified). iOS has no equivalent in this app (`SetScreenCaptureAllowedAsync` is a no-op there), with or without OAHU.
- **Row context menu**: SwiftUI `.contextMenu` (View, Edit, Options) and a Compose long-press `DropdownMenu`; Edit opened
  MAUI's edit page and Options MAUI's action sheet (both platforms). The iOS `.swipeActions` is built but was not
  exercised. Richer per-row actions (Copy password, Launch) would need the action list per row in the contract,
  because `AppHelpers.CipherListOptions` builds and dispatches them through a MAUI action sheet by localized title.
- **MAUI views over a native screen**: the Android floating add/edit buttons and the account switcher overlay stayed
  MAUI views in the page's `AbsoluteLayout`, over the `OahuView`; the FAB drew and worked (the overlay was not opened).
- **MAUI dialogs from native intents**: options sheet, vault-filter sheet, password re-prompt, toasts, the edit and add
  pages and the password history page, all from native taps.

### Not OAHU (found while testing)
- Android: any page with a `Picker` (the Generator tab, built when `TabsPage` loads) crashed with
  `HandlerNotFoundException: Unable to find a IElementHandler corresponding to Microsoft.Maui.Controls.Picker`. The
  app's MAUI 8 workaround replaced the `Picker` handler with one registered for `IPicker`; MAUI 11 looks handlers up by
  the view's type. Fixed by registering it for `Picker` too (`src/Core/MauiProgram.cs`). A step 1 (MAUI 11) leftover.
- The custom-field item view models and the Android device action service captured the real event and state services
  at start, so the debug route can't fake them by replacing the registration (the route repoints the device action
  service's state by reflection, DEBUG only).
- iOS shows the account avatar toolbar item as a blank white circle for the fake account (Android draws "AE").
