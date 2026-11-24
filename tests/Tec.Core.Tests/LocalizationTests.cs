using System.Xml.Linq;
using Tec.Core.Abstractions;
using Tec.Core.Localization;
using Tec.Core.Model;
using Tec.Core.Reporting;

namespace Tec.Core.Tests;

public class LocalizationTests
{
    /// <summary>Every <c>.resx</c> in the solution (Tec.Core and Tec.App), with its translations.</summary>
    public static TheoryData<string> NeutralResourceFiles()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(Path.Combine(FindRepositoryRoot(), "src"), "*.resx", SearchOption.AllDirectories))
        {
            // Strings.resx is neutral; Strings.en.resx is a translation.
            if (Path.GetFileNameWithoutExtension(file).Contains('.', StringComparison.Ordinal)) continue;
            data.Add(Path.GetRelativePath(FindRepositoryRoot(), file));
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(NeutralResourceFiles))]
    public void Every_language_translates_every_text(string neutralFile)
    {
        var root = FindRepositoryRoot();
        var neutralKeys = ReadKeys(Path.Combine(root, neutralFile));
        foreach (var language in TecLanguage.All.Where(l => l != TecLanguage.Spanish))
        {
            var translated = Path.ChangeExtension(Path.Combine(root, neutralFile), $".{language.Code}.resx");
            Assert.True(File.Exists(translated), $"Missing {translated}");
            Assert.Equal(neutralKeys, ReadKeys(translated));
        }
    }

    [Fact]
    public void Missing_texts_show_their_key() =>
        Assert.Equal("Missing_Key", new Texts(CoreResources.Report, TecLanguage.English.Culture).Get("Missing_Key"));

    [Fact]
    public async Task Report_is_written_in_the_session_language()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tec-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var session = new EvidenceSession("s1", "app", DateTimeOffset.Now, directory);
            var sink = new HtmlReportSink(new SequentialEvidenceFileNamer("png"), new FixedClock(), TecLanguage.English);

            await sink.OnSessionStartedAsync(session, CancellationToken.None);
            await sink.OnSessionCompletedAsync(session, CancellationToken.None);

            var html = await File.ReadAllTextAsync(Path.Combine(directory, HtmlReportSink.FileName));
            Assert.Contains("<html lang=\"en\">", html, StringComparison.Ordinal);
            Assert.Contains("Test evidence — app", html, StringComparison.Ordinal);
            Assert.Contains("Finished:", html, StringComparison.Ordinal);
            Assert.DoesNotContain("Evidencia", html, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private static SortedSet<string> ReadKeys(string file) =>
        new(XDocument.Load(file).Root!.Elements("data").Select(e => (string)e.Attribute("name")!), StringComparer.Ordinal);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tec.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Tec.slnx not found.");
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset Now { get; } = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
    }
}
