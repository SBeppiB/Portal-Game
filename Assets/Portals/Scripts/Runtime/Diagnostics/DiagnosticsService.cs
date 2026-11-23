using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Portals.Diagnostics
{
    public enum LogLevel { Debug, Info, Warning, Error, Critical }

    public sealed class LogEntry
    {
        public DateTime Timestamp;
        public LogLevel Level;
        public string System;
        public string Message;
        public string Values;
        public string StackTrace;

        public string Format()
        {
            var sb = new StringBuilder(128);
            sb.Append(Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.Append(" [").Append(Level.ToString().ToUpperInvariant()).Append("] ");
            sb.Append(System).Append(": ").Append(Message);
            if (!string.IsNullOrEmpty(Values)) sb.Append(" | ").Append(Values);
            if (!string.IsNullOrEmpty(StackTrace)) sb.Append("\n").Append(StackTrace);
            return sb.ToString();
        }
    }

    public sealed class DiagnosticsService : IDisposable
    {
        private readonly string _logRoot;
        private readonly int _maxEntries;
        private readonly int _retentionCount;
        private readonly List<LogEntry> _buffer = new List<LogEntry>();
        private readonly Dictionary<string, DateTime> _throttle = new Dictionary<string, DateTime>();
        private readonly object _lock = new object();

        private StreamWriter _sessionWriter;
        private StreamWriter _latestWriter;
        private StreamWriter _errorWriter;
        private DateTime m_NextFlush;

        public string SessionLogPath { get; private set; }
        public string LatestLogPath { get; private set; }
        public string ErrorLogPath { get; private set; }
        public LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

        public DiagnosticsService(string logRoot, int maxEntries = 1000, int retentionCount = 20)
        {
            _logRoot = logRoot;
            _maxEntries = Math.Max(10, maxEntries);
            _retentionCount = Math.Max(1, retentionCount);

            try
            {
                Directory.CreateDirectory(_logRoot);

                string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                SessionLogPath = Path.Combine(_logRoot, $"session_{stamp}.log");
                LatestLogPath = Path.Combine(_logRoot, "latest.log");
                ErrorLogPath = Path.Combine(_logRoot, "errors.log");

                // AutoFlush = true forces a synchronous disk flush on every log line, which
                // stalls the main thread. Session/latest logs are buffered and flushed
                // periodically (see Log) and on Dispose; only the rare error log stays
                // unbuffered so failures are never lost.
                _sessionWriter = new StreamWriter(SessionLogPath, false) { AutoFlush = false };
                _latestWriter = new StreamWriter(LatestLogPath, false) { AutoFlush = false };
                _errorWriter = new StreamWriter(ErrorLogPath, true) { AutoFlush = true };

                RotateOldSessions();

                Info("Diagnostics", "Session started", $"root={_logRoot}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[Diagnostics] Failed to initialize log files: {e}");
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                try { Info("Diagnostics", "Session ended", null); } catch { }
                try { _sessionWriter?.Dispose(); } catch { }
                try { _latestWriter?.Dispose(); } catch { }
                try { _errorWriter?.Dispose(); } catch { }
            }
        }

        public bool Throttle(string key, float seconds)
        {
            lock (_lock)
            {
                DateTime now = DateTime.Now;
                if (_throttle.TryGetValue(key, out DateTime last) && (now - last).TotalSeconds < seconds)
                    return false;
                _throttle[key] = now;
                return true;
            }
        }

        public void Log(LogLevel level, string system, string message, string values = null, string stackTrace = null)
        {
            if (level < MinimumLevel) return;

            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                System = system ?? "Unknown",
                Message = message ?? string.Empty,
                Values = values,
                StackTrace = stackTrace
            };

            string formatted = entry.Format();

            lock (_lock)
            {
                _buffer.Add(entry);
                if (_buffer.Count > _maxEntries) _buffer.RemoveAt(0);

                try { _sessionWriter?.WriteLine(formatted); } catch { }
                try { _latestWriter?.WriteLine(formatted); } catch { }
                if (level >= LogLevel.Error)
                {
                    try { _errorWriter?.WriteLine(formatted); } catch { }
                }

                if (level >= LogLevel.Error)
                {
                    try { _sessionWriter?.Flush(); } catch { }
                    try { _latestWriter?.Flush(); } catch { }
                    try { _errorWriter?.Flush(); } catch { }
                }
                else if (DateTime.Now >= m_NextFlush)
                {
                    // Buffered writers: flush at most every couple of seconds instead
                    // of on every line, so logging never forces a disk sync mid-frame.
                    m_NextFlush = DateTime.Now.AddSeconds(2.0);
                    try { _sessionWriter?.Flush(); } catch { }
                    try { _latestWriter?.Flush(); } catch { }
                }
            }

            // Mirror to Unity console for visibility.
            switch (level)
            {
                case LogLevel.Debug: case LogLevel.Info: UnityEngine.Debug.Log(formatted); break;
                case LogLevel.Warning: UnityEngine.Debug.LogWarning(formatted); break;
                default: UnityEngine.Debug.LogError(formatted); break;
            }
        }

        public void Debug(string system, string message, string values = null) => Log(LogLevel.Debug, system, message, values);
        public void Info(string system, string message, string values = null) => Log(LogLevel.Info, system, message, values);
        public void Warning(string system, string message, string values = null) => Log(LogLevel.Warning, system, message, values);
        public void Error(string system, string message, string values = null, string stackTrace = null) => Log(LogLevel.Error, system, message, values, stackTrace);
        public void Critical(string system, string message, string values = null, string stackTrace = null) => Log(LogLevel.Critical, system, message, values, stackTrace);

        public void CaptureException(Exception e, string system = "Unhandled")
        {
            if (e == null) return;
            Error(system, e.GetType().Name + ": " + e.Message, null, e.StackTrace);
        }

        public string GetReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== Portal Game Diagnostics Report ===");
            sb.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            sb.AppendLine("Log root: " + _logRoot);
            sb.AppendLine("Session log: " + SessionLogPath);
            sb.AppendLine("Buffered entries: " + _buffer.Count);
            sb.AppendLine();
            lock (_lock)
            {
                foreach (var e in _buffer) sb.AppendLine(e.Format());
            }
            return sb.ToString();
        }

        public void CopyReportToClipboard()
        {
            GUIUtility.systemCopyBuffer = GetReport();
        }

        public void CopyErrorsToClipboard()
        {
            var sb = new StringBuilder();
            lock (_lock)
            {
                foreach (var e in _buffer)
                    if (e.Level >= LogLevel.Error) sb.AppendLine(e.Format());
            }
            GUIUtility.systemCopyBuffer = sb.ToString();
        }

        public void OpenLogFolder()
        {
            if (!Directory.Exists(_logRoot)) Directory.CreateDirectory(_logRoot);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.RevealInFinder(_logRoot);
#else
            Application.OpenURL("file://" + _logRoot);
#endif
        }

        public void ClearOldLogs()
        {
            RotateOldSessions(true);
        }

        private void RotateOldSessions(bool force = false)
        {
            try
            {
                var files = new List<FileInfo>(new DirectoryInfo(_logRoot).GetFiles("session_*.log"));
                files.Sort((a, b) => b.CreationTimeUtc.CompareTo(a.CreationTimeUtc));
                int keep = _retentionCount;
                for (int i = keep; i < files.Count; i++)
                {
                    try { files[i].Delete(); } catch { }
                }
            }
            catch { }
        }

        public static string ResolveLogRoot()
        {
#if UNITY_EDITOR
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "DebugLogs");
#else
            return Path.Combine(Application.persistentDataPath, "DebugLogs");
#endif
        }
    }
}
