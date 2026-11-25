#if DEBUG
// DEBUG ONLY: an in-memory vault for the OAHU vault pages' debug route (OahuDebugRoute, docs/oahu/03-vault.md). No
// server, no account and no cryptography: every "encrypted" string is base64 text whose decrypted value is preset, so
// the app's own Cipher.DecryptAsync returns it. All values are made up. Never compiled into Release.
using System.Reflection;
using System.Text;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Domain;
using Bit.Core.Models.View;
using CollectionView = Bit.Core.Models.View.CollectionView;

namespace Bit.App.Pages.OahuDebug
{
    public sealed class FakeVault
    {
        private static readonly FieldInfo DecryptedValueField =
            typeof(EncString).GetField("_decryptedValue", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("EncString._decryptedValue not found");

        public const string OrgId = "fake-org-1";
        public const string FamilyOrgId = "fake-org-2";

        private readonly List<Cipher> _ciphers = new();
        private List<CipherView> _decrypted;
        private int _syncCount;

        public FakeVault(string variant)
        {
            Variant = variant;
            Folders = new List<(string Id, string Name)>
            {
                ("folder-finance", "Finance"),
                ("folder-personal", "Personal"),
                ("folder-shopping", "Shopping"),
                ("folder-work", "Work"),
                ("folder-clients", "Work/Clients"),
            };
            Collections = new List<CollectionView>
            {
                new() { Id = "col-eng", OrganizationId = OrgId, Name = "Engineering" },
                new() { Id = "col-eng-prod", OrganizationId = OrgId, Name = "Engineering/Production" },
                new() { Id = "col-marketing", OrganizationId = OrgId, Name = "Marketing" },
                new() { Id = "col-family", OrganizationId = FamilyOrgId, Name = "Shared" },
            };
            Organizations = new List<OrganizationData>
            {
                new() { Id = OrgId, Name = "Example Org", Enabled = true, UseTotp = true,
                    Status = OrganizationUserStatusType.Confirmed, Type = OrganizationUserType.User },
                new() { Id = FamilyOrgId, Name = "Family", Enabled = true, UseTotp = true,
                    Status = OrganizationUserStatusType.Confirmed, Type = OrganizationUserType.User },
            };

            if (variant == "vault-empty")
            {
                Folders.Clear();
                Collections.Clear();
                Organizations.Clear();
            }
            else
            {
                Seed();
                if (variant == "vault-large")
                {
                    SeedLarge(2000);
                }
            }
        }

        public string Variant { get; }
        public List<(string Id, string Name)> Folders { get; }
        public List<CollectionView> Collections { get; }
        public List<OrganizationData> Organizations { get; }
        public IReadOnlyList<Cipher> Ciphers => _ciphers;

        public async Task<List<CipherView>> GetAllDecryptedAsync()
        {
            if (_decrypted == null)
            {
                var views = new List<CipherView>(_ciphers.Count);
                foreach (var c in _ciphers)
                {
                    views.Add(await c.DecryptAsync());
                }
                _decrypted = views;
            }
            return _decrypted.ToList();
        }

        public Cipher Get(string id) => _ciphers.FirstOrDefault(c => c.Id == id);

        public void Changed() => _decrypted = null;

        public void Add(Cipher cipher)
        {
            _ciphers.Add(cipher);
            Changed();
        }

        public void Remove(string id)
        {
            _ciphers.RemoveAll(c => c.Id == id);
            Changed();
        }

        /// <summary>What a sync "brings from the server": one renamed login and, every other sync, one new item.</summary>
        public void ApplyServerChanges()
        {
            _syncCount++;
            var github = Get("login-github");
            if (github != null)
            {
                github.Name = Enc($"GitHub (synced {DateTime.Now:HH:mm:ss})");
                github.RevisionDate = DateTime.UtcNow;
            }
            if (_syncCount % 2 == 1)
            {
                _ciphers.Add(Login($"login-synced-{_syncCount}", $"Synced login {_syncCount}", $"user{_syncCount}@example.com",
                    "Synced-Passw0rd!", new[] { "https://example.org" }));
            }
            Changed();
        }

        public static EncString Enc(string value)
        {
            if (value == null)
            {
                return null;
            }
            var enc = new EncString(EncryptionType.AesCbc256_B64,
                Convert.ToBase64String(Encoding.UTF8.GetBytes(value.Length == 0 ? " " : value)));
            DecryptedValueField.SetValue(enc, value);
            return enc;
        }

        public static string Plain(EncString enc) => enc == null ? null : (string)DecryptedValueField.GetValue(enc);

        private void Seed()
        {
            var now = DateTime.UtcNow;

            var github = Login("login-github", "GitHub", "alice@example.com", "c0rrect-H0rse!battery",
                new[] { "https://github.com/login", "https://gist.github.com" },
                totp: "JBSWY3DPEHPK3PXP", folderId: "folder-work", favorite: true);
            github.Fields = new List<Field>
            {
                new() { Type = FieldType.Text, Name = Enc("Recovery email"), Value = Enc("alice.backup@example.com") },
                new() { Type = FieldType.Hidden, Name = Enc("API token"), Value = Enc("ghp_FAKE0123456789abcdefFAKE") },
                new() { Type = FieldType.Boolean, Name = Enc("2FA enabled"), Value = Enc("true") },
                new() { Type = FieldType.Linked, Name = Enc("Login name"), LinkedId = LinkedIdType.Login_Username },
            };
            github.Attachments = new List<Attachment>
            {
                new() { Id = "att-1", FileName = Enc("recovery-codes.txt"), Size = "1234", SizeName = "1.21 KB",
                    Url = "https://example.invalid/att-1" },
                new() { Id = "att-2", FileName = Enc("ssh-key.pub"), Size = "742", SizeName = "742 Bytes",
                    Url = "https://example.invalid/att-2" },
            };
            github.PasswordHistory = new List<PasswordHistory>
            {
                new() { Password = Enc("old-Passw0rd-1"), LastUsedDate = now.AddDays(-40) },
                new() { Password = Enc("old-Passw0rd-2"), LastUsedDate = now.AddDays(-90) },
            };
            github.Login.PasswordRevisionDate = now.AddDays(-12);
            github.Login.Fido2Credentials = new List<Fido2Credential>
            {
                new()
                {
                    CredentialId = Enc("fake-credential"), Discoverable = Enc("true"), KeyType = Enc("public-key"),
                    KeyAlgorithm = Enc("ECDSA"), KeyCurve = Enc("P-256"), KeyValue = Enc("fake"),
                    RpId = Enc("github.com"), RpName = Enc("GitHub"), UserName = Enc("alice"),
                    UserDisplayName = Enc("Alice"), Counter = Enc("0"), CreationDate = now.AddDays(-30),
                },
            };
            _ciphers.Add(github);

            _ciphers.Add(Login("login-gmail", "Gmail", "bob@example.com", "Gm@il-2026-pass",
                new[] { "https://accounts.google.com" },
                totp: "otpauth://totp/Google:bob@example.com?secret=GEZDGNBVGY3TQOJQ&period=60", favorite: true));
            _ciphers.Add(Login("login-amazon", "Amazon", "alice@example.com", "Sh0pping!!", new[] { "amazon.com" },
                folderId: "folder-shopping"));
            var bank = Login("login-bank", "Bank of Example", "alice-banking", "B4nk-Vault#9",
                new[] { "https://bank.example.com/login" }, totp: "KRSXG5CTMVRXEZLUKN2XAZLSORSWC3TU",
                folderId: "folder-finance");
            bank.Reprompt = CipherRepromptType.Password;
            _ciphers.Add(bank);
            _ciphers.Add(Login("login-android", "Example Android app", "alice", "andr0id-app",
                new[] { "androidapp://com.example.app" }, folderId: "folder-personal"));
            _ciphers.Add(Login("login-ios", "Example iOS app", "alice", "i0s-app",
                new[] { "iosapp://com.example.app" }, folderId: "folder-personal"));
            _ciphers.Add(Login("login-router", "Home router", "admin", "r0uter-admin", new[] { "192.168.1.1" }));
            _ciphers.Add(Login("login-wifi", "Wi-Fi", null, "wifi-passphrase-2026", null));
            _ciphers.Add(Login("login-client", "Client portal", "alice@client.example", "Cl1ent!", new[] { "https://portal.example.net" },
                folderId: "folder-clients"));

            var jira = Login("login-jira", "Jira", "alice@example.com", "J1ra-Org-Pass",
                new[] { "https://example.atlassian.net" }, totp: "MFRGGZDFMZTWQ2LK", orgId: OrgId,
                collections: new[] { "col-eng" });
            jira.OrganizationUseTotp = true;
            _ciphers.Add(jira);
            var aws = Login("login-aws", "AWS console (view only)", "deploy-bot", "Hidden-From-You-1",
                new[] { "https://console.aws.amazon.com" }, orgId: OrgId, collections: new[] { "col-eng-prod" });
            aws.ViewPassword = false;
            aws.Edit = false;
            _ciphers.Add(aws);
            _ciphers.Add(Login("login-figma", "Figma", "marketing@example.com", "F1gma-pass", new[] { "https://www.figma.com" },
                orgId: OrgId, collections: new[] { "col-marketing" }));
            _ciphers.Add(Login("login-netflix", "Netflix", "family@example.com", "N3tflix-family", new[] { "https://www.netflix.com" },
                orgId: FamilyOrgId, collections: new[] { "col-family" }));

            var oldForum = Login("login-old-forum", "Old forum", "alice", "f0rum", new[] { "https://forum.example.com" });
            oldForum.DeletedDate = now.AddDays(-3);
            _ciphers.Add(oldForum);

            _ciphers.Add(Card("card-visa", "Visa", "Alice Example", "4111111111111111", "123", "12", "2030", "Visa",
                favorite: true));
            _ciphers.Add(Card("card-amex", "Amex Gold", "Alice Example", "378282246310005", "1234", "07", "2028", "Amex",
                folderId: "folder-finance"));
            var oldCard = Card("card-old", "Expired Mastercard", "Alice Example", "5555555555554444", "321", "01", "2020",
                "Mastercard");
            oldCard.DeletedDate = now.AddDays(-10);
            _ciphers.Add(oldCard);

            _ciphers.Add(Identity("identity-alice", "Alice Example"));

            var note = Note("note-server", "Server notes",
                "Production:\n  host: prod.example.internal\n  port: 2222\n\nStaging is the same with -stg.");
            note.FolderId = "folder-work";
            _ciphers.Add(note);
            _ciphers.Add(Note("note-recovery", "Wi-Fi guest instructions",
                "Network: Example-Guest\nAsk at the desk for today's code."));
        }

        private void SeedLarge(int count)
        {
            var hosts = new[] { "example.com", "example.org", "example.net", "github.com", "google.com", "amazon.com",
                "microsoft.com", "apple.com", "wikipedia.org", "mozilla.org" };
            var folders = new[] { null, "folder-finance", "folder-personal", "folder-shopping", "folder-work", "folder-clients" };
            for (var i = 0; i < count; i++)
            {
                var host = hosts[i % hosts.Length];
                _ciphers.Add(Login($"bulk-{i:0000}", $"Bulk login {i:0000} ({host})", $"user{i}@{host}", $"Bulk-Pass-{i}!",
                    new[] { $"https://{(i % 3 == 0 ? "login." : "")}{host}" },
                    totp: i % 25 == 0 ? "JBSWY3DPEHPK3PXP" : null, folderId: folders[i % folders.Length],
                    favorite: i % 100 == 0));
            }
        }

        private static Cipher Base(string id, CipherType type, string name)
        {
            var now = DateTime.UtcNow;
            return new Cipher
            {
                Id = id,
                Type = type,
                Name = Enc(name),
                Edit = true,
                ViewPassword = true,
                RevisionDate = now.AddHours(-5),
                CreationDate = now.AddDays(-100),
                Reprompt = CipherRepromptType.None,
            };
        }

        public static Cipher Login(string id, string name, string username, string password, string[] uris,
            string totp = null, string folderId = null, bool favorite = false, string orgId = null,
            string[] collections = null)
        {
            var c = Base(id, CipherType.Login, name);
            c.FolderId = folderId;
            c.Favorite = favorite;
            c.OrganizationId = orgId;
            c.CollectionIds = collections != null ? new HashSet<string>(collections) : null;
            c.Login = new Login
            {
                Username = Enc(username),
                Password = Enc(password),
                Totp = Enc(totp),
                Uris = uris?.Select(u => new LoginUri { Uri = Enc(u) }).ToList(),
            };
            return c;
        }

        private static Cipher Card(string id, string name, string holder, string number, string code, string month,
            string year, string brand, bool favorite = false, string folderId = null)
        {
            var c = Base(id, CipherType.Card, name);
            c.Favorite = favorite;
            c.FolderId = folderId;
            c.Card = new Card
            {
                CardholderName = Enc(holder), Number = Enc(number), Code = Enc(code), ExpMonth = Enc(month),
                ExpYear = Enc(year), Brand = Enc(brand),
            };
            return c;
        }

        private static Cipher Identity(string id, string name)
        {
            var c = Base(id, CipherType.Identity, name);
            c.FolderId = "folder-personal";
            c.Identity = new Identity
            {
                Title = Enc("Ms"), FirstName = Enc("Alice"), LastName = Enc("Example"), Username = Enc("alice"),
                Company = Enc("Example Corp"), Email = Enc("alice@example.com"), Phone = Enc("+1 555 0100"),
                SSN = Enc("000-00-0000"), PassportNumber = Enc("X00000000"), LicenseNumber = Enc("D000-0000-0000"),
                Address1 = Enc("1 Example Street"), Address2 = Enc("Suite 2"), City = Enc("Springfield"),
                State = Enc("EX"), PostalCode = Enc("00000"), Country = Enc("US"),
            };
            c.Notes = Enc("Fake identity for testing.");
            return c;
        }

        private static Cipher Note(string id, string name, string notes)
        {
            var c = Base(id, CipherType.SecureNote, name);
            c.SecureNote = new SecureNote { Type = SecureNoteType.Generic };
            c.Notes = Enc(notes);
            return c;
        }
    }
}
#endif
