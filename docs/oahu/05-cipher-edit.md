# Step 5: the item add/edit form as a native screen

Branch `oahu/05-cipher-edit`, cut from `oahu/04-generator-lock` (0bcde1fb). Goal: move the item add/edit/clone form
(`CipherAddEditPage`, the app's largest page) to SwiftUI and Compose screens hosted in `OahuView`, keeping MAUI's
navigation, title, toolbar items, dialogs, action sheets and pushed pages, and probe editable keyed lists, form scale,
type switching, Save while a native field is being edited (G12), results from pushed pages, keyboard handling, the
cost of edits (G26), the first-tap focus problem (G29) and sensitive fields. Findings are in [gaps.md](gaps.md)
(G30–G36, updates under G6, G7, G20, G23, G24, G27, G28 and the G12/G26/G29 results); this file is what was done.

OAHU: the same preview, `0.1.0-preview.g104c58c2`, from `~/RiderProjects/oahu-feed`.

Effort: about 2 hours 40 minutes of wall-clock time: 20 minutes reading the page, view model and docs, 35 minutes
contract, view-model half, page and DEBUG routes (one generator rename, one C# syntax error), Swift and Kotlin screens
compiled first time, 80 minutes on the simulator and emulator (including moving to my own simulator and emulator when
other agents turned out to be driving the shared ones), 25 minutes writing up. Host load average 64–146 from other
sessions during the runs.

## Results

| Target | Build | Run |
|---|---|---|
| `net11.0-ios`, `iossimulator-x64`, Debug | Yes (incremental 70–80 s) | "BW OAHU Step5 iPhone" (created for this step, iOS 18.6): edit of the many-fields login (URIs, every custom field kind; churn while typing; move down/remove on a focused row; add a hidden field; eye; linked picker; toggle), generated password into the password field, fake scan into the TOTP field (also while focused), add login with the name focus, empty-name validation and focus, type switch Login → Card with Next traversal, card number, secure note with Japanese marked text and Save, multi-line notes A/B, identity edit with keyboard avoidance at the bottom, organization add with 305 folders and 2,000 collections, G29 with `HideSoftInputOnTapped` on and off |
| `net11.0-android`, Debug | Yes (115 s full, about 4 min for the install build on a loaded host) | `OAHU_Pixel_API36`, second read-only instance (port 5640; 5580 first, until another agent used it): edit of the many-fields login (one edit per keystroke, Gboard composition then Save, churn while typing in a URI row, move down on a focused custom field, hidden field), generated password and fake scan into focused fields, add with name focus, type switch to Identity with Next traversal and Save, view-only password masked, organization add with 2,000 collections and a toggled row saved |

Build and run:

```sh
cd src/App
dotnet build -f net11.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-x64
xcrun simctl install <udid> bin/Debug/net11.0-ios/iossimulator-x64/App.app
SIMCTL_CHILD_BW_OAHU_DEBUG=edit SIMCTL_CHILD_BW_OAHU_EDIT=edit:login-many xcrun simctl launch <udid> com.8bit.bitwarden
#   BW_OAHU_EDIT: add | add-card | add-identity | add-note | add-org | edit:<id> | clone:<id>
#   BW_OAHU_VAULT=vault-manypick, _CHURN=1, _FAKESCAN=1, _HIDESOFT=0, _NOTES_LOCAL=1, _DIAG=1 (see OahuDebugEdit.cs)

dotnet build -f net11.0-android -c Debug -t:Install -p:AdbTarget="-s emulator-5640"
adb -s emulator-5640 shell am start -n com.x8bit.bitwarden/com.x8bit.bitwarden.MainActivity \
  --es bwOahuDebug edit --es bwOahuEdit edit:login-many --es bwOahuDiag 1
```

Every Save prints `OAHUDEBUG save ...` with what `SubmitAsync` read (fake data; secrets as lengths) and the per-member
edit counts.

## What changed

### Contract (`src/Core.Contracts/CipherAddEdit.cs`)
`ICipherAddEditViewModel`, 45 members: `Copy` (54 labels and the type, brand, month and title option lists),
`Palette`, `Layout` (item kind, which sections and buttons show, eye states, view-password rights, passkey text,
ownership and collections flags), `Choices` (folder and owner labels); editables `Type`, `Folder`, `Owner`,
`CardBrand`, `CardExpMonth`, `IdentityTitle` (indexes), `Name`, `Username`, `Password`, `Totp`, `CardholderName`,
`CardNumber`, `CardExpYear`, `CardCode`, `Notes`, `Identity` (one 17-string record), `Favorite`, `Reprompt`; keyed lists
`Uris`, `Fields` (a union: text, hidden, toggle, linked) and `Collections`; the `Focus` event; and methods for rows
(add, set text, set bool, set linked index, options, toggle hidden, toggle collection) and buttons (show/hide ×3,
generate password and username, check password, scan, copy TOTP, re-prompt help).

### View model (existing class, now `partial`)
- `CipherAddEditPageViewModel.Oahu.cs`: each editable is `OahuEditable.FromProperty` over `Cipher` (or the existing
  `*SelectedIndex` property) with a getter and setter that walk the `CipherView` path the XAML bound. A password or
  TOTP key the user may not view crosses as a fixed mask and edits are refused. The lists are `FromCollection` over the
  view model's own `Uris`, `Fields` and `Collections`, keyed with `OahuKeys.Identity`; row methods find the item with
  `TryGetItem`. DEBUG: edit counters and the churn steps.
- `CipherAddEditPageViewModel.cs`: `partial`; `UriOptions` refreshes the URI rows after setting `Match`; `SubmitAsync`
  publishes `Focus(Name)` after the "Name is required" alert; a DEBUG save log.

### Page
- `CipherAddEditPage.xaml`: the 730-line `ScrollView` is one `OahuView<ICipherAddEditViewModel>` in the resources,
  swapped in by the existing `LoadOnAppearedAsync`. Toolbar items unchanged (Save; Cancel and the more menu on iOS;
  attachments, delete, collections, move to organization on Android).
- Code-behind: the Return-key chain, picker setup, the iOS TOTP text-color hack, `NewUri_Clicked`, `NewField_Clicked`
  and `PasswordPrompt_Toggled` are gone; the name focus waits for the modal animation (G33); the scan handler runs
  from the view model's `ScanTotpRequested`; Android saved-state ID `oahu_cipher_edit`; DEBUG hooks (fake scan, churn,
  `HideSoftInputOnTapped` off).

### Native screens (`src/App/.oahu/App/`)
- SwiftUI `CipherAddEdit.oahu-screen.swift`: a `Form` with sections per item type, `@FocusState` over an enum (fields,
  identity lines, URI and field rows by key) driven by Return and by the `Focus` event, `OahuRowTexts` for URI and
  text/hidden field rows, `SecureField`/`TextField` swaps for secrets, index `Picker`s, a vertical notes field.
- Compose `CipherAddEdit.oahu-screen.kt`: a scrolling `Column` with `imePadding()`, `rememberTextFieldState` for the
  string editables and the row helper for URI and field rows (inside `key(row.key)`), `OutlinedSecureTextField` whose
  obfuscation follows the eye, `ExposedDropdownMenuBox` pickers, value-based fields for the identity record (G31),
  `FocusRequester`s with a pending row focus.
- Glyphs `camera` and `cog` added to the icon tables; generated files committed beside them.

### Debug routes (DEBUG only, `src/Core/Pages/Settings/OahuDebug/`)
- `OahuDebugEdit.cs` and the `edit` route in `OahuDebugRoute.cs`: open add/edit/clone modally over the fake vault, as
  the vault and item pages do; churn timer; fake scan page; `vault-manypick` (300 more folders, 2,000 collections).
- `FakeVault.cs`: `login-many` (8 URIs with every match type, 10 custom fields of every kind) and `FromView`, so the
  fake save keeps everything the page can change; `FakeVaultServices.cs` uses it.

## Left in MAUI
- Title, toolbar items and the more menu, every dialog and action sheet (URI options and match detection, custom field
  options, type and name prompts, overwrite confirmations, validation errors, check-password result), the loading
  overlay and toasts, and the pushed pages (generator, scan, attachments, collections, move to organization, password
  history).

## Not done or not verified
- Clone (route built), the passkey row, the Android Save-from-toolbar validation focus, rename by prompt, URI match
  detection change, the owner picker switching collections on and off, "New URI" focusing the new row, delete,
  attachments/collections/move pages from the menu, the FIDO2 and autofill entry points, the personal-ownership
  policy banner.
- The camera scan page itself (no camera on the simulator or emulator; a DEBUG page stands in).
- G34 needs more runs and a device; G29 was not compared with a plain MAUI `Entry` page.
- Dark/Nord themes on this screen, Dynamic Type, VoiceOver/TalkBack, Release builds, devices.
- The MAUI page has no dirty tracking or discard confirmation, so none was built.

## Size

| | Lines |
|---|---|
| XAML removed (`CipherAddEditPage.xaml`) | 771 changed, 815 → 64 |
| App C# changed in existing files (page, view model) | +59 / -853 across XAML and C# (`git diff --stat`) |
| New C#: contract (`CipherAddEdit.cs`) | 227 |
| New C#: view-model half | 501 |
| New C#: DEBUG routes and fakes | 113 new, +228/-16 in existing files |
| Native code written by hand: Swift 527, Kotlin 461 | 988 |
| Generated native code (committed) | 28 files, 4,824 lines |

## Security-relevant notes
- No crypto, key derivation, secure storage or biometric code changed. The `edit` route replaces the cipher, folder,
  collection, organization, policy, event, audit and user-verification services for the debug process only (`#if
  DEBUG`, needs the launch variable or extra), and must never run with a real account.
- Passwords, TOTP keys, card numbers and codes, hidden custom field values and the identity section (SSN, passport,
  license) cross to C# on every keystroke, as the MAUI bindings did; the identity record sends all 17 strings per
  keystroke (G27). Don't record sessions on this page.
- A password or TOTP key the user may not view no longer reaches the native side (MAUI's disabled entry held it).
- The DEBUG save log prints fake names, usernames, URIs and non-hidden field values; hidden values and passwords only
  as lengths.
- iOS offers saved passwords (QuickType "Passwords") on hidden custom fields because they are secure fields; MAUI's
  password entries do the same (inferred).
