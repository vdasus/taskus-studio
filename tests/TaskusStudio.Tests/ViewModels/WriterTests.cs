using TaskusStudio.Model;
using YamlDotNet.RepresentationModel;

namespace TaskusStudio.Tests.ViewModels;

public class WriterTests
{
    private static List<string> Keys(string yaml) =>
        TaskYaml.Read(yaml).Value.Children.Keys.Select(TaskYaml.Text).ToList();

    private static YamlMappingNode Section(string yaml, string key) =>
        (YamlMappingNode)TaskYaml.Read(yaml).Value.Children[new YamlScalarNode(key)];

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void BlankDocument_WritesOnlyTypes()
    {
        var vm = TestData.NewDocument();

        vm.YamlText.Should().Be($"{TaskYaml.SchemaHeader}\nsource:\n  type: excel\nsink:\n  type: excel\n");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TypeChange_WritesNoDefaults()
    {
        var vm = TestData.NewDocument();

        vm.Source.Type = "http";
        vm.Sink.Type = "http";

        Section(vm.YamlText, "source").Children.Keys.Select(TaskYaml.Text).Should().Equal("type");
        Section(vm.YamlText, "sink").Children.Keys.Select(TaskYaml.Text).Should().Equal("type");
        vm.YamlText.Should().NotContainAny("timeout", "method", "rateLimit", "onError", "maxErrors", "report", "pattern");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void KeyOrder_NameSourceMapSinkThenRunSettings()
    {
        var vm = TestData.NewDocument();

        vm.LoadText(TestData.Template("excel-to-postgres"), null);

        Keys(vm.YamlText).Should().Equal("name", "source", "map", "sink", "onError", "maxErrors", "report", "smtp");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FieldOrder_FollowsSchema()
    {
        var vm = TestData.NewDocument();

        vm.LoadText(TestData.Template("excel-to-postgres"), null);

        Section(vm.YamlText, "source").Children.Keys.Select(TaskYaml.Text).Should().Equal("type", "folder", "pattern", "processed", "failed");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void MultilineQuery_IsLiteralBlock()
    {
        var vm = TestData.NewDocument();

        vm.LoadText(TestData.Template("postgres-to-excel"), null);

        vm.YamlText.Should().MatchRegex(@"  query: \|-?\n").And.Contain("\n    select id, client_name, amount, issued\n    from invoices\n");
    }

    [Theory]
    [InlineData("{{ row.id }}")]
    [InlineData("{{ row['Kliento Nr'] | string.strip }}")]
    [InlineData("INV-{{ row.id }}")]
    [InlineData("{{ row.a }}: {{ row.b }}")]
    [InlineData("#{{ row.a }}")]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void Templates_ReadBackUnchanged(string template)
    {
        var vm = TestData.NewDocument();

        vm.AddColumn("x");
        vm.MapEntries[0].Template = template;

        var map = Section(vm.YamlText, "map");
        TaskYaml.Text(map.Children[new YamlScalarNode("x")]).Should().Be(template);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BoolAtDefault_IsLeftOut()
    {
        var vm = TestData.NewDocument();
        var overwrite = vm.Sink.Field("overwrite")!;

        overwrite.BoolValue = true;
        vm.YamlText.Should().Contain("overwrite: true");

        overwrite.BoolValue = false;
        vm.YamlText.Should().NotContain("overwrite");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MapEntriesWithoutField_AreLeftOut()
    {
        var vm = TestData.NewDocument();

        vm.MapEntries.Add(new TaskusStudio.ViewModels.MapEntryViewModel(() => { }, "  ", "{{ row.a }}"));
        vm.Refresh();

        Keys(vm.YamlText).Should().NotContain("map");
    }
}
