using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Management.Automation.Host;
using System.Globalization;

namespace admgmt_backend.Services
{
    public class PowerShellRunner : IDisposable
    {
        private readonly RunspacePool _pool;

        public PowerShellRunner()
        {
            var iss = InitialSessionState.CreateDefault();
            // Import ActiveDirectory module in each runspace
            iss.ImportPSModule(new[] { "ActiveDirectory" });
            
            // إنشاء PSHost مخصص
            var host = new DefaultPSHost();
            _pool = RunspaceFactory.CreateRunspacePool(1, 4, iss, host);
            _pool.Open();
        }

        public async Task<IReadOnlyList<PSObject>> Invoke(string script, IDictionary<string, object>? parameters = null)
        {
            using var ps = PowerShell.Create();
            ps.RunspacePool = _pool;
            ps.AddScript(script);
            if (parameters != null)
            {
                foreach (var kv in parameters)
                    ps.AddParameter(kv.Key, kv.Value);
            }
            var output = await Task.Run(() => ps.Invoke());
            if (ps.HadErrors)
            {
                var msg = string.Join(" | ", ps.Streams.Error.Select(e => e.ToString()));
                throw new InvalidOperationException($"PowerShell error: {msg}");
            }
            return output;
        }

        public static T? GetProp<T>(PSObject obj, string name)
        {
            var p = obj.Properties[name]?.Value;
            if (p is null) return default;
            try { return (T)Convert.ChangeType(p, typeof(T)); } catch { return default; }
        }

        public void Dispose() => _pool?.Close();
    }

    // Default PSHost implementation
    public class DefaultPSHost : PSHost
    {
        public override string Name => "ADMgmtHost";
        public override Version Version => new Version(1, 0);
        public override Guid InstanceId { get; } = Guid.NewGuid();
        public override PSHostUserInterface UI => new DefaultPSHostUserInterface();
        public override CultureInfo CurrentCulture => System.Globalization.CultureInfo.CurrentCulture;
        public override CultureInfo CurrentUICulture => System.Globalization.CultureInfo.CurrentUICulture;
        public override void SetShouldExit(int exitCode) { }
        public override void EnterNestedPrompt() { }
        public override void ExitNestedPrompt() { }
        public override void NotifyBeginApplication() { }
        public override void NotifyEndApplication() { }
    }

    public class DefaultPSHostUserInterface : PSHostUserInterface
    {
        public override PSHostRawUserInterface RawUI => new DefaultPSHostRawUserInterface();
        public override string ReadLine() => string.Empty;
        public override System.Security.SecureString ReadLineAsSecureString() => new System.Security.SecureString();
        public override void Write(string value) { }
        public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value) { }
        public override void WriteLine(string value) { }
        public override void WriteLine(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value) { }
        public override void WriteDebugLine(string message) { }
        public override void WriteErrorLine(string value) { }
        public override void WriteVerboseLine(string message) { }
        public override void WriteWarningLine(string message) { }
        public override void WriteProgress(long sourceId, ProgressRecord record) { }
        public override Dictionary<string, PSObject> Prompt(string caption, string message, System.Collections.ObjectModel.Collection<FieldDescription> descriptions) => new Dictionary<string, PSObject>();
        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName) => new PSCredential("", new System.Security.SecureString());
        public override PSCredential PromptForCredential(string caption, string message, string userName, string targetName, PSCredentialTypes allowedCredentialTypes, PSCredentialUIOptions options) => new PSCredential("", new System.Security.SecureString());
        public override int PromptForChoice(string caption, string message, System.Collections.ObjectModel.Collection<ChoiceDescription> choices, int defaultChoice) => 0;
    }

    public class DefaultPSHostRawUserInterface : PSHostRawUserInterface
    {
        public override ConsoleColor BackgroundColor { get; set; } = ConsoleColor.Black;
        public override ConsoleColor ForegroundColor { get; set; } = ConsoleColor.White;
        public override Size BufferSize { get; set; } = new Size(120, 30);
        public override Coordinates CursorPosition { get; set; } = new Coordinates(0, 0);
        public override int CursorSize { get; set; } = 1;
        public override bool KeyAvailable => false;
        public override Size MaxPhysicalWindowSize => new Size(120, 30);
        public override Size MaxWindowSize => new Size(120, 30);
        public override Coordinates WindowPosition { get; set; } = new Coordinates(0, 0);
        public override Size WindowSize { get; set; } = new Size(120, 30);
        public override string WindowTitle { get; set; } = "ADMgmt";
        public override void FlushInputBuffer() { }
        public override void SetBufferContents(Coordinates origin, BufferCell[,] contents) { }
        public override void SetBufferContents(Rectangle rectangle, BufferCell fill) { }
        public override BufferCell[,] GetBufferContents(Rectangle rectangle) => new BufferCell[1, 1];
        public override void ScrollBufferContents(Rectangle source, Coordinates destination, Rectangle clip, BufferCell fill) { }
        public override KeyInfo ReadKey(ReadKeyOptions options) => new KeyInfo();
    }
}