using Tec.Core.Model;

namespace Tec.Core.Tests;

public class EvidenceSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void One_application_keeps_the_folder_name_used_so_far()
    {
        var session = EvidenceSession.Create("root", ["Farmax"], Now);

        Assert.Equal("Farmax", session.TargetDescription);
        Assert.Equal(Path.Combine("root", "20261124_090000_Farmax"), session.OutputDirectory);
    }

    [Fact]
    public void Several_applications_are_joined_in_the_folder_name()
    {
        var session = EvidenceSession.Create("root", ["Farmax", "Postman"], Now);

        Assert.Equal("Farmax+Postman", session.TargetDescription);
        Assert.Equal(Path.Combine("root", "20261124_090000_Farmax+Postman"), session.OutputDirectory);
    }

    [Fact]
    public void Names_that_do_not_fit_keep_the_first_ones_and_count_the_rest()
    {
        var names = new[] { "AplicacionMuyLargaDePrueba", "OtraAplicacionTambienLarga", "UnaTerceraApp" };

        var description = EvidenceSession.DescribeTargets(names);

        Assert.StartsWith("AplicacionMuyLargaDePrueba+", description, StringComparison.Ordinal);
        Assert.EndsWith("+2", description, StringComparison.Ordinal);
        Assert.True(description.Length <= EvidenceSession.MaxDescriptionLength);
        Assert.DoesNotContain("UnaTerceraApp", description, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_set_still_produces_a_usable_folder()
    {
        var session = EvidenceSession.Create("root", [" "], Now);

        Assert.Equal("sesion", session.TargetDescription);
    }

    [Fact]
    public void A_name_from_the_tester_keeps_the_date_prefix_and_the_applications()
    {
        var session = EvidenceSession.Create("root", ["Farmax", "Postman"], "Login con Postman", Now);

        Assert.Equal("Login con Postman", session.Name);
        Assert.Equal("Farmax+Postman", session.TargetDescription);
        Assert.Equal(Path.Combine("root", "20261124_090000_Login_con_Postman"), session.OutputDirectory);
    }

    [Fact]
    public void A_blank_name_is_no_name_at_all()
    {
        var session = EvidenceSession.Create("root", ["Farmax"], "   ", Now);

        Assert.Equal("Farmax", session.Name);
    }

    [Theory]
    [InlineData("Prueba de login mal", "Prueba_de_login_mal")]
    [InlineData("Login.", "Login")]
    [InlineData("Login.. ", "Login")]
    public void The_written_name_is_made_folder_safe_in_the_folder_but_shown_as_written(string written, string folderPiece)
    {
        var session = EvidenceSession.Create("root", ["Farmax"], written, Now);

        Assert.Equal(written.Trim(), session.Name);
        Assert.EndsWith($"20261124_090000_{folderPiece}", session.Id, StringComparison.Ordinal);
    }

    [Fact]
    public void A_long_written_name_is_cut_to_the_same_limit_as_the_generated_one()
    {
        var session = EvidenceSession.Create("root", ["Farmax"], new string('n', EvidenceSession.MaxDescriptionLength + 10), Now);

        Assert.EndsWith(new string('n', EvidenceSession.MaxDescriptionLength), session.Id, StringComparison.Ordinal);
    }

    [Fact]
    public void A_name_with_nothing_usable_falls_back_to_the_applications()
    {
        var junk = new string(Path.GetInvalidFileNameChars().Take(3).ToArray());

        var session = EvidenceSession.Create("root", ["Farmax"], junk, Now);

        Assert.Equal("Farmax", session.Name);
    }

    [Fact]
    public void A_repeated_session_name_goes_to_the_next_free_folder()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var first = EvidenceSession.Create(root, ["Farmax"], "Login", Now);
            Directory.CreateDirectory(first.OutputDirectory);
            var second = EvidenceSession.Create(root, ["Farmax"], "Login", Now);
            Directory.CreateDirectory(second.OutputDirectory);

            var third = EvidenceSession.Create(root, ["Farmax"], "Login", Now);

            Assert.EndsWith("_Login", first.Id, StringComparison.Ordinal);
            Assert.EndsWith("_Login_2", second.Id, StringComparison.Ordinal);
            Assert.EndsWith("_Login_3", third.Id, StringComparison.Ordinal);
            Assert.Equal("Login", third.Name);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void The_name_field_shows_exactly_the_folder_a_nameless_session_would_get()
    {
        var root = CreateTemporaryRoot();
        try
        {
            string[] names = ["Farmax", "Postman"];

            var preview = EvidenceSession.PreviewSessionName(names, Now);
            var session = EvidenceSession.Create(root, names, Now);

            Assert.Equal("20261124_090000_Farmax+Postman", preview);
            Assert.Equal(session.Id, preview);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void The_name_field_applies_the_same_folder_rules_as_the_folder()
    {
        string[] names = ["AplicacionMuyLargaDePruebaConUnNombreEnorme"];

        var preview = EvidenceSession.PreviewSessionName(names, Now);
        var session = EvidenceSession.Create("root", names, Now);

        // The joined application names do not fit, and both trim them the same way.
        Assert.Equal(session.Id, preview);
        Assert.Equal("20261124_090000_".Length + EvidenceSession.MaxDescriptionLength, preview.Length);
    }

    private static string CreateTemporaryRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "tec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteDirectory(string directory)
    {
        try { Directory.Delete(directory, recursive: true); }
        catch (IOException) { /* left behind in the temp folder */ }
    }
}
