using System;
using System.Globalization;
using System.Text;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Diagnostics.Interfaces;

namespace JingleBox2.Diagnostics;

/// <inheritdoc/>
public sealed class LogLine(ILogAreas? areas = null) : ILogLine
{
    /// <summary>What each area is called, which is the second column of every line.</summary>
    private readonly ILogAreas _areas = areas ?? new LogAreas();

    /// <summary>How wide the area column is, which is the longest name plus room to breathe.</summary>
    private const int AreaWidth = 7;

    /// <summary>How wide the process column is, which is wider than any process number gets.</summary>
    private const int ProcessWidth = 7;

    /// <inheritdoc/>
    public string Format(LogArea area, DateTime at, int processId, string message)
    {
        message ??= "";

        var line = new StringBuilder(message.Length + 64);

        line.Append(at.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append("  ")
            .Append(_areas.Short(area).PadRight(AreaWidth))
            .Append(processId.ToString(CultureInfo.InvariantCulture).PadLeft(ProcessWidth))
            .Append("  ")
            .Append(message)
            .Append('\n');

        return line.ToString();
    }

    /// <inheritdoc/>
    public string Lost(int lost) =>
        "(" + lost.ToString(CultureInfo.InvariantCulture) + " line(s) went unwritten: the log could not keep up)\n";
}
