# OAHU gap log

A running list of the places where adopting OAHU in this app needed something the docs or tools did not give: a doc
that was wrong or unclear, a missing feature, a failing tool, or an app-side shim. Later steps append here. Each entry
names the step that found it.

Severity: **blocker** (no app-side way past it), **friction** (cost real time or needed a shim), **papercut** (small,
annoying), **doc** (the docs were wrong, missing or misleading). "Verified" means seen on a simulator, emulator or in
build output; "inferred" means read from code or docs and not exercised.

OAHU used in steps 2–5: preview `0.1.0-preview.g104c58c2` (AdamE.Oahu commit `104c58c2`, origin/main on 2026-11-25),
from the local feed `~/RiderProjects/oahu-feed`. Step 7 moved to `0.1.0-preview.gc63d0aba` (commit `c63d0aba`,
2026-11-26) and retested the gaps ([07-retest.md](07-retest.md)); each gap's title links its AdamE.Oahu issue.

## Step 2 (folders)

### G1. `oahu init` and the MAUI guide assume pages and view models live in the app project (friction) [#931](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/931)
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
- Seen again in step 4: the pages in `src/Core` can't name the app's Android resources, so the saved-state view IDs
  `native-screens.md` asks for (`Resource.Id.…`) are declared in `src/App/Platforms/Android/Resources/values/ids.xml`
  and looked up by name at run time with the discouraged `Resources.GetIdentifier`
  (`src/Core/Utilities/OahuAndroidIds.cs`). The contracts project can't reference `Bit.Core` either (G24).
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): init still adds OAHU to `src/App` only, and on the upgrade it left `Oahu.Maui` 104c58c2 in `src/Core/Core.csproj` and `Oahu` 104c58c2 in `src/Core.Contracts/Core.Contracts.csproj`; `doctor` and `versions --check` reported the new version everywhere and said nothing (G38). Two lines by hand.

### G2. `init`'s Compose packages collide with the app's pinned AndroidX packages (friction) [#932](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/932)
- Trying to: run `oahu init` on an app that pins older AndroidX bindings.
- What happened: the Android restore inside `init` failed: `error NU1605: Warning As Error: Detected package
  downgrade: Xamarin.AndroidX.AutoFill from 1.3.0.4 to 1.1.0.18 ... App -> Xamarin.AndroidX.Compose.UI 1.11.3.1 ->
  Xamarin.AndroidX.Compose.UI.Android 1.11.3.1 -> Xamarin.AndroidX.AutoFill (>= 1.3.0.4)`. `init` reported it clearly,
  succeeded anyway and printed the align command to run later, as `Oahu.Cli.md` says it does.
- Workaround: raised `Xamarin.AndroidX.AutoFill` to 1.3.0.4 in `src/App/App.csproj` and `src/Core/Core.csproj`, then
  reran `init`. Behavior change of that bump is untested (autofill service).

### G3. First Android build fails OAHUVER001 after `init` said it was aligned (friction) [#933](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/933)
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
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): the upgrade's init aligned (171 constraints) and the first Android build had no OAHUVER error; the app already pins Jdk7/Jdk8, so the original case was not re-run.

### G4. `oahu generate` silently generates nothing until a screen file exists (friction) [#934](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/934)
- Trying to: generate the Swift and Kotlin models after writing the contracts, to see their API before writing screens.
- What happened: `dotnet tool run oahu generate --project src/App/App.csproj --framework net11.0-ios` printed
  `oahu-gen: : 0 file(s) written or removed.`; `--json` reported `"current": true, "files": []`, no diagnostics.
  Generation only happens for contracts that have a `{Name}.oahu-screen.swift`/`.kt` file
  (`docs/consumer/oahu-gen.md`: "one provider per screen source it discovers"), so the model a screen is written
  against does not exist until a screen file referring to it exists. Nothing in the output says "2 contracts found,
  0 screens; create Folders.oahu-screen.swift". `init`'s "Next for ios" line names the file convention but not the
  consequence.
- Workaround: wrote the screen files blind from the docs' member mapping, then generated, then corrected them.

### G5. A keyed-list row without `IOahuKeyed` gives a plain compiler error, not an OAHU one (papercut) [#935](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/935)
- `public sealed record FolderRow(string Key, string Name);` used in `IOahuKeyedList<FolderRow>` fails with
  `CS0311 ... There is no implicit reference conversion from 'FolderRow' to 'Oahu.IOahuKeyed'`. `contracts.md` does
  say rows implement `IOahuKeyed`, so this is only a discoverability point. Fixed by adding `: IOahuKeyed`.
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): unchanged: removing `IOahuKeyed` gives the same bare `CS0311`. contracts.md now names CS0311 beside the row rule (#935 closed as documentation). Verified.

### G6. Native intents land on the view model, but this app navigates from page code-behind (friction) [#936](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/936)
- Trying to: keep the MAUI page's navigation (row tap pushes `FolderAddEditPage` modally; Android's add button) while
  the tap happens in the native screen.
- What happened: contract methods are called on the bound view model. In this app (as in many Xamarin.Forms-era apps)
  navigation lives in the page's event handlers (`RowSelected`, `AddButton_Clicked`, with a `DoOnce` debounce), not the
  view model. Neither `maui.md` nor `maui-migration.md` covers routing a native intent back to the page.
- Workaround: the view model's contract half raises plain C# events (`FolderSelected`, `AddRequested`) that the page
  subscribes to (`src/Core/Pages/Settings/FoldersPageViewModel.Oahu.cs`, `FoldersPage.xaml.cs`). Small, but it is a
  shim every such page will need.
- Seen again in step 5: the TOTP camera buttons are native, but the scan page is pushed from `ScanTotp_Clicked` in the
  page, so the view model raises `ScanTotpRequested` (`CipherAddEditPageViewModel.Oahu.cs`). The rest of add/edit's
  navigation already lived in the view model (generator, prompts, action sheets).
- Seen again in step 3: both pages navigate from code-behind (`RowSelected`, `AddButton_Clicked`,
  `PasswordHistory_Tapped`), so `GroupingsPageViewModel.Oahu.cs` raises `ItemSelected`/`AddRequested` and
  `CipherDetailsPageViewModel.Oahu.cs` raises `PasswordHistoryRequested` for the pages to handle. `RowSelected` became
  `SelectItemAsync(IGroupingsPageListItem)` so the old body is reused (`src/Core/Pages/Vault/GroupingsPage/GroupingsPage.xaml.cs`).

### G7. Static labels need a "copy" record per screen (papercut) [#937](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/937)
- The native screens cannot read the app's `AppResources` resx, so "There are no folders to list.", "Add Folder" and
  "Name" go through `IOahuState<FoldersCopy>`/`IOahuState<FolderAddEditCopy>` (made with `new StateSource<T>(...)`).
  This is exactly what `docs/consumer/localization.md` prescribes, and it worked; it is boilerplate per screen, and the
  app's own culture handling (`SetCulture()`) and `OahuCulture` were not reconciled (not checked: a non-English UI).
- Seen again in step 3: `GroupingsCopy` (9 strings) and `CipherDetailsCopy` (32 strings) are copy records again.
  Two app-specific twists: GroupingsPage upper-cases section titles on iOS only (`#if IOS ... ToUpperInvariant`), so
  platform presentation is decided in C# and arrives finished; and the `Filter` key the XAML used
  (`{u:I18n Filter}`) does not exist in `AppResources.resx` (MAUI showed the key itself), found only because the copy
  record needed a C# property (`FilterByVault` used instead; an app bug, not OAHU).
- Seen again in step 5: `CipherAddEditCopy` has 54 strings and the four fixed option lists.

### G8. `init` derives an odd Kotlin package and generic names (papercut) [#938](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/938)
- For `src/App/App.csproj`, `init` chose Kotlin package `com.oahu.appapp`, Gradle root `oahu-native`, module
  `oahu-screens` and Swift package `OahuScreens`. The doubled `appapp` looks like a bug in name derivation. There is no
  init option to choose them. Kept as generated.
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): unchanged (`com.oahu.appapp`).

### G9. `init` writes machine-relative feed paths; the guide uses environment variables (doc) [#939](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/939)
- `maui-migration.md` ("Set up the feed and folders") tells you to export `OAHU_FEED`/`OAHU_VERSION` and use
  `%OAHU_FEED%/nuget`, `System.getenv("OAHU_FEED")` and an absolute path in `Package.swift`. `init` instead wrote
  relative paths into committed files: `nuget.config` (`../oahu-feed/nuget`), `Package.swift`
  (`../../../../../../../oahu-feed/swift/0.1.0-preview.g104c58c2/OahuRuntime`) and `settings.gradle.kts`
  (`../../../../../../oahu-feed/maven`). These work only when the feed sits beside the repository. The guide says "If you
  use the CLI, use its generated MSBuild and native build files", so both are "right", but the two setups differ more
  than the guide suggests. `init` also wrote the `nuget.config` line with different indentation from its neighbours.
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): init still writes file-relative paths; maui-migration.md now says so and how to move the feed (#939). Matches.

### G10. `Oahu.Maui` in a library shared with iOS app extensions links the extensions against OahuRuntime (friction, inferred) [#940](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/940)
- `src/Core` is also referenced by the three iOS extensions (autofill, action, share). Because `Core` now references
  `Oahu.Maui` (G1), each `.appex` binary links `@rpath/OahuRuntime.framework/OahuRuntime` (seen with `otool -L`); it
  resolves through `@executable_path/../../Frameworks` to the host app's copy. The extensions were not launched, so it
  is not known whether they load, nor what OAHU costs inside the autofill extension's memory limit. Nothing in the docs
  covers app extensions. A cleaner split would keep OAHU out of the library the extensions use, which this app's
  layout (pages in `Core`) does not allow without moving pages.

### G11. The app's own theme does not reach native screens (friction) [#941](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/941)
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
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): the palette is now `OahuColor` (Styling, #1021/#1022) and a live theme change while the folders screen was open re-themed it on iOS and Android (verified, screenshots). Each root contract still declares it (G16).

### G12. A MAUI toolbar action cannot flush a Compose text field's pending edit (friction, inferred; not reproduced) [#942](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/942)
- Save is a MAUI toolbar item; the name field is a Compose `TextFieldState`. `native-screens.md` says text during an
  IME composition is not proposed until the composition ends, and `rememberTextFieldBinding().commit()` sends pending
  text, but only native code can call `commit()`. Tapping MAUI's Save while Gboard is still composing a word could save
  the previous text. Not reproduced: `adb shell input text` commits immediately, and the save in the run had the full
  text. The keyboard's Go key does call `commit()` before `submit()`.
- Step 4, tried for real and **not reproduced on Android**: folder edit page (step 2), emulator `OAHU_Pixel_API36` with
  Gboard. Tapped the field, tapped Gboard's space key, then typed `p`, `l`, `a`, `n` by tapping the on-screen keys
  (Gboard showed "plan / plans / planner" as suggestions for the word being composed), and tapped MAUI's **Save**
  without ending the word. The list then showed **"Work plan"**: the composing word was saved. Whether the binding
  proposed it while composing or the toolbar tap finished the composition first was not instrumented (the folders
  route has no diagnostics). The generator and lock screens have no MAUI action that reads a text field (Select reads
  the generated value; Unlock and Go are native and call `commit()`).
- **iOS not tried with marked text**: the simulator's typing goes in through the hardware keyboard and no Japanese
  keyboard was installed on "BW OAHU iPhone". The screens use SwiftUI `TextField`/`SecureField`, which propose each
  keystroke; OAHU's own `OahuTextField` (which keeps marked text local, so would be the risky one) is not used. Still
  open on iOS.
- **Step 5, tried on both platforms with real composition: not reproduced** (no text lost). The add/edit Save is a
  MAUI toolbar item and reads the `CipherView` directly (DEBUG log of what `SubmitAsync` saw).
  - iOS 18.6 simulator ("BW OAHU Step5 iPhone", a Japanese Romaji keyboard added with `defaults write -g
    AppleKeyboards`): tapped the name field of a secure note, switched to Japanese, tapped `k a n a` on the on-screen
    keyboard (marked かな with candidates showing, underlined), then tapped MAUI's **Save**. Saved name:
    `Server notesかな`. Diagnostics show why: the SwiftUI `TextField` proposes the marked text on every keystroke
    (edits for `…k`, `…か`, `…かn`, `…かな`), so C# already has the unconfirmed composition. Nothing was lost, but the
    view model sees, and Save stores, text the user hasn't confirmed, including romaji fragments if Save comes
    mid-syllable.
  - Android (`OAHU_Pixel_API36`, Gboard): typed `line`, Enter, then `p l a n` by tapping keys (Gboard showing "plan /
    plant / planet" for the word being composed) and tapped **Save**: saved `line\nplan`. Nine `Notes` edits for nine
    characters: oahu-compose proposes the composing text too.
  - iOS with predictive text pending (typed "Strete", QuickType showing it) then Save: saved as typed.
  - A new limit found instead (G35): on Android the window pans to keep a low field above the keyboard, which pushes
    MAUI's toolbar, and so Save, off screen until the keyboard closes.

### G13. A whole-page iOS screen stops at the bottom safe area (papercut) [#943](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/943)
- The SwiftUI `List` background ends above the home indicator, leaving a strip of the MAUI page's white background
  below it (visible in the grouped-list screenshots). MAUI lays the `OahuView` out inside the safe area; the screen
  cannot extend under it. Not fixed.
- Not seen in step 3: the vault list sits above the tab bar and the item view's background fills to the bottom on
  both themes tried.
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): fixed by following maui.md "Whole-page safe areas" (page `UseSafeArea` off, `SafeAreaEdges="None"` on the page, the `AbsoluteLayout` and the `ContentView`). With the MAUI page painted red, the old settings leave a red strip under the list and the recipe leaves none (iOS, verified). Android inconclusive (the Compose folder screen draws no background).

### G14. `oahu doctor` text output is mostly Gradle resolution lines (papercut) [#944](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/944)
- Without `--json`, `doctor` printed over a hundred `Gradle resolved androidx.compose...` lines before the one warning
  that mattered (`OAHUCLI011`, no emulator running). `--json` is clean.
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): fixed: `doctor` prints `warning OAHUCLI005: …` with a `Try:` line first, then summary counts (verified).

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

### G15. Keyed lists have no sections, and `FromCollection` can't key a row by its section (friction) [#947](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/947)
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

### G16. No value is shared by every screen: each contract re-declares theme and glyphs (friction) [#948](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/948)
- Trying to: draw both new screens (and, later, every converted page) in the app's own theme.
- What happened: OAHU has no ambient or app-wide state reaching screens (`maui.md`, "Known limits": no environment
  values), and a contract can't extend a base interface (OAHU001), so "every screen gets the palette" is one
  `IOahuState<AppPalette> Palette` member per contract, one line per view model implementation, and one
  `PaletteTheme`/`.environment` wrapper per screen. The step 2 folder screens don't have it, so the app now has two
  looks for one theme setting. The same applies to anything app-wide (the icon-font glyphs the screens' own buttons
  use, G21; a font scale; a "screen capture allowed" flag).
- Workaround: one shared `StateSource<AppPalette>` (`src/Core/Utilities/OahuPalette.cs`) returned by every contract.
  It works (G11), but nothing makes a new contract include it.
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): all 7 root contracts now carry `IOahuState<AppPalette> Palette` (the two folder contracts added in this step), typed with `OahuColor`. Mounted sections (D25) read it from the parent's environment with no member. D26's revisit condition ("most screens of the Bitwarden conversion carry the palette") is met.

### G17. Adding a resource or a library means editing files `init` manages, and `init` then refuses to run (friction) [#949](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/949)
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
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): unchanged, and a second cause (G37): the upgrade's init refused Package.swift and build.gradle.kts (`OAHUCLI001` only in `--json`), then gradlew.bat, which the app never edited.

### G18. The Coil recipe in `hosting.md` doesn't cover Coil's Compose `AsyncImage` (doc) [#950](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/950)
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

### G19. A value that changes every second in each row re-sends whole rows, one turn per row (friction) [#951](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/951)
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

### G20. Several pushed instances of one contract under one Android activity share a saved-state key (friction, inferred) [#952](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/952)
- Trying to: push GroupingsPage three deep (Types > Login, a folder, a nested collection), each with its own view
  model and `OahuView<IGroupingsViewModel>`.
- What happened: it worked on both platforms (each level loaded its own rows; Back restored the one below). But
  `native-screens.md` says every ID-less `ComposeView` under one owner shares a saved-state key and asks for "a positive
  Android view ID declared in the app's resources" per logical screen instance. A drill-down has no fixed depth, so a
  fixed set of declared IDs doesn't fit, and `View.generateViewId()` is what the doc warns against. Each Compose screen
  here uses `rememberLazyListState()` (saveable), so the scroll position of covered levels is what would be mixed up
  after a recreation; MAUI drops that bundle anyway (`maui.md`), so nothing visible happened. Not set.
- Step 4, checked for real on Android: the new screens set `OahuView.AndroidSavedStateViewId` (generator tab, generator
  modal, lock page: declared IDs looked up by name, G1); the vault drill-down stays ID-less. With four ID-less
  `GroupingsScreen`s open (vault-large, three levels deep) plus the generator, sending the app to the background and
  killing the process (`am kill`), then reopening: no fault, no `Wrong state class`, nothing restored. The app starts
  from scratch (the debug route re-runs from the intent, every level reloads at the top), so neither a leak between
  instances nor a lost state is observable; MAUI drops the bundle anyway (`maui.md`). Recreating only the activity was
  not achieved: "Don't keep activities" (`always_finish_activities 1`) did not take effect on the running emulator,
  so that case is still inferred. Each new LockPage reuses the one `oahu_lock` ID; the previous lock screen had always
  closed first (diagnostics show `ScreenCloseFinished Lock` before the next `ScreenOpening Lock`), as `maui.md`
  requires.
- Step 5: add/edit sets a declared `oahu_cipher_edit` ID; it is modal and only one is open at a time.

### G21. The icon font and its glyphs are copied into each native library (papercut) [#953](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/953)
- `bwi-font.ttf` now exists three times (`src/Core/Resources/Fonts`, the Swift package's `Resources`, the Gradle
  module's `res/font/bwi_font.ttf`), and the screens' own buttons (copy, eye, launch, download, paperclip, collection)
  repeat `BitwardenIcons` code points in `BitwardenTheme.swift` and `BitwardenPalette.kt`. Row glyphs come from C#
  (`Glyph` fields). On iOS the font must be registered in code (`CTFontManagerRegisterFontsForURL` with `Bundle.module`,
  as `hosting.md` says); it rendered on both platforms. A font from the MAUI app (`MauiFont`) can't be reused, and there
  is no OAHU place to put assets both native libraries share.

### G22. Nothing in OAHU marks a value as sensitive; keeping secrets off the native side is contract design (doc) [#954](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/954)
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
- Seen again in step 4, sharper: the master password and PIN are editables, so every keystroke crosses as an edit
  (as the MAUI `Entry` binding did) and a session recording (`docs/session-replay.md`, §1: `execute` entries hold the
  argument bytes; "OAHU never starts a recording ... and redacts nothing") would hold the typed master password,
  the PIN and the forwarding-service API key. There is no per-member opt-out, so an app that records sessions in
  Debug must not record on the lock page at all. See G27. `OahuDiagnostics` stayed metadata-only (verified: with
  `BW_OAHU_DIAG=1` the logs show `MasterPassword Succeeded 0.2ms` per keystroke, no values; the iOS console logs of all
  runs contain none of the typed secrets).
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): contracts.md "Sensitive values" now says what this entry had to work out (#954, documentation). The cost is unchanged.

### G23. A view model's nested item view models don't reach `FromProperty` (papercut) [#955](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/955)
- `CipherDetailsPageViewModel.Fields` holds `HiddenCustomFieldItemViewModel`s, which raise `ShowHiddenValue` on
  themselves. The details state is `OahuState.FromProperty(this, [...])` over the page view model, so a toggle would not
  re-publish. `ToggleField` awaits the field's toggle and then raises `PropertyChanged(nameof(Fields))` on the page view
  model (`CipherDetailsPageViewModel.Oahu.cs`). The INPC guide covers item notifications only for `FromCollection`.
- Seen again in step 5, at scale: the add/edit XAML bound about 40 two-way paths into the mutable `CipherView`
  (`Cipher.Login.Username`, `Cipher.Card.Number`, `Cipher.Identity.City`, ...), none of which notify. Each became an
  `OahuEditable.FromProperty(this, nameof(Cipher), get, set)` whose getter and setter walk the path; edits settle by
  reading back, and the view model's own `TriggerCipherChanged` (type change, generated password, scanned key) re-reads
  all of them. It works, but `FromProperty` takes one property name, so every editable must watch `Cipher` and
  re-read on any cipher change. The URI rows (`LoginUriView`, no INPC) need `Refresh()` wherever the view model
  changes one, including inside the existing `async void UriOptions` after its action sheets
  (`CipherAddEditPageViewModel.cs`, one line).

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

## Step 4 (generator, lock)

Pages: the generator (`GeneratorPage`, as the Generator tab and as the modal add/edit opens with Select) and the lock
page (`LockPage`). Same OAHU preview. Details in [04-generator-lock.md](04-generator-lock.md). Host load during the
runs: load average 7–38 from other sessions; timings below are indicative only.

### G24. A contract can't use the app's own enums, so each picker's enum is mirrored and mapped (papercut) [#956](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/956)
- Trying to: bind the generator's pickers (`GeneratorType`, `UsernameType`, `UsernameEmailType`,
  `ForwardedEmailServiceType`, all in `Bit.Core.Enums`) as `IOahuEditable<TEnum>`.
- What happened: the contracts project must be a plain library oahu-gen can read (`docs/consumer/contracts.md`,
  `oahu-gen.md`); the enums live in `src/Core`, a MAUI library that references the contracts, so the contracts can't
  reference it back. Nothing in the docs says what to do with domain types that live with the view models.
- Workaround: five mirror enums in `src/Core.Contracts/Generator.cs` (`ForwardedService` also mirrors the app's
  `None = -1`, which crosses fine because enums cross by position) and a by-name `Map<T>` in
  `GeneratorPageViewModel.Oahu.cs`. A renamed member in either enum fails at run time (`Enum.Parse`), not compile time.
- Avoided in step 5: add/edit's pickers were already `*SelectedIndex` properties over `List<KeyValuePair<...>>`, so
  the contract has `IOahuEditable<int>` per picker plus the labels as `IReadOnlyList<string>`; no enum crosses
  (`CipherType`, `FieldType`, `UriMatchType`, `LinkedIdType` stay in C#). The one enum the screen switches on
  (`EditItemKind`) is mirrored.
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): contracts.md "Types declared with the view models" covers it and warns against `Enum.Parse` (#956, documentation). The app's five mirrors are unchanged.

### G25. Picker options with localized labels need one record type per enum (papercut) [#957](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/957)
- Trying to: send each picker's options with their localized names (the XAML used `LocalizableEnumConverter`) and
  only the options the page offers (the service list leaves out `None`).
- What happened: records can't be generic (`contracts.md`, "Records may not be generic"), so there is no
  `Option<T>(T Value, string Label)`. Five records (`GeneratorKindOption`, `PasswordKindOption`, …) carry the same two
  fields. Swift and Kotlin then need one `Picker` call per enum anyway; a generic native helper over them works
  (Kotlin `Picker<T>` takes `List<Pair<T, String>>`), but every option list is converted by hand.
- Workaround: the five records in `Generator.cs`, built from the view model's existing `*Options` lists in
  `GeneratorPageViewModel.Oahu.cs`.
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): contracts.md "Options with labels" shows the per-enum record (#957, ruled: records stay non-generic). Unchanged cost.

### G26. Every control change crosses, including values equal to the current one (papercut, verified) [#958](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/958)
- Trying to: count round trips for the length slider (which regenerates the password as it moves) and for secure text.
- What happened (diagnostics plus a DEBUG counter in the setter):
  - iOS SwiftUI `Slider` over `Binding(get: Double(model.length), set: { model.length = Int($0.rounded()) })`: one
    edit per touch sample (11 edits for a drag from 14 to 82 in ~1 s; the first and last proposed the value it already
    had). Each `Edit` took 0.5–2 ms in C#, generation included.
  - Android Compose `Slider` with `collectAsMutableState()`: one edit per `onValueChange`, 75 for a 2-second drag
    across 70 values (`82.49 -> 82` was sent although 82 was current). With the policy clamp applied during the drag
    (`BW_OAHU_CLAMP_LIVE=1`), a 4-second drag below the minimum sent **201** edits, all answered 20.
  - iOS `SecureField`/`TextField` on the lock page: 12 `MasterPassword` edits for the 5 characters of "wrong" (about two
    per keystroke), plus edits on focus changes and after `Submit` cleared the field. Android
    `OutlinedSecureTextField`: one per character.
- Nothing is wrong with the answers (see "Probed and fine"), but an editable whose setter does real work (here: save
  options, normalize, regenerate a password, and in MAUI a history write on release) runs it for every sample. The
  model setter doesn't skip a proposal equal to the current value, and neither `native-screens.md` nor `editable.md`
  says how often a continuous control proposes or suggests coalescing. Not changed: the MAUI slider also set the bound
  property on every move.
- **Step 5, at form scale** (diagnostics, load average 64–146 on the host, so only counts are meaningful):
  - iOS: every SwiftUI `TextField` sends about two edits per keystroke plus two when it gains focus (Name: 40 edits for
    "Type switch" and two Returns; a vertical notes field: 70 edits for 30 characters). Row text methods
    (`SetFieldText`, `SetUriText`) come in the same pairs. The identity record editable: 10 record edits for focusing a
    field and typing one letter, each carrying all 17 strings.
  - Android: one edit per keystroke for text fields, rows and the identity record (13 record edits for 13 characters),
    none on focus.
  - C# time per edit: 0.2–1.3 ms on iOS, 0.3–4.3 ms on Android (Debug, x86_64 simulator under Rosetta, loaded host).
    Only the edited member is re-read; the 18 editables watching `Cipher` re-read only on the view model's own
    `TriggerCipherChanged`.
  - No typing lag attributable to OAHU: on iOS every screen change (keyboard, caret, typed text, MAUI alerts too) showed
    0.7–3 s late at load average 70–100, on all pages alike. On Android, opening the organization form with 2,000
    collection rows took about 20 s to draw (my non-lazy `Column` of 2,000 switches; `ScreenOpenFinished` was 481 ms),
    after which typing and toggling a row (3.2 ms in C#) were immediate.
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): native-screens.md now says every write is a proposal (#958, documentation). New count: `OahuTextField` with `isSecure:` sends one edit per keystroke and none on focus on iOS (lock page), against about two plus two on focus for SwiftUI fields.

### G27. A session recording would capture every keystroke of a secret editable, with no way to exclude a member (friction, security, inferred) [#959](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/959)
- Trying to: decide whether the lock page can be recorded for a replay test (`docs/consumer/debugging.md`,
  "Recording a session"; `docs/session-replay.md`).
- What happened: by the docs, each edit is recorded as an `execute` entry with its argument bytes, and the recorder
  "redacts nothing". The lock contract's `MasterPassword` and `Pin` editables, and the generator's `ApiSecret`, are
  edits. `debugging.md` warns that recordings hold what the user typed, but there is no attribute or option to keep a
  member's values out (a `[OahuSensitive]` that records the edit with empty bytes would do). Not run: recording was not
  turned on in this app.
- Workaround: none in code; the step log says never to record on the lock page. Seen with G22.
- Seen again in step 5, wider: the login password, TOTP key, card number and code, hidden custom field values and the
  identity section all cross per keystroke. Because the identity section is one record editable (G31), typing one
  letter of the city sends all 17 identity strings, SSN and passport number included. Not recorded.

### G28. Secure text on iOS: show/hide swaps fields and loses focus; OAHU's text field has no secure mode or focus (friction, verified) [#960](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/960)
- Trying to: keep MAUI's `MonoEntry` behavior: one field whose `IsPassword` follows the eye button, keeping text,
  focus and cursor; the view model then asks for focus with the cursor at the end (`TogglePassword` raises
  `FocusSecretEntry(secret.Length)`).
- What happened:
  - iOS: SwiftUI has no secure toggle on one field, so the eye swaps a `SecureField` for a `TextField`. The text stayed
    (it is the model's), focus did not. On the lock page the C# focus event put focus back (verified: keyboard stayed
    up after the toggle), but SwiftUI has no API to place the cursor, so `SecretFocus.Cursor` is ignored. On the
    generator's API key field, which has no focus event in MAUI either, the toggle dismissed the keyboard (verified).
    `OahuTextField` (OAHU's UIKit-backed field) has `keyboard:` options but no `isSecureTextEntry` and, per
    `native-screens.md`, isn't for "SwiftUI focus management", so it doesn't help. iOS also clears a secure field's text
    when typing resumes after it lost focus (seen: "wrong" was replaced, not appended), as MAUI's `Entry` does.
  - Android: one `OutlinedSecureTextField` whose `textObfuscationMode` follows the eye kept text, focus, keyboard and
    cursor (verified), and the cursor request maps to `state.edit { selection = … }`.
- Workaround: the focus event (see "Probed and fine") restores focus on iOS. Not an OAHU core-design issue: SwiftUI's
  field model; but OAHU's own text field could carry a secure mode and a focus binding.
- Seen again in step 5 (iOS, verified): the eye of a hidden custom field row swaps the field, and setting the row's
  `@FocusState` in the same button action does not bring focus back (the new field doesn't exist yet), so the keyboard
  closes. iOS also cleared a hidden row's 16-character value when typing resumed after a refocus (typed "Z" replaced
  it), as a secure `UITextField` does. Android kept focus and appended.
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): `OahuTextField(…, isSecure:)` (#982) on the lock page and the add/edit secrets keeps focus, keyboard and cursor across show/hide (cursor moved, hidden, typed, shown: the character landed at the cursor), and a hidden custom field row kept focus and appended. SwiftUI `.focused()` on an `OahuTextField` worked for the C# focus requests. Still missing: placing the cursor from the view model (G41). Verified on iOS.

### G29. A first tap on a native text field sometimes doesn't focus it on iOS (friction, inferred cause) [#961](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/961)
- Seen three times on iOS (generator word separator on first use, the API key field after the eye toggle, the lock
  page's field after a MAUI alert): the first tap showed no cursor and no keyboard; a second tap focused the field.
  Both pages set `HideSoftInputOnTapped="True"`, whose page-level tap handling ends editing, and the inferred cause is
  that it races the SwiftUI field becoming first responder. Not checked by turning the property off. `maui.md`'s
  "Not measured" list includes keyboard handling for hosted screens. Android did not show it.
- **Step 5: pinned down as display latency, not a missed focus; neither OAHU nor `HideSoftInputOnTapped` causes it**
  (verified on "BW OAHU Step5 iPhone", iOS 18.6 x86_64, load average 70–98):
  - First tap on the username field of a fresh page: the screenshot 0.7 s later showed no caret and no keyboard (the
    step 4 symptom), but diagnostics showed the focus edits (`Username` ×2, the unchanged text SwiftUI sends on focus)
    within 1–2 ms of the tap, and text typed right away went into that field. 3 s after the tap the caret and keyboard
    were there without a second tap.
  - Same run with `HideSoftInputOnTapped` off (`BW_OAHU_HIDESOFT=0`, logged `HideSoftInputOnTapped off`): identical:
    focus edits at the tap, nothing drawn at 0.7 s, caret and keyboard by 3 s.
  - Every later change on this host showed the same lag (typed text appeared 1–2 s after the keystrokes crossed), so the
    "second tap focused it" in step 4 was the first keyboard presentation catching up. Inferred: a slow first
    keyboard presentation on a loaded Rosetta simulator. Not compared with a plain MAUI `Entry` page.
  - One earlier sighting on the shared "BW OAHU iPhone" (a second tap landing focus on the row below) is discounted:
    another agent was driving that simulator at the time (see "Not OAHU" in step 5).

### Probed and fine (step 4)
- **Editables of non-string types**: `int` (slider, steppers), `bool` (toggles), five enums (pickers, iOS menu
  `Picker`, Compose `ExposedDropdownMenuBox`), on both platforms.
- **Clamping and refusals while dragging**: with the policy minimum applied during the drag, the native thumb stayed at
  the answer (20) while the finger moved to 5–7; screenshots every 0.5–1 s during the drag on both platforms showed the
  thumb at 20 every time, no jitter caught. Without it (the app's real behavior), the slider followed the finger to 5
  and **snapped to 20 on release** when `LengthEditingEnded` ran `SaveOptionsAsync` and `LoadFromOptions` set
  `Length` (a method's later change, delivered through `FromProperty`). The password generated during the drag already
  had the policy length (as in MAUI).
- **Clamp inside the edit**: Minimum numbers "−" from 3 with a policy minimum of 3: the stepper stayed at 3 (the setter's
  save completed synchronously, so `LoadFromOptions` ran inside `Edit`). With `BW_OAHU_SLOWSAVE=1` (the save yields 30
  ms) the answer arrived after the edit and the stepper ended at 3, with no fault (the late value comes in through the
  property notification, as `inpc.md` describes for setters that write later). The app's own reentrancy (every
  property `LoadFromOptions` sets saves again) made Minimum special jump 2 → 3 in the synchronous case and not in the
  slow one; the same view-model code runs under MAUI, not compared there.
- **Controls disabled by other state**: the six policy-locked toggles disabled from `Locks`; sections shown and hidden
  by the type pickers, service and email type; the policy banner.
- **Many small states changing together**: one regenerate or one picker change publishes several states (value,
  layout, a dozen option editables via `LoadFromOptions`); no intermediate frame was seen in screenshots, though frames
  were not instrumented.
- **One-shot C# → native signals**: `IOahuEvents` worked for focus (`FocusSecret`: on appearing, after show/hide, after
  the fake biometric prompt's "Master password" fallback; both platforms) and for "copied" (`Copied` → haptic). The
  first focus request is published before the screen subscribes and was still delivered (the held-events rule).
- **Keyboard Go / submit**: SwiftUI `.submitLabel(.go).onSubmit` and Compose `ImeAction.Go` + `onKeyboardAction` (with
  `commit()`) ran the view model's `SubmitCommand`; wrong password → MAUI "Invalid master password" alert over the
  native screen; `debug` unlocked; PIN 1234 unlocked (number pad on iOS has no Go key, as with MAUI).
- **Keyboard avoidance**: on iOS the `Form` scrolled the focused field above the keyboard (word separator, API key);
  Compose screens use `imePadding()`; both fine inside the MAUI page.
- **Rich text**: the coloured value crosses as `TextRun`s (letters, digits, symbols) and is coloured natively
  (`AttributedString`, `AnnotatedString`), replacing MAUI's HTML spans.
- **Same contract two ways**: the generator as a long-lived tab screen (kept across tab switches on Android; its
  options kept, value regenerated on return as MAUI's tab does) and as a per-modal screen with Cancel/Select (MAUI
  toolbar), closed when the modal popped (`ScreenCloseFinished` 10–22 ms).
- **Teardown when `MainPage` is replaced**: unlocking swaps `LockPage` for a new `TabsPage`, and relocking swaps back;
  diagnostics show the lock screen closing on unlock and, on relock, the tab's Generator and Groupings screens closing
  before the new lock screen opened, on both platforms, with no fault.
- **Backgrounding**: relock on resume (debug route watching `Window.Stopped`/`Resumed`) showed the lock page with the
  biometric auto-prompt (a MAUI action sheet standing in for the system prompt) over the native screen on Android and
  iOS. The real Face ID/fingerprint prompt was not shown (simulator not enrolled).
- **Android saved state of secrets**: oahu-compose's text helpers use `remember`, not `rememberSaveable` (read in
  `EditableTextFieldState.kt`: "Not saved across process death"), so a typed master password is not written to the
  saved-instance bundle. Only in KDoc; `native-screens.md` doesn't say it (doc).
- **Autofill/password manager hints**: iOS showed the "Passwords" QuickType bar on the `SecureField`s (and the lock
  field sets `.textContentType(.password)`); Android's password keyboard showed no suggestion strip. Autocorrect off on
  both; iOS emoji key hidden on the separator field with `.asciiCapable`, still present on the shown-password
  `TextField`.

### Not OAHU (found while testing)
- iOS: after a **PIN** unlock the app crashed twice with `SIGSEGV` in `UIBarButtonItem.get_Image()` called from
  `Bit.iOS.Core.Handlers.CustomNavigationHandler` (`src/iOS.Core/Handlers/CustomNavigationHandler.cs`, the avatar
  item's `IconImageSource` handler dispatched after the lock page's bar items were released). The master-password
  unlock path did not crash. Inferred to be the app's handler racing the `MainPage` swap; not compared with the MAUI
  page.
- SwiftUI wrapped long generated values at "word" boundaries and drew a hyphen that isn't in the value
  (`ex-ample.com`, `fragilexdepressxam-enity`). Fixed with a zero-width space after each character
  (`Generator.oahu-screen.swift`); MAUI's HTML label used `word-break: break-all` for the same reason.
- The XAML's `{u:I18n UsernamePromptHelpLink}` key doesn't exist in `AppResources` (MAUI showed the key); the screen uses
  "Learn more".
- DEBUG harness: `MainThread.BeginInvokeOnMainThread` runs inline on the main thread, so the lock route had to post
  with `Dispatcher.Dispatch` (App.Bootstrap casts `platformUtilsService` to its concrete type right after the route
  runs); `AccountsManager` holds the real state service, so the fake lock navigates with `App.Navigate` instead of the
  "locked" message; the generation services hold the real state service, so the route registers new ones.

## Step 5 (item add/edit)

Page: the item add/edit/clone form (`CipherAddEditPage`, 815 lines of XAML). Same OAHU preview. Details in
[05-cipher-edit.md](05-cipher-edit.md). Host load average 64–146 from other sessions during the runs; timings are
indicative only. iOS runs on a simulator created for this step ("BW OAHU Step5 iPhone", iPhone 16 Pro, iOS 18.6,
x86_64), Android on a second read-only instance of `OAHU_Pixel_API36` (port 5640), because other agents were using
the shared ones (see "Not OAHU").

### G30. A large form is one flat contract; there is no way to split it into sections or nest view models (friction) [#973](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/973)
- Trying to: map the add/edit view model, which nests `CipherView` → `LoginView`/`CardView`/`IdentityView` and lists
  of `LoginUriView` and custom field item view models, to a contract, ideally one per section.
- What happened: contract members are only the four stream types and methods, declared on the contract itself; "Base
  interfaces, generic contracts ... are unsupported" (`contracts.md`, "Members"), so no section interface can be
  shared or composed, and a member can't be another contract. The only grouping is a record: a record editable for a
  section (G31) or a record state. The result is `ICipherAddEditViewModel` with 45 members: 4 states (copy, palette,
  layout, picker choices), 18 editables (6 index pickers, 9 strings, the identity record, 2 bools), 3 keyed lists, 1
  event and 19 methods, plus 14 records/enums.
- Scale itself was fine: the analyzer reported nothing; `oahu generate` took 9 s (iOS) and 6 s (Android) including the
  contracts build; it wrote 28 files, 4,824 lines (Swift model 1,804 lines, Kotlin model 1,203); the C# exporter
  compiled without warnings; Swift and Kotlin compiled first time. Reading the contract is fine; reading the
  generated model is not needed.
- Workaround: the flat contract; sections are grouped by comments. Not a core-design problem, but a form-heavy app will
  want either composable contracts or a documented "section record" pattern.
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): D25 works under `OahuView`: the identity section and the URI list as mounted child contracts, drawn inside the parent's Form/Column on iOS and Android (verified; 07-retest.md, "D25 trial").

### G31. Compose has no text-field helper for a string inside a record editable (friction, verified) [#974](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/974)
- Trying to: edit the identity section (17 strings) as one `IOahuEditable<IdentityFields>`.
- What happened: Swift binds a field directly (`$model.identity[dynamicMember: \.city]`, which `native-screens.md`
  documents: "Binding a field of a record proposes the whole record", and "doesn't hold"). oahu-compose has
  `OahuEditable<String>.rememberTextFieldState()` and the row helper `StateFlow<List<T>>.rememberTextFieldState(...)`,
  nothing for a field of a record. The only option is the value-based `OutlinedTextField(value, onValueChange)` with
  `collectAsMutableState()` and `copy(city = it)`, which the same doc says not to use for text ("Use
  `TextFieldState`, not the value-based `TextField(value, onValueChange)` with `collectAsMutableState()`"). It worked
  for typing (13 record edits for 13 characters, Next moved field to field, saved correctly), but it gives up the
  composition and pending-typing protection the state-based helpers exist for.
- Workaround: value-based fields in `IdentitySection` (`CipherAddEdit.oahu-screen.kt`). The alternative is 17 string
  editables. Every keystroke also sends all 17 strings (G27).
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): sidestepped by D25: the identity section is 17 string editables, so Compose uses `rememberTextFieldState()` and each keystroke sends one string (verified, Android).

### G32. A union case named like a Kotlin type passes the analyzer and iOS, then fails only the Android generation (papercut) [#975](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/975)
- `EditFieldRow.Boolean` (a custom field row case) built, generated Swift, then `oahu generate --framework
  net11.0-android` failed: `error OAHUGEN004: oahu-gen: CipherAddEdit: Bit.App.Contracts.EditFieldRow.Boolean: inside
  the sealed interface EditFieldRow, a case named Boolean would hide the type of that name. Rename the case.` The
  message is clear; the C# analyzer could report it as you type, before one platform's output is already written.
  Renamed to `Toggle`. `Text` as a case name was fine on both.
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): fixed: `EditFieldRow.Boolean` now fails the C# build with `error OAHU027: … whose name Boolean is reserved in Kotlin …` for both targets, before oahu-gen runs (verified).

### G33. A one-shot event published before a modal finishes presenting can't take focus on iOS (papercut, verified) [#976](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/976)
- Trying to: focus the name field when a new item opens (MAUI: `RequestFocus(_nameEntry)`).
- What happened: publishing `Focus(Name)` in the load callback was held until the screen subscribed (as documented)
  and delivered at once; SwiftUI set `@FocusState` while the modal sheet was still animating and nothing was focused.
  MAUI's `RequestFocus` waited `ShowModalAnimationDelay` for this reason. OAHU has no "screen is on screen" signal a
  view model or screen can wait for; `docs/consumer` doesn't mention it.
- Workaround: the page publishes after `Task.Delay(ShowModalAnimationDelay)`, as MAUI did (`CipherAddEditPage.xaml.cs`).
  Then the name field focused on open on both platforms. Focus after the "Name is required" alert worked without a
  delay (the alert is dismissed by then).
- **Retested on 0.1.0-preview.gc63d0aba** (step 7): with the page's delay removed the name took focus in 4 of 4 runs (not reproduced); a screen-side retry kept focus only on the 4th attempt (~300 ms). native-screens.md now says the screen should hold the request, but names no moment to apply it (G42).

### G34. iOS multi-line field: a fast keystroke was sometimes dropped (friction, partly verified, cause not isolated) [#977](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/977)
- Trying to: type a multi-line note quickly (`TextField(axis: .vertical)` bound to `$model.notes`), typing through the
  simulator's hardware keyboard.
- What happened: three of four runs with the generated binding lost one character of the first word ("Line ne",
  "Lie one" for "Line one"); one run with a 30-character single line lost nothing. A DEBUG A/B (`BW_OAHU_NOTES_LOCAL=1`
  binds a local `@State` and copies to the model) typed "Line one" intact in its one run. Separately, in both bindings
  a hardware Return ended editing of the vertical field instead of inserting a newline (SwiftUI behavior, not OAHU),
  so the rest of the text went nowhere; the on-screen Return key inserted newlines. Notes edits run at 2.3 per
  keystroke here (G26). Inferred: the generated binding's write-back interacts with the vertical field when typing
  starts right after focus. Not enough runs to call it; not tried on a device.
- Workaround: none. MAUI's `Editor` had no such report.

### G35. Android: the window pans for the keyboard, hiding MAUI's toolbar (and Save) while a low field has focus (friction, verified) [#978](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/978)
- Trying to: keep the form usable with the keyboard up (the Compose screen uses `imePadding()`, as in step 4).
- What happened: focusing a field in the lower half (third URI, a custom field) panned the whole activity window up
  (the app uses MAUI's default `adjustPan`; nothing in the app sets a soft input mode). The MAUI navigation bar with
  Save and the overflow menu went off screen until the keyboard closed (screenshots). `imePadding()` has no effect
  under pan. The XAML page presumably behaved the same (inferred, not compared). `maui.md` lists keyboard avoidance
  under "Not measured"; nothing says which soft input mode suits a hosted Compose form.
- Workaround: none. `Application.UseWindowSoftInputModeAdjust(Resize)` would let Compose handle the insets, but it is
  app-wide and changes every MAUI page, so not tried.

### G36. Copy-and-paste per screen: union row text needs a hand-written accessor on each platform (papercut) [#979](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/979)
- `OahuRowTexts<Row>(\.text)` takes a key path to a `String`; for the union `EditFieldRow` that means an extension
  property switching over the cases (`editText`), and in Kotlin a `text = { when (it) { … } }` lambda. Small, but the
  generator knows which cases have which fields.

### Probed and fine (step 5)
- **Editable keyed lists, the core probe**: URIs and custom fields as `FromCollection` adapters keyed by
  `OahuKeys.Identity` (rows have no ids; new ones are unsaved), text through `SetUriText`/`SetFieldText` that set the
  item and `Refresh()`, and the documented row helpers (`OahuRowTexts`, `rememberTextFieldState(key, keyOf, text, set)`).
  - With a DEBUG churn timer (`BW_OAHU_CHURN=1`: every 4 s C# moves the last field to the top, inserts a URI at the
    top, removes it, or rewrites another text field) while typing into a URI row: the typed text stayed in its row
    ("two.example.com/abcde" built across an insert and a remove above it), focus and keyboard stayed, on both
    platforms.
  - Move down (MAUI action sheet) on a focused custom field: the row moved, focus came back to it with its text, and
    typing continued in the same row (iOS: UIKit restored first responder after the sheet; Android: same). On iOS the
    row had a Japanese composition pending when the cog was tapped; it was committed, not lost.
  - Remove on a focused row: the row and keyboard went, no fault. Add (type sheet, name prompt) appended a hidden
    field that took typing and the eye (iOS). The linked picker and a boolean toggle changed on screen (iOS); saved
    order checked in the DEBUG save log on both platforms, values on Android. Rename (the Edit prompt) not tried.
  - The row helpers were used exactly as `native-screens.md` shows; nothing undocumented was needed. This is the part
    of OAHU's design (keyed lists, edits settled before the call returns) that a form like this leans on, and it held.
- **Type switch**: iOS Login → Card with the name field focused (the menu picker opened over the keyboard): the
  sections swapped and the name kept focus and text; Android Login → Identity (the dropdown takes focus, so the
  keyboard closed). No stale fields: each section reads the same `CipherView`, and the view model drops linked fields
  of the old type. Once, the first Return after the switch didn't move focus to the cardholder field (not
  reproduced).
- **Result back from a pushed page into a focused field**: generated password (the step 4 generator modal's Select)
  replaced the focused password field's text on both platforms (Android kept focus with the cursor at the end; iOS
  lost focus when the modal opened, as UIKit does). A scanned key (DEBUG stand-in page, no camera) replaced text
  typed into the focused TOTP field on both. SwiftUI's unchanged-text edits on focus loss after the modal did not
  overwrite the new value.
- **Validation focus**: Save with an empty name → MAUI alert → `Focus(Name)` from C# → name focused with the keyboard
  (iOS verified; not tried on Android).
- **Next traversal and keyboards**: iOS Return moved name → cardholder → number (`@FocusState` chain; the last
  identity field shows "done"); Android Next moved first → middle → last name (`ImeAction.Next` default handling).
  The URL keyboard (with `.com`) on URI rows and the password keyboard on secure fields were seen; the email, phone
  and number pads and per-field capitalization are set but were not each looked at. iOS's `Form` scrolled the focused
  bottom field (Country) above the keyboard.
- **Sensitive values, new beyond G22/G27/G28**: a password the user may not view (`login-aws`, `ViewPassword` false) no
  longer reaches the native side at all: the editable returns a fixed mask and refuses edits, so the disabled field
  shows "••••••••" (MAUI's disabled `Entry` held the real password). The same for its TOTP key. Hidden custom fields a
  user may not reveal send `""`. iOS shows the "Passwords" QuickType bar on hidden custom fields (secure fields),
  offering saved passwords for a vault item's custom field, as MAUI's `IsPassword` entry would (inferred).
- **Long pickers and checklist**: 305 folders in the folder picker and 2,000 collections in the checklist
  (`vault-manypick`): iOS opened the screen in 58 ms; the menu picker and `ForEach` toggles worked. Android: see G26
  for the draw time of a non-lazy column; toggling one of 2,000 rows took 3.2 ms in C# and saved the right ids.
- **Edit, add, clone, organization**: edit of a login with 8 URIs (each match type) and 10 custom fields of every
  kind, a card, an identity, a note, an organization login (collections preselected, owner picker), the view-only
  login; add of each type; organization add with collections. The fake save now round-trips every field
  (`FakeVault.FromView`). Clone was built (`clone:<id>`) but not run.
- **Passkey row**: built ("Created …", read-only, hidden on clone as MAUI does), not run.

### Not OAHU (found while testing)
- Other agents were using the shared simulator and emulator during this step: a Codex session launched
  `com.8bit.bitwarden` on "BW OAHU iPhone" (`simctl launch --terminate-running-process ... com.8bit.bitwarden`), which
  restarted the app mid-test, and a scavos session drove `emulator-5580` (`uiautomator dump`, its own preview app). I
  switched to a simulator I created and a second emulator instance; observations from the shared devices are only
  used where nothing else could have caused them.
- The app showed "Bitwarden isn't responding" and "System UI isn't responding" on Android at start under load average
  ~100–146 (Debug, fast deployment); "Wait" continued.
- `MainActivity` has no soft input mode, so MAUI's default pan applies (G35).
- The iOS "add site" autofill alert and Android's autofill service alert show before the form on a new login, as in
  MAUI (the form is swapped in after `ShowAlertsAsync`).

## Step 7 (retest)

Moved to preview `0.1.0-preview.gc63d0aba` and retested the closed issues; tried D25 (sections as mounted child
contracts) on the add/edit form. Details in [07-retest.md](07-retest.md). Host load average 17–711 from other sessions;
Android ran on a read-only `OAHU_Fold_API36` because another session held `OAHU_Pixel_API36` without `-read-only`.

### G37. `init` refuses a managed file that git's line-ending normalisation changed (friction, verified)
- Issue: [AdamE.Oahu#1046](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/1046)
- Trying to: rerun `dotnet tool run oahu init --project src/App/App.csproj --version 0.1.0-preview.gc63d0aba` from a
  fresh worktree of the app, after putting init's own Package.swift and build.gradle.kts back (G17).
- What happened: `oahu: /Users/adam/RiderProjects/bw-retest-18315/src/App/.oahu/App/android/gradlew.bat already exists
  or has local changes. Move your changes aside before retrying.` Nobody edited the file. The app's `.gitattributes`
  has `* text=auto eol=lf`, so the CRLF `gradlew.bat` init wrote in step 2 was committed as LF and every checkout has LF;
  its SHA-256 no longer matches the hash `setup.json` recorded, so init treats it as changed. The text output has no
  code; `--json` gives `OAHUCLI001` with `"fix": null`, and errors.md's OAHUCLI001 says only "The CLI could not read
  its inputs." Found by hashing each managed file against `setup.json`.
- Workaround: rewrote `gradlew.bat` with CRLF (hash then matched; git still stores LF, so the next clone has the same
  problem). A `.gitattributes` line for `gradlew.bat` (`eol=crlf`) would make it stick (not tried).
- Inferred: any repository that normalises line endings, or a Windows checkout with `core.autocrlf`, breaks init's
  rerun the same way for every text file init manages.

### G38. An upgrade leaves stale OAHU versions in projects `init` doesn't manage, and the checks don't notice (friction, verified)
- Issue: [AdamE.Oahu#1047](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/1047)
- Trying to: move the app to the new preview with `init --version`.
- What happened: init updated `.oahu/App/Oahu.targets` (src/App), but the `Oahu.Maui` reference in `src/Core/Core.csproj`
  and the `Oahu` reference in `src/Core.Contracts/Core.Contracts.csproj` (both added by hand because of G1) stayed at
  104c58c2. `oahu doctor` then printed "Declared Oahu.Maui: 0.1.0-preview.gc63d0aba; feed 0.1.0-preview.gc63d0aba" and
  "NuGet restored Oahu 0.1.0-preview.gc63d0aba", and `versions --check` failed only on the missing Gradle report. Nothing
  named the old references. NuGet picks the newer version for App, so the app builds, while the contract project built
  on its own would use the old analyzer (inferred).
- Workaround: `git grep g104c58c2` and edit both lines. An MSBuild property such as `$(OahuVersion)` defined by init,
  or a doctor check across the project's references, would have caught it.

### G39. Mounted sections need the platform's `Mount` and a teardown hook passed in from MAUI page code (papercut, verified)
- Issue: [AdamE.Oahu#1048](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/1048)
- Trying to: follow contracts.md "Splitting a large screen into sections" in a view model that lives in a MAUI class
  library and is hosted by `OahuView`.
- What happened: it works (07-retest.md, "D25 trial"), but `OahuScreens.Mount<T>` lives in `Oahu.iOS` and
  `Oahu.Android`, so the page passes it in under `#if IOS` / `#elif ANDROID`, and the page has to dispose the mounts
  after the parent screen closes, which under `OahuView` means subscribing to `TornDown`. The doc's example shows a
  constructor taking `Func<T, OahuMount>` and a `Dispose`, but nothing about `OahuView`, which the doc says is untried.
  `maui.md`'s "No mounts" known limit reads as if mounts can't be used at all under `OahuView`; they can, inside a
  screen.
- Workaround: `MountSections(...)`/`DisposeSections()` on the view model, called from `CipherAddEditPage.xaml.cs`.

### G40. On iOS a parent screen can't move focus into a mounted section without a round trip through C# (friction, verified)
- Issue: [AdamE.Oahu#1049](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/1049)
- Trying to: keep Return on the name field moving to the identity section's first field, as the flat form did with one
  `@FocusState`.
- What happened: the section is a separate SwiftUI view with its own `@FocusState`; the parent's focus enum can't
  name its fields, and OAHU gives the two screens no shared native channel. The parent contract gained
  `void MoveFocus(FormFocus target)`; C# forwards it to the section's `IOahuEvents<int> Focus`, whose screen sets its
  own focus (verified: `MoveFocus`, then the section's `FirstName` focus edits). Compose needed nothing: its default
  Next traversal crossed into the mounted section (verified, Android).
- Workaround: the method and event above. A SwiftUI environment value declared in the screen package (a focus
  request the parent sets and the section reads) would avoid C#, but it is native plumbing the doc doesn't mention.
- Challenges the design? Mildly: D25 makes each section its own contract, so anything that crosses sections natively
  (focus order, "scroll to the invalid field") now needs either C# or hand-written environment plumbing.

### G41. `OahuTextField` takes SwiftUI focus but the docs say it doesn't, and it can't place the cursor (doc/papercut, verified)
- Issue: [AdamE.Oahu#1050](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/1050)
- native-screens.md says to "Use `TextField` … for multiline input or SwiftUI focus management", yet `.focused($state)`
  on an `OahuTextField` focused it on appearing and after the wrong-password alert (lock page, iOS). The view model's
  cursor request (`SecretFocus.Cursor`, MAUI's "cursor at the end") still has nowhere to go: `OahuTextField` has no
  selection binding. Cost here: none (UIKit kept the cursor itself across show/hide).

### G42. A held focus request has no moment to wait for (papercut, partly verified)
- Issue: [AdamE.Oahu#1051](https://github.com/AdamEssenmacher/AdamE.Oahu/issues/1051)
- native-screens.md now says the screen should hold a request that arrives while it is being presented, and admits the
  earliest moment is unknown. Here a retry every 100 ms kept focus only on the 4th attempt; yet with the request sent at
  once and applied once, the name took focus in 4 of 4 runs. So the screen has to retry or sleep, and the timing it
  guards against is not reproducible on demand. The app keeps MAUI's 400 ms delay in C#.

### G43. The Compose folder screen and FAB don't take the palette's background and container colors (papercut, app-side)
- After adding the palette to the folder screens, the Compose `FoldersScreen` still draws no background of its own (the
  MAUI page shows through) and the floating button uses Material's default container (purple) under Nord, because the
  app's `PaletteTheme` sets only `primary`, `background`, `surface` and a few text colors. Not OAHU; recorded because
  "Styling" leaves the full Material mapping to the app.

### Worked, worth recording (step 7)
- Upgrading the CLI with `dotnet tool update` and rerunning `init --version` (once U1/U2 were out of the way) took 5 s
  and the app built first time on both targets with no new OAHU diagnostic.
- A live app-theme change reaches open screens through the shared palette state on both platforms.
- Sections inherit the parent's SwiftUI environment and Compose composition locals, so a section contract needs no
  palette member.
- `OahuTextField` secure mode: one field, focus, keyboard and cursor kept across show/hide; one edit per keystroke.
