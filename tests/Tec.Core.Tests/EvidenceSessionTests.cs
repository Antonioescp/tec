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
}
