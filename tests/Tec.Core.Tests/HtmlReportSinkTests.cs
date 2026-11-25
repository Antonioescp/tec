using Tec.Core.Localization;
using Tec.Core.Model;
using Tec.Core.Reporting;
using Tec.Core.Services;

namespace Tec.Core.Tests;

public class HtmlReportSinkTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task The_title_lists_every_observed_application()
    {
        var directory = CreateSessionDirectory();
        try
        {
            var session = new EvidenceSession("s1", "Farmax+Postman", Now, directory);
            var sink = new HtmlReportSink(new SequentialEvidenceFileNamer(".png"), SystemClock.Instance, TecLanguage.Spanish);

            await sink.OnSessionStartedAsync(session, CancellationToken.None);

            var html = await File.ReadAllTextAsync(Path.Combine(directory, HtmlReportSink.FileName));
            Assert.Contains("<h1>Evidencia de prueba — Farmax+Postman</h1>", html, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task The_report_notes_the_applications_that_stopped_running()
    {
        var directory = CreateSessionDirectory();
        try
        {
            var session = new EvidenceSession("s1", "Farmax+Postman", Now, directory);
            var sink = new HtmlReportSink(
                new SequentialEvidenceFileNamer(".png"),
                SystemClock.Instance,
                TecLanguage.Spanish,
                () => ["Postman"]);

            await sink.OnSessionStartedAsync(session, CancellationToken.None);

            var html = await File.ReadAllTextAsync(Path.Combine(directory, HtmlReportSink.FileName));
            // The label is HTML-encoded, so the accent of "sesión" shows as &#243;.
            Assert.Contains("<p class=\"lost\">Aplicaciones que dejaron de existir durante la sesi&#243;n: Postman</p>", html, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task The_report_omits_the_note_while_every_application_is_alive()
    {
        var directory = CreateSessionDirectory();
        try
        {
            var session = new EvidenceSession("s1", "Farmax+Postman", Now, directory);
            var sink = new HtmlReportSink(
                new SequentialEvidenceFileNamer(".png"),
                SystemClock.Instance,
                TecLanguage.Spanish,
                () => []);

            await sink.OnSessionStartedAsync(session, CancellationToken.None);

            var html = await File.ReadAllTextAsync(Path.Combine(directory, HtmlReportSink.FileName));
            Assert.DoesNotContain("dejaron de existir", html, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task A_named_session_shows_its_name_and_still_lists_the_applications()
    {
        var directory = CreateSessionDirectory();
        try
        {
            var session = new EvidenceSession("s1", "Farmax+Postman", Now, directory, "Login con Postman");
            var sink = new HtmlReportSink(new SequentialEvidenceFileNamer(".png"), SystemClock.Instance, TecLanguage.Spanish);

            await sink.OnSessionStartedAsync(session, CancellationToken.None);

            var html = await File.ReadAllTextAsync(Path.Combine(directory, HtmlReportSink.FileName));
            Assert.Contains("<h1>Evidencia de prueba — Login con Postman</h1>", html, StringComparison.Ordinal);
            Assert.Contains("<p>Aplicaciones: Farmax+Postman</p>", html, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task A_session_without_a_name_does_not_repeat_the_applications()
    {
        var directory = CreateSessionDirectory();
        try
        {
            var session = new EvidenceSession("s1", "Farmax+Postman", Now, directory);
            var sink = new HtmlReportSink(new SequentialEvidenceFileNamer(".png"), SystemClock.Instance, TecLanguage.Spanish);

            await sink.OnSessionStartedAsync(session, CancellationToken.None);

            var html = await File.ReadAllTextAsync(Path.Combine(directory, HtmlReportSink.FileName));
            Assert.DoesNotContain("<p>Aplicaciones:", html, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static string CreateSessionDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { /* left behind in the temp folder */ }
    }
}
