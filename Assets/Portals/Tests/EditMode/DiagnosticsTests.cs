using System.IO;
using NUnit.Framework;
using Portals.Diagnostics;
using UnityEngine.TestTools;

namespace Portals.Tests
{
    public class DiagnosticsTests
    {
        private string _tempRoot;

        [SetUp]
        public void SetUp()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "PortalDiagTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_tempRoot, true); } catch { }
        }

        [Test]
        public void LogEntryCreation_AddsEntry()
        {
            var svc = new DiagnosticsService(_tempRoot);
            svc.Info("Test", "hello", "v=1");
            Assert.That(svc.GetReport(), Does.Contain("hello"));
            svc.Dispose();
        }

        [Test]
        public void SeverityFiltering_MinimumLevel()
        {
            var svc = new DiagnosticsService(_tempRoot);
            svc.MinimumLevel = LogLevel.Warning;
            svc.Info("Test", "info msg", null);
            svc.Warning("Test", "warn msg", null);
            Assert.That(svc.GetReport(), Does.Not.Contain("info msg"));
            Assert.That(svc.GetReport(), Does.Contain("warn msg"));
            svc.Dispose();
        }

        [Test]
        public void FileOutput_SessionLogCreated()
        {
            var svc = new DiagnosticsService(_tempRoot);
            svc.Info("Test", "file output", null);
            svc.Dispose();
            Assert.That(File.Exists(svc.SessionLogPath), Is.True);
            Assert.That(File.ReadAllText(svc.SessionLogPath), Does.Contain("file output"));
        }

        [Test]
        public void SessionSeparation_DifferentPaths()
        {
            var svc1 = new DiagnosticsService(_tempRoot);
            var svc2 = new DiagnosticsService(Path.Combine(_tempRoot, "sub"));
            Assert.That(svc1.SessionLogPath, Is.Not.EqualTo(svc2.SessionLogPath));
            svc1.Dispose();
            svc2.Dispose();
        }

        [Test]
        public void ClipboardReportFormatting_ContainsHeader()
        {
            var svc = new DiagnosticsService(_tempRoot);
            svc.Info("Test", "report", null);
            string report = svc.GetReport();
            Assert.That(report, Does.Contain("Portal Game Diagnostics Report"));
            svc.Dispose();
        }

        [Test]
        public void LogRetention_DeletesOldSessions()
        {
            // Create more than retention count
            for (int i = 0; i < 5; i++)
            {
                var svc = new DiagnosticsService(_tempRoot, 100, 2);
                svc.Info("Test", "session " + i, null);
                svc.Dispose();
            }
            var files = Directory.GetFiles(_tempRoot, "session_*.log");
            Assert.That(files.Length, Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void ExceptionCapture_RecordsStackTrace()
        {
            var svc = new DiagnosticsService(_tempRoot);
            LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("boom"));
            try { throw new System.InvalidOperationException("boom"); }
            catch (System.Exception e) { svc.CaptureException(e, "Test"); }
            Assert.That(svc.GetReport(), Does.Contain("boom"));
            svc.Dispose();
        }

        [Test]
        public void Throttling_PreventsRapidDuplicates()
        {
            var svc = new DiagnosticsService(_tempRoot);
            bool first = svc.Throttle("key", 10f);
            bool second = svc.Throttle("key", 10f);
            Assert.That(first, Is.True);
            Assert.That(second, Is.False);
            svc.Dispose();
        }
    }
}