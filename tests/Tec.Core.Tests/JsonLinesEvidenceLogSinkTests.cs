using System.Text.Json;
using Tec.Core.Model;
using Tec.Core.Reporting;

namespace Tec.Core.Tests;

public class JsonLinesEvidenceLogSinkTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Writes_the_process_id_and_the_application_name()
    {
        var directory = CreateSessionDirectory();
        try
        {
            var session = new EvidenceSession("s1", "Farmax+Postman", Now, directory);
            var sink = new JsonLinesEvidenceLogSink(new SequentialEvidenceFileNamer(".png"));
            var evidence = Click(1, processId: 42, processName: "Postman");

            await sink.OnSessionStartedAsync(session, CancellationToken.None);
            await sink.WriteAsync(session, evidence, CancellationToken.None);

            var line = await File.ReadAllTextAsync(Path.Combine(directory, JsonLinesEvidenceLogSink.FileName));
            using var json = JsonDocument.Parse(line);
            Assert.Equal(42, json.RootElement.GetProperty("processId").GetInt32());
            Assert.Equal("Postman", json.RootElement.GetProperty("processName").GetString());
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Fact]
    public async Task Writes_a_null_application_name_when_it_is_unknown()
    {
        var directory = CreateSessionDirectory();
        try
        {
            var session = new EvidenceSession("s1", "Farmax", Now, directory);
            var sink = new JsonLinesEvidenceLogSink(new SequentialEvidenceFileNamer(".png"));

            await sink.WriteAsync(session, Click(1, processId: 7, processName: null), CancellationToken.None);

            var line = await File.ReadAllTextAsync(Path.Combine(directory, JsonLinesEvidenceLogSink.FileName));
            using var json = JsonDocument.Parse(line);
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("processName").ValueKind);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static Evidence Click(int sequence, int processId, string? processName) => new(
        sequence,
        new Interaction
        {
            Kind = InteractionKind.LeftClick,
            Timestamp = Now,
            ProcessId = processId,
            ProcessName = processName,
            WindowTitle = "Principal",
        },
        new InteractionCaption($"#{sequence:000} · Click izquierdo", "detalle"),
        new ScreenRect(0, 0, 10, 10),
        RgbColor.Red,
        RasterImage.Solid(10, 10, RgbColor.White),
        Now);

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
