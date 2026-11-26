using Taskus.Config;
using TaskusStudio.Model;
using TaskusStudio.ViewModels;

namespace TaskusStudio.Tests.ViewModels;

public class RoundTripTests
{
    public static TheoryData<string> Templates => new(TemplateInfo.All.Select(t => t.Name));

    public static IEnumerable<TheoryDataRow<string>> RoundTripTemplates => TemplateInfo.All.Select(t => new TheoryDataRow<string>(t.Name));

    [Fact]
    [Trait("Category", "Unit")]
    public void Templates_AreTheEmbeddedExamples()
    {
        TemplateInfo.All.Select(t => t.Name).Should().Contain(["blank", "excel-to-postgres", "postgres-to-excel", "excel-to-mail", "api-to-excel"]);
        TemplateInfo.All.Single(t => t.Name == "excel-to-postgres").Description.Should().StartWith("Imports every .xlsx");
    }

    [Theory]
    [MemberData(nameof(RoundTripTemplates))]
    [Trait("Category", "Integration")]
    [Trait("Priority", "High")]
    public void Template_WrittenByEditor_ParsesToSameTask(string name)
    {
        var original = TestData.Template(name);
        var secrets = TestData.Secrets();
        var vm = TestData.NewDocument();

        vm.LoadText(original, null).Should().BeTrue();
        var written = TaskYaml.Write(vm.ToYaml());

        var expected = TaskLoader.Parse(original, name, secrets);
        var actual = TaskLoader.Parse(written, name, secrets);
        if (expected.IsFailure)
        {
            actual.IsFailure.Should().BeTrue();
            actual.Error.Should().Be(expected.Error);
            return;
        }

        actual.IsSuccess.Should().BeTrue(actual.IsFailure ? $"{actual.Error}\n{written}" : "");
        actual.Value.Should().BeEquivalentTo(expected.Value, o => o.RespectingRuntimeTypes());
    }

    [Theory]
    [MemberData(nameof(Templates))]
    [Trait("Category", "Integration")]
    public void Template_LoadsWithoutUnknownKeys(string name)
    {
        var vm = TestData.NewDocument();

        vm.LoadText(TestData.Template(name), null);

        vm.FileWarning.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RespectingRuntimeTypes_SeesSourceDifferences()
    {
        // Guards the comparison above: a different source folder must not compare equal.
        var secrets = TestData.Secrets();
        var a = TaskLoader.Parse(TestData.Template("excel-to-postgres"), "t", secrets).Value;
        var b = TaskLoader.Parse(TestData.Template("excel-to-postgres").Replace(@"C:\Taskus\in\clients\done", @"X:\done"), "t", secrets).Value;

        var act = () => b.Should().BeEquivalentTo(a, o => o.RespectingRuntimeTypes());

        act.Should().Throw<Exception>();
    }
}
