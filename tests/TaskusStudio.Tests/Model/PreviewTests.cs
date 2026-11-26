using System.IO.Abstractions;
using TaskusStudio.Model;

namespace TaskusStudio.Tests.Model;

public class PreviewTests
{
    private static IReadOnlyList<Row> Clients()
    {
        var rows = Preview.Rows(new FileSystem(), TestData.ClientsFile, null);
        rows.IsSuccess.Should().BeTrue(rows.IsFailure ? rows.Error : "");
        return rows.Value;
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "High")]
    public void Rows_ReadsClientsSample()
    {
        Clients().Should().HaveCount(3);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Rows_MissingSheet_IsFailureWithFileName()
    {
        var rows = Preview.Rows(new FileSystem(), TestData.ClientsFile, "NoSuchSheet");

        rows.IsFailure.Should().BeTrue();
        rows.Error.Should().StartWith("clients.xlsx: ");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Columns_InSheetOrder()
    {
        Preview.Columns(Clients()).Should().StartWith(["Kliento Nr", "Vardas"]).And.Contain("El. paštas");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "High")]
    public void Evaluate_ColumnByName()
    {
        var value = Preview.Evaluate("x", "{{ row['Kliento Nr'] }}", Clients()[0]);

        value.IsSuccess.Should().BeTrue(value.IsFailure ? value.Error : "");
        value.Value.Should().Be("K-001");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Evaluate_SingleNumericExpression_KeepsType()
    {
        var value = Preview.Evaluate("x", "{{ 1 + 2 }}", Clients()[0]);

        value.Value.Should().BeAssignableTo<IConvertible>().And.NotBeOfType<string>();
        Convert.ToDouble(value.Value).Should().Be(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Evaluate_TextAroundExpression_IsString()
    {
        var row = new Dictionary<string, object?> { ["n"] = 5 };

        Preview.Evaluate("x", "n={{ row.n }}", row).Value.Should().Be("n=5");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void Evaluate_BrokenTemplate_IsFailure()
    {
        var row = new Dictionary<string, object?> { ["n"] = 5 };

        Preview.Evaluate("x", "{{ row. }}", row).IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData("Vardas", "{{ row.Vardas }}", "row.Vardas")]
    [InlineData("_a1", "{{ row._a1 }}", "row._a1")]
    [InlineData("El. paštas", "{{ row['El. paštas'] }}", "row['El. paštas']")]
    [InlineData("1st", "{{ row['1st'] }}", "row['1st']")]
    [InlineData("it's", @"{{ row['it\'s'] }}", @"row['it\'s']")]
    [Trait("Category", "Unit")]
    public void ColumnTemplate_QuotesNonIdentifiers(string column, string template, string access)
    {
        Preview.ColumnTemplate(column).Should().Be(template);
        Preview.ColumnAccess(column).Should().Be(access);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ColumnTemplate_EvaluatesToTheColumn()
    {
        var row = new Dictionary<string, object?> { ["it's"] = "v" };

        Preview.Evaluate("x", Preview.ColumnTemplate("it's"), row).Value.Should().Be("v");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Format_ShowsType()
    {
        Preview.Format(null).Should().Be("null");
        Preview.Format("a").Should().Be("\"a\"");
        Preview.Format(1.5).Should().Be("1.5  (double)");
        Preview.Format(new DateTime(2026, 1, 2, 3, 4, 5)).Should().Be("2026-01-02 03:04:05  (date)");
        Preview.Format(true).Should().Be("true");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Files_SkipsExcelLockFilesAndMissingFolder()
    {
        var fs = new System.IO.Abstractions.TestingHelpers.MockFileSystem();
        fs.AddFile("/in/b.xlsx", new("x"));
        fs.AddFile("/in/a.xlsx", new("x"));
        fs.AddFile("/in/~$a.xlsx", new("x"));
        fs.AddFile("/in/c.csv", new("x"));

        Preview.Files(fs, "/in", "").Select(fs.Path.GetFileName).Should().Equal("a.xlsx", "b.xlsx");
        Preview.Files(fs, "/missing", "*.xlsx").Should().BeEmpty();
    }
}
