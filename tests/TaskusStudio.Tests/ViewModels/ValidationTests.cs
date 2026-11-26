using TaskusStudio.Model;
using TaskusStudio.ViewModels;

namespace TaskusStudio.Tests.ViewModels;

public class ValidationTests
{
    private const string EXCEL_TO_EXCEL = "source:\n  type: excel\n  folder: in\nsink:\n  type: excel\n  folder: out\n  file: a.xlsx\n";

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidTask_HasNoProblems()
    {
        Validation.Check(EXCEL_TO_EXCEL, "t", null).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void MissingSetting_PathPointsToIt()
    {
        var problems = Validation.Check(EXCEL_TO_EXCEL.Replace("  file: a.xlsx\n", ""), "t", null);

        problems.Select(p => p.Path).Should().Equal("sink.file");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void BrokenTemplate_PathIsMapField()
    {
        var problems = Validation.Check(EXCEL_TO_EXCEL + "map:\n  x: \"{{ row. }}\"\n", "t", null);

        problems.Select(p => p.Path).Should().Equal("map.x");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void NoSecretsFile_ReferencesGetStandIns()
    {
        var text = "source:\n  type: postgres\n  connection: ${secret:pg}\n  query: select 1\n"
            + "sink:\n  type: smtp\nsmtp: ${secret:smtp}\n";

        Validation.Check(text, "t", null).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void LoadedSecrets_MissingNameIsAProblem()
    {
        var text = "source:\n  type: postgres\n  connection: ${secret:nope}\n  query: select 1\n"
            + "sink:\n  type: excel\n  folder: out\n  file: a.xlsx\n";

        Validation.Check(text, "t", TestData.Secrets()).Should().ContainSingle()
            .Which.Message.Should().Contain("nope");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BrokenTemplate_FirstProblemIsMapField()
    {
        var problems = Validation.Check(EXCEL_TO_EXCEL + "map:\n  x: \"{{ row.a | }}\"\n", "t", null);

        problems.Should().NotBeEmpty();
        problems[0].Path.Should().Be("map.x");
        problems[0].Message.Should().NotStartWith("field ");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Split_CutsOnlyBeforeASettingPath()
    {
        Validation.Split("source.folder: is required; sink.file: bad; part; of one message").Should().Equal(
            new Problem("source.folder", "is required"),
            new Problem("sink.file", "bad; part; of one message"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void Target_FindsFieldViewModels()
    {
        var vm = TestData.NewDocument();
        vm.Sink.Type = "http";
        vm.Sink.Field("retry")!.IsEnabled = true;
        vm.AddColumn("name");

        vm.Target("source.folder").Should().BeSameAs(vm.Source.Field("folder"));
        vm.Target("sink.retry.attempts").Should().BeSameAs(vm.Sink.Field("retry")!.Nested!.Field("attempts"));
        vm.Target("map.name").Should().BeSameAs(vm.MapEntries.Single());
        vm.Target("report.folder").Should().BeSameAs(vm.Run.Field("report")!.Nested!.Field("folder"));
        vm.Target("onError").Should().BeSameAs(vm.Run.Field("onError"));
        vm.Target("map.other").Should().BeNull();
        vm.Target("").Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void Refresh_SetsFieldErrors()
    {
        var vm = TestData.NewDocument();

        vm.LoadText(EXCEL_TO_EXCEL.Replace("  folder: in\n", ""), null);

        vm.Source.Field("folder")!.Error.Should().NotBeNullOrEmpty();
        vm.Sink.Field("folder")!.Error.Should().BeNull();
        vm.IsValid.Should().BeFalse();
        vm.Problems.Should().ContainSingle(p => p.Path == "source.folder");

        vm.Source.Field("folder")!.Text = "in";

        vm.Source.Field("folder")!.Error.Should().BeNull();
        vm.IsValid.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Refresh_SetsMapEntryError()
    {
        var vm = TestData.NewDocument();
        vm.LoadText(EXCEL_TO_EXCEL, null);

        vm.AddColumn("x");
        vm.MapEntries[0].Template = "{{ row. }}";

        vm.MapEntries[0].Error.Should().NotBeNullOrEmpty();
    }
}
