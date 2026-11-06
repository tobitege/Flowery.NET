using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Flowery.NET.Kiosk;

internal enum SmokeOutcome { Passed, Failed, Inconclusive }

/// <summary>
/// The XML report of a smoke run (<c>--smoke-test</c>), as scripts/test_kiosk.ps1 reads it: one entry per check with
/// its outcome and message.
/// </summary>
internal sealed class KioskSmokeReport(string path)
{
    private readonly List<SmokeResult> _results = [];
    public string Path { get; } = System.IO.Path.GetFullPath(path);
    public int Failed => _results.Count(result => result.Outcome == SmokeOutcome.Failed);
    public int Inconclusive => _results.Count(result => result.Outcome == SmokeOutcome.Inconclusive);

    /// <summary>The folder of the report, which also takes the screenshots of the run.</summary>
    public string Folder => System.IO.Path.GetDirectoryName(Path)!;

    public void Add(string name, bool passed, string message) =>
        Add(name, passed ? SmokeOutcome.Passed : SmokeOutcome.Failed, message);

    public void Add(string name, SmokeOutcome outcome, string message)
    {
        var uniqueName = name;
        for (var suffix = 2; _results.Any(result => result.Name == uniqueName); suffix++)
            uniqueName = $"{name}.{suffix}";
        _results.Add(new SmokeResult(uniqueName, outcome, message));
    }

    public void Save()
    {
        var document = new XDocument(new XElement("tests",
            new XAttribute("total", _results.Count),
            new XAttribute("passed", _results.Count - Failed - Inconclusive),
            new XAttribute("failed", Failed),
            new XAttribute("inconclusive", Inconclusive),
            _results.Select(result => new XElement("test",
                new XAttribute("name", result.Name),
                new XAttribute("outcome", result.Outcome),
                new XElement("message", result.Message)))));
        Directory.CreateDirectory(Folder);
        var pendingPath = Path + ".writing";
        var settings = new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true, CloseOutput = false };
        using (var stream = new FileStream(pendingPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            using (var writer = XmlWriter.Create(stream, settings))
                document.Save(writer);
            stream.Flush(flushToDisk: true);
        }
        File.Move(pendingPath, Path, overwrite: true);
        var saved = XDocument.Load(Path).Root ?? throw new InvalidDataException("The smoke report has no root element.");
        if (saved.Name != "tests" || (int?)saved.Attribute("total") != _results.Count ||
            (int?)saved.Attribute("failed") != Failed || (int?)saved.Attribute("inconclusive") != Inconclusive ||
            saved.Elements("test").Count() != _results.Count)
            throw new InvalidDataException("The published smoke report failed readback validation.");
        File.WriteAllLines(Path + ".log", _results.Select(result =>
            $"{result.Outcome} {result.Name}: {result.Message}"), new UTF8Encoding(false));
    }

    private sealed record SmokeResult(string Name, SmokeOutcome Outcome, string Message);
}
