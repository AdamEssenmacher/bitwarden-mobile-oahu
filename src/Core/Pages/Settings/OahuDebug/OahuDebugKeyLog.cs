#if DEBUG
// DEBUG ONLY: the step 6 keystroke probe (docs/oahu/06-keystroke-repro.md). With BW_OAHU_REPRO_RUN=<run id>, logs
// what C# receives and publishes for the notes editable, with the main-thread flag and the uptime clock that Swift's
// CACurrentMediaTime uses, to Documents/repro977/<run id>.cs.jsonl (rewritten one second after the last entry).
using System.Diagnostics;
using System.Text;

namespace Bit.App.Pages.OahuDebug
{
    public static class OahuDebugKeyLog
    {
        private static readonly string? Run = Environment.GetEnvironmentVariable("BW_OAHU_REPRO_RUN");
        private static readonly List<string> Lines = new();
        private static readonly object Gate = new();
        private static int _seq;
        private static Timer? _flush;

        public static void Log(string kind, string? text)
        {
            if (Run == null)
            {
                return;
            }
            var seconds = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
            var main = MainThread.IsMainThread;
            var escaped = (text ?? "<null>").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
            lock (Gate)
            {
                _seq++;
                Lines.Add($"{{\"seq\":{_seq},\"t\":{seconds:F6},\"kind\":\"{kind}\",\"main\":{(main ? "true" : "false")},\"text\":\"{escaped}\"}}");
                _flush ??= new Timer(_ => Flush());
                _flush.Change(1000, Timeout.Infinite);
            }
        }

        private static void Flush()
        {
            string content;
            lock (Gate)
            {
                content = string.Join("\n", Lines) + "\n";
            }
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "repro977");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, $"{Run}.cs.jsonl"), content, Encoding.UTF8);
        }
    }
}
#endif
