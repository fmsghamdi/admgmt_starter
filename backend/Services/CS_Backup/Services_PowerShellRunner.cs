using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Threading;

namespace admgmt_backend.Services
{
    public static class PowerShellRunner
    {
        private static readonly Lazy<RunspacePool> _pool = new Lazy<RunspacePool>(() =>
        {
            var iss = InitialSessionState.CreateDefault();
            iss.ImportPSModule(new[] { "ActiveDirectory" });
            var pool = RunspaceFactory.CreateRunspacePool(1, 4, iss, null);
            pool.Open();
            return pool;
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        public static IReadOnlyList<PSObject> Invoke(string script, Dictionary<string, object>? parameters = null, TimeSpan? timeout = null)
        {
            using var ps = PowerShell.Create();
            ps.RunspacePool = _pool.Value;
            ps.AddScript(script);

            if (parameters != null)
                foreach (var kv in parameters)
                    ps.AddParameter(kv.Key, kv.Value);

            var async = ps.BeginInvoke();
            if (!async.AsyncWaitHandle.WaitOne(timeout ?? TimeSpan.FromSeconds(30)))
            {
                ps.Stop();
                throw new TimeoutException("PowerShell invocation timed out.");
            }
            var output = ps.EndInvoke(async);

            if (ps.HadErrors)
            {
                var err = string.Join(Environment.NewLine, ps.Streams.Error.Select(e => e.ToString()));
                throw new InvalidOperationException(err);
            }

            return output.ToList();
        }

        public static T? GetProp<T>(PSObject obj, string name, T? @default = default)
        {
            try
            {
                var p = obj.Properties[name];
                if (p == null || p.Value is null) return @default;
                return (T)Convert.ChangeType(p.Value, typeof(T));
            }
            catch
            {
                return @default;
            }
        }
    }
}