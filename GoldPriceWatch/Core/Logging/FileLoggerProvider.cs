using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;

namespace GoldPriceWatch.Core.Logging
{
    /// <summary>
    /// 按天滚动的文件日志：%LocalAppData%\GoldPriceWatch\logs\app-yyyyMMdd.log，保留 7 天。
    /// 只写入 Information 及以上级别，避免把每次成功的 SQL 写入等细节刷进日志。
    /// </summary>
    public sealed class FileLoggerProvider : ILoggerProvider
    {
        private const int RetentionDays = 7;
        private const string Prefix = "app-";

        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GoldPriceWatch", "logs");

        private readonly object _sync = new();
        private StreamWriter? _writer;
        private DateTime _currentDate;
        private DateTime _lastCleanupDate;

        public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

        private void Append(string line)
        {
            lock (_sync)
            {
                try
                {
                    var today = DateTime.Today;
                    if (_writer == null || _currentDate != today)
                    {
                        _writer?.Dispose();
                        Directory.CreateDirectory(LogDir);
                        _writer = new StreamWriter(Path.Combine(LogDir, $"{Prefix}{today:yyyyMMdd}.log"), true)
                        {
                            AutoFlush = true
                        };
                        _currentDate = today;
                    }

                    _writer.WriteLine(line);

                    if (_lastCleanupDate != today)
                    {
                        _lastCleanupDate = today;
                        CleanupOldFiles(today);
                    }
                }
                catch
                {
                    // 日志写入失败不能影响主流程
                }
            }
        }

        private static void CleanupOldFiles(DateTime today)
        {
            try
            {
                var cutoff = today.AddDays(-RetentionDays);
                foreach (var file in Directory.GetFiles(LogDir, $"{Prefix}*.log"))
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
            }
            catch
            {
                // 忽略清理失败
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }

        private sealed class FileLogger : ILogger
        {
            private readonly FileLoggerProvider _provider;
            private readonly string _category;

            public FileLogger(FileLoggerProvider provider, string categoryName)
            {
                _provider = provider;
                _category = categoryName.StartsWith("GoldPriceWatch.", StringComparison.Ordinal)
                    ? categoryName.Substring("GoldPriceWatch.".Length)
                    : categoryName;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;

                var builder = new StringBuilder(160);
                builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                    .Append(" [").Append(LevelText(logLevel)).Append("] ")
                    .Append(_category).Append(" - ")
                    .Append(formatter(state, exception));

                if (exception != null)
                {
                    builder.AppendLine().Append(exception);
                }

                _provider.Append(builder.ToString());
            }

            private static string LevelText(LogLevel level) => level switch
            {
                LogLevel.Trace => "TRACE",
                LogLevel.Debug => "DEBUG",
                LogLevel.Information => "INFO ",
                LogLevel.Warning => "WARN ",
                LogLevel.Error => "ERROR",
                LogLevel.Critical => "FATAL",
                _ => "NONE "
            };
        }
    }
}
