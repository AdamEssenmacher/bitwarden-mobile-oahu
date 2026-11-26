# OAHU in App.csproj

Written by `oahu init` for OAHU 0.1.0-preview.gc63d0aba (mobile). Do not edit it: rerun `dotnet tool run oahu init` to update it.
Paths here are relative to the folder that holds `App.csproj`; run the commands from there. Commands here and each
diagnostic's `fix` run the CLI as the local tool the docs install, `dotnet tool run oahu`; with a global install, drop
`dotnet tool run`.

## Docs for this version

Every OAHU package carries the consumer docs of its version. After a restore they are in `~/.nuget/packages/oahu/0.1.0-preview.gc63d0aba/docs/`
(under `$NUGET_PACKAGES` if it is set). Read them there, not on the web, so they match the packages in use:

- `README.md`: the index. `protocol.md`: delivery, threading and lifetime rules.
- `contracts.md`: the types a contract may use and what the analyzer enforces.
- `native-screens.md`: generated models, the screen you write, previews. `hosting.md`: mounting screens.
- `errors.md`: every diagnostic code with its cause and fix. `packages/Oahu.Cli.md`: this CLI.
- `packages/Oahu.Maui.md` and `maui.md`: hosting in a MAUI app.
- `packages/Oahu.iOS.md`: hosting on ios.
- `packages/Oahu.Android.md`: hosting on android.

Before a restore, the same docs are in the feed: `../../../oahu-feed/swift/0.1.0-preview.gc63d0aba/OahuRuntime/docs/`.

## Contracts

- A contract is a C# interface marked `[OahuContract]` in a project that references `Oahu` (here `../Core.Contracts/Core.Contracts.csproj`).
  Its name drops a leading `I` and a trailing `ViewModel`: `ITasksViewModel` is `Tasks`.
- Use only the members and value types `contracts.md` lists. The analyzer reports the rest as `OAHU0xx` errors.
- Change the C# contract, then regenerate. Never edit generated files (`*.oahu.*`) or the files
  `.oahu/App/setup.json` lists: rerun `dotnet tool run oahu init` or `dotnet tool run oahu generate` instead.

## Screens

- ios: a SwiftUI `View` named `{Contract}Screen` with `init(model: {Contract}Model)`, in a file named `{Contract}.oahu-screen.swift` (`Tasks.oahu-screen.swift` for `ITasksViewModel`) in `.oahu/App/ios/OahuScreens/Sources/OahuScreens/`.
- android: `@Composable fun {Contract}Screen(model: {Contract}Model)` in a file named `{Contract}.oahu-screen.kt` (`Tasks.oahu-screen.kt` for `ITasksViewModel`) in `.oahu/App/android/oahu-screens/src/main/kotlin/`, in the package `com.oahu.appapp`.

## Check your work

Each `--json` report is one JSON document on stdout. Exit codes: 0 success (also with warnings), 1 the
command ran and failed, 2 the command line is wrong.

- `dotnet tool run oahu generate --project App.csproj --framework net11.0-ios --check --json`: exits 1 when generated files are stale. Without `--check` it regenerates them.
- `dotnet tool run oahu generate --project App.csproj --framework net11.0-android --check --json`: exits 1 when generated files are stale. Without `--check` it regenerates them.
- `dotnet tool run oahu doctor --project App.csproj --json`: prerequisites and setup; exits 1 on an error.
- `dotnet tool run oahu inspect --project App.csproj --json`: each target's build items and the evidence of its last build.
- `dotnet build`: analyzer and generator errors appear with their codes.

## Reading diagnostics

Each diagnostic has a stable `code`, a `severity` and a `message`: act on the code, not the message text.
`fix`, when present, is a command line that addresses it; run it as given. `docs`, when present, links the
code's section of `errors.md` in the latest docs: read the same section in `~/.nuget/packages/oahu/0.1.0-preview.gc63d0aba/docs/errors.md`.
`OAHUCLI005` means build or restore evidence is missing, stale or ambiguous (NuGet assets, the Gradle report,
the GTK marker). Run its `fix` when it has one, then the command that reported it; otherwise follow the message.

`OAHUAPK007` means the Gradle screen module builds against older Maven versions than the app's NuGet
packages ship. `oahu init` aligns `.oahu/App/android/oahu-screens` when the project restores; if the
warning appears, or after a NuGet package change, run
`dotnet tool run oahu versions --align-android --project App.csproj`, then rebuild.
