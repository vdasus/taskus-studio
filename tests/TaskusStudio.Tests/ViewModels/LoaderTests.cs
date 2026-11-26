namespace TaskusStudio.Tests.ViewModels;

public class LoaderTests
{
    private const string TASK = "source:\n  type: excel\n  folder: in\nsink:\n  type: excel\n  folder: out\n  file: a.xlsx\n";

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void UnknownKeys_AreReported()
    {
        var vm = TestData.NewDocument();

        vm.LoadText(TASK.Replace("  folder: in\n", "  folder: in\n  bogus: 1\n") + "extra: 2\n", null).Should().BeTrue();

        vm.FileWarning.Should().Contain("source.bogus").And.Contain("extra");
        vm.Source.Field("folder")!.Text.Should().Be("in");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void UnknownSourceType_IsReported()
    {
        var vm = TestData.NewDocument();

        vm.LoadText(TASK.Replace("type: excel\n  folder: in", "type: ftp"), null);

        vm.FileWarning.Should().Contain("source.type: ftp");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void Comments_GiveWarning()
    {
        var vm = TestData.NewDocument();

        vm.LoadText("# a note\n" + TASK, "task.yaml");

        vm.FileWarning.Should().Contain("comments");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void NoComments_NoWarning()
    {
        var vm = TestData.NewDocument();

        vm.LoadText($"{TaskusStudio.Model.TaskYaml.SchemaHeader}\n{TASK}", "task.yaml");

        vm.FileWarning.Should().BeNull();
        vm.FilePath.Should().Be("task.yaml");
        vm.IsDirty.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BrokenYaml_KeepsDocument()
    {
        var vm = TestData.NewDocument();
        vm.LoadText(TASK, null);

        vm.LoadText("source: [", null).Should().BeFalse();

        vm.FileWarning.Should().StartWith("Cannot read the file");
        vm.Source.Field("folder")!.Text.Should().Be("in");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Edit_MarksDirty()
    {
        var vm = TestData.NewDocument();
        vm.LoadText(TASK, null);

        vm.Source.Field("folder")!.Text = "other";

        vm.IsDirty.Should().BeTrue();
        vm.YamlText.Should().Contain("folder: other");
    }
}
