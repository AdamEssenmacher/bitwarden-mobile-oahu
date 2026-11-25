# Step 1: .NET 11 / MAUI 11 upgrade

Branch `oahu/01-net11-upgrade`, cut from upstream `main` (b543459f). Goal: `src/App` builds in Debug for
`net11.0-ios` (simulator) and `net11.0-android`, with iOS 17.0 and Android API 28 as the minimums, and the app
reaches its first screen on each. No OAHU work.

Toolchain: .NET SDK 11.0.100, workloads android 37.2.31, ios 27.2.11039, maui 11.0.0; Xcode 27.2.

Effort: about 50 minutes of wall-clock time, most of it builds and the Android install (about 5 minutes).

## Results

| Target | Build | Launch |
|---|---|---|
| `net11.0-ios`, `iossimulator-x64`, Debug | Yes, with the three app extensions embedded | Yes, iOS 18.6 simulator "BW OAHU iPhone": the login page appears |
| `net11.0-android`, Debug | Yes | Yes, emulator `OAHU_Pixel_API36` (API 36, read-only): the login page appears |

Build commands:

```sh
cd src/App
dotnet build -f net11.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-x64
dotnet build -f net11.0-android -c Debug
```

## Problems, causes and fixes

### SDK pinned to 8.0.402
- Cause: `global.json` pinned 8.0.402 with `rollForward: disable`; that SDK isn't installed.
- Fix: pin 11.0.100 with `rollForward: latestFeature`.

### TFMs and OS minimums
- Fix: `net8.0` became `net11.0` in every project under `src/` and `test/` (`store/google/Publisher` is untouched).
- `SupportedOSPlatformVersion` is 17.0 on iOS and 28.0 on Android (App, Core, the three extensions). The
  `MinimumOSVersion` in the four iOS Info.plists is 17.0. The Android manifest's `minSdkVersion` is 28.
- `MauiVersion` in `Directory.Build.props` is 11.0.0.
- `targetSdkVersion` stays 34 (warnings XA4211 and XA1006). Raising it changes runtime behavior, so it is left for
  later.

### Microsoft.Maui.Controls.Compatibility has no MAUI 11 release
- Cause: restore failed with NU1102; the package stops at 10.0.x.
- Fix: removed it, and `SkiaSharp.Views.Maui.Controls.Compatibility`, which depends on it, from every project.
  Source that used it:
  - `UseMauiCompatibility()` in `Core/MauiProgram.cs`: removed.
  - `ToUIColor()` (iOS.Core `iOSHelpers`, `Toast`) and `ToAndroid()` (TouchBehavior, ThemeHelpers,
    DeviceActionService, Slider/Stepper mappings): replaced by MAUI's `ToPlatform()`.
  - The copied TouchBehavior's iOS part cast the view to `IVisualNativeElementRenderer`. Only Compatibility
    renderers implement it, and none are used, so the cast always fell through to the view. It now uses the view.
  - `UseMauiEmbedding<Application>()` (iOS.Core `SetupMaui`, used by the extensions) came from Compatibility.
    MAUI 11's replacement is `UseMauiEmbeddedApp<Application>()`. CommunityToolkit's analyzer MCT001 then
    complains that `UseMauiCommunityToolkit()` is not chained to `UseMauiApp<T>()`; it is suppressed with a
    `#pragma` around that one statement, because the app is registered by `UseMauiEmbeddedApp` after it.

### Package downgrades on Android (NU1605)
- Cause: MAUI 11 needs `Xamarin.AndroidX.Activity.Ktx` >= 1.13.0.1 and `Xamarin.AndroidX.Credentials` >= 1.6.0.1.
- Fix: bumped Activity.Ktx to 1.13.0.1. The local hand-rolled `Xamarin.AndroidX.Credentials` 1.0.0 (nuget.config
  source `lib/android/Xamarin.AndroidX.Credentials`) is replaced by the official package 1.6.0.1. The local
  source, nupkg and `src/Xamarin.AndroidX.Credentials` project are left in place but unused.
- The official binding has no `CallingAppInfo.Origin` property. The code only tested it for null, so it now uses
  `IsOriginPopulated`. The verified origin is still read with `GetOrigin(privilegedAllowlist)`. See Security.

### CommunityToolkit.Maui 5.2.0 targets MAUI 7
- Fix: bumped to 17.0.0, the release built for MAUI 11.
- CT 17 ships its own `CommunityToolkit.Maui.Behaviors.TouchBehavior`. Bitwarden's copied implementation used the
  same namespace and name, so the XAML source generator failed (MAUIG1001 in `AccountViewCell.xaml`). The copy
  moved to namespace `Bit.App.Behaviors.MCTTouch`; behavior is unchanged.
- `LazyView.LoadViewAsync` now takes a `CancellationToken`; the override in `SendAddOnlyOptionsView` follows.

### Other MAUI 11 API changes
- MAUI 11 adds `Microsoft.Maui.Controls.HybridWebView`, which is ambiguous with `Bit.App.Controls.HybridWebView`.
  The Bitwarden type is aliased in the handlers and qualified in `TwoFactorPage`.
- `DatePicker.Date` and `TimePicker.Time` are nullable. `ExtendedDatePicker`/`ExtendedTimePicker` use `?.ToString`.

### SkiaSharp 2.88.4-preview
- Fix: bumped `SkiaSharp.Views.Maui.Controls` to 3.119.4, the latest 3.x (built against MAUI 8; works with 11 for
  the build). 4.x would be the next step if 3.x misbehaves at run time.

### Microsoft.Extensions.Logging.Debug 7.0.0
- Fix: bumped to 11.0.0 to match MAUI 11's Microsoft.Extensions.* versions.

### iOS 27 SDK requires the scene lifecycle (MT7199)
- Fix: `Info.plist` has a `UIApplicationSceneManifest` with the `__MAUI_DEFAULT_SCENE_CONFIGURATION__` config, and
  `Platforms/iOS/SceneDelegate.cs` subclasses `MauiUISceneDelegate`.
- Under scenes, UIKit stops calling the app delegate's activate, resign, background, foreground, open-URL and
  continue-user-activity callbacks. The SceneDelegate forwards each to the existing AppDelegate override, and
  forwards cold-launch URLs and activities from `WillConnect`. The AppDelegate is unchanged.
- On launch the login page appeared, then the launch-screen logo faded out over it; a second screenshot showed
  the page clean.
- Not verified: that MAUI doesn't now raise some lifecycle events twice (once from the scene, once from the
  forwarded app-delegate base call), deep links, and the privacy splash on resign/activate.

### iOS simulator must be x86_64
- Cause: `lib/ios/libargon2.a` (Argon2id KDF) and AppCenter's native `AppCenter.a` contain arm64 for devices and
  x86_64 for the simulator only. An `iossimulator-arm64` build fails to link ("building for 'iOS-simulator', but
  linking in object file ... built for 'iOS'"). The project already set `ForceSimulatorX64ArchitectureInIDE`,
  and the Argon2 link flags are only set for `iossimulator-x64` and `ios-arm64`.
- Fix: none in the project; build with `-p:RuntimeIdentifier=iossimulator-x64`. AppCenter 5.0.7 was tried and has
  the same slices, so it stays at 5.0.3.
- The iOS 27.2 simulator refuses x86_64 apps ("does not contain code for any platform ... runnable on this
  device"). The iOS 18.6 simulator runs them under Rosetta, so the launch check used iOS 18.6.
- Follow-up: rebuild libargon2 as an xcframework with an arm64 simulator slice, and remove AppCenter (its service
  is retired), to build for arm64 simulators and iOS 27 simulators.

## Packages changed

| Package | Before | After |
|---|---|---|
| Microsoft.Maui.Controls | 8.0.7 | 11.0.0 |
| Microsoft.Maui.Controls.Compatibility | 8.0.7 | removed |
| SkiaSharp.Views.Maui.Controls | 2.88.4-preview.84 | 3.119.4 |
| SkiaSharp.Views.Maui.Controls.Compatibility | 2.88.4-preview.84 | removed |
| CommunityToolkit.Maui | 5.2.0 | 17.0.0 |
| Microsoft.Extensions.Logging.Debug | 7.0.0 | 11.0.0 |
| Xamarin.AndroidX.Activity.Ktx | 1.7.2.1 | 1.13.0.1 |
| Xamarin.AndroidX.Credentials | 1.0.0 (local binding) | 1.6.0.1 (official) |

Unchanged and still restoring against old frameworks (they built; run-time behavior beyond the first screen is
unverified): Camera.MAUI 1.4.4 (net7), FFImageLoadingCompat.Maui 0.1.1 (net7), Plugin.Fingerprint 3.0.0-beta.1
(net6), PCLCrypto 2.1.40-alpha (MonoAndroid/Xamarin.iOS, NU1703), Microsoft.AppCenter.Crashes 5.0.3,
Xamarin.Firebase.Messaging 123.1.2.2, Xamarin.GooglePlayServices.SafetyNet 118.0.1.5, Xamarin.AndroidX.AutoFill
1.1.0.18, Portable.BouncyCastle 1.9.0, System.Formats.Cbor 8.0.0, MessagePack 2.5.124. NuGet reports known
vulnerabilities in MessagePack 2.5.124, SkiaSharp 2.88.x (now gone) and System.Formats.Cbor 8.0.0.

## Stubbed, excluded or deferred
- Nothing stubbed or excluded. The iOS extensions build and are embedded. The watch app is not embedded, as before
  (`IncludeBitwardenWatchOSApp` is False upstream).
- F-Droid (`FDROID`) and unit-test (`UT`) configurations were not built.
- `test/` projects were retargeted but not built or run.
- Release builds, signing and device builds were not tried.
- Android `targetSdkVersion` stays 34.

## Security-relevant notes
- No crypto, secure-storage or biometric package changed. PCLCrypto, Portable.BouncyCastle, Plugin.Fingerprint
  and the Argon2 native libraries are as upstream.
- Passkey (Credential Manager) origin handling moved from the local AndroidX.Credentials binding to the official
  one. `Origin is null` became `!IsOriginPopulated`; both mean "no origin was supplied by a privileged caller".
  The allowlist check and `GetOrigin(privilegedAllowlist)` are unchanged. Not exercised at run time.
- Under the scene lifecycle the privacy splash (shown on resign, removed on activate) depends on the forwarding in
  SceneDelegate. Not exercised.
