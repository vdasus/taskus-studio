namespace TaskusStudio.Tests.ViewModels;

public class PreviewViewModelTests
{
    private static string Task(string folder) =>
        $"source:\n  type: excel\n  folder: '{folder}'\nmap:\n  code: \"{{{{ row['Kliento Nr'] }}}}\"\n  bad: \"{{{{ row. }}}}\"\nsink:\n  type: excel\n  folder: out\n  file: a.xlsx\n";

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "High")]
    public void ExcelSourceFolder_LoadsColumnsAndMapPreview()
    {
        var vm = TestData.NewDocument();

        vm.LoadText(Task(TestData.Folder), null);

        vm.PreviewFiles.Should().Equal(TestData.ClientsFile);
        vm.HasPreview.Should().BeTrue();
        vm.Columns.Should().Contain(["Kliento Nr", "El. paštas"]);
        vm.RowLabel.Should().Be("row 1 of 3");
        vm.MapEntries[0].Preview.Should().Be("\"K-001\"");
        vm.MapEntries[0].PreviewFailed.Should().BeFalse();
        vm.MapEntries[1].Error.Should().Contain("dot operator", "a template error shows under the line");
        vm.MapEntries[1].Preview.Should().BeEmpty("the error is shown once, not again as preview");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void NextRow_MovesPreview()
    {
        var vm = TestData.NewDocument();
        vm.LoadText(Task(TestData.Folder), null);
        var first = vm.MapEntries[0].Preview;

        vm.NextRowCommand.Execute(null);

        vm.RowLabel.Should().Be("row 2 of 3");
        vm.MapEntries[0].Preview.Should().NotBe(first);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void EmptyFolder_GivesStatusAndNoPreview()
    {
        using var temp = new TempFolder();
        var vm = TestData.NewDocument();

        vm.LoadText(Task(temp.Path), null);

        vm.HasPreview.Should().BeFalse();
        vm.Columns.Should().BeEmpty();
        vm.PreviewStatus.Should().Contain("No file matching");
        vm.MapEntries[0].Preview.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void AddColumn_MakesUniqueFieldNames()
    {
        var vm = TestData.NewDocument();

        vm.AddColumn("Vardas");
        vm.AddColumn("vardas");
        vm.AddColumn("Vardas");
        vm.AddColumn("El. paštas");

        vm.MapEntries.Select(e => e.Field).Should().Equal("Vardas", "vardas2", "Vardas3", "El. paštas");
        vm.MapEntries[3].Template.Should().Be("{{ row['El. paštas'] }}");
    }
}
