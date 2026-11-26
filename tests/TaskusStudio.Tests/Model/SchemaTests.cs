using TaskusStudio.Model;

namespace TaskusStudio.Tests.Model;

public class SchemaTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void SourceTypes_MatchTaskus()
    {
        Schema.SourceTypes.Should().Equal("excel", "postgres", "odbc", "http");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void SinkTypes_MatchTaskus()
    {
        Schema.SinkTypes.Should().Equal("excel", "postgres", "odbc", "smtp", "http");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void ExcelSource_FolderRequired_PatternOptionalWithDefault()
    {
        var fields = Schema.Fields(Schema.SourceType("excel"));

        var folder = fields.Single(f => f.Name == "folder");
        folder.Required.Should().BeTrue();
        folder.DefaultText.Should().BeNull();

        var pattern = fields.Single(f => f.Name == "pattern");
        pattern.Required.Should().BeFalse();
        pattern.DefaultText.Should().Be("*.xlsx");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Fields_KeepDeclarationOrder()
    {
        Schema.Fields(Schema.SourceType("excel")).Select(f => f.Name)
            .Should().Equal("folder", "pattern", "sheet", "processed", "failed");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void HttpMethod_UsesTaskusSpelling()
    {
        var method = Schema.Fields(Schema.SourceType("http")).Single(f => f.Name == "method");

        method.Kind.Should().Be(FieldKind.Enum);
        method.EnumValues.Should().Equal("GET", "POST", "PUT", "PATCH", "DELETE");
        method.DefaultText.Should().Be("GET");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OnError_IsCamelCaseEnum()
    {
        var onError = Schema.RunSettings.Single(f => f.Name == "onError");

        onError.EnumValues.Should().Equal("stop", "continue");
        onError.DefaultText.Should().Be("stop");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ReportWhen_DefaultIsOnError()
    {
        var report = Schema.RunSettings.Single(f => f.Name == "report");
        var when = report.Children.Single(f => f.Name == "when");

        when.EnumValues.Should().Equal("always", "onError", "never");
        when.DefaultText.Should().Be("onError");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RunSettings_ExcludeSourceMapSink()
    {
        Schema.RunSettings.Select(f => f.Name)
            .Should().Equal("name", "onError", "maxErrors", "report", "smtp", "logExclude");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Kinds_FollowPropertyTypes()
    {
        var sink = Schema.Fields(Schema.SinkType("http")).ToDictionary(f => f.Name);
        sink["headers"].Kind.Should().Be(FieldKind.Dictionary);
        sink["timeout"].Kind.Should().Be(FieldKind.Number);
        sink["timeout"].DefaultText.Should().Be("100");
        sink["retry"].Kind.Should().Be(FieldKind.Object);
        sink["retry"].Children.Select(c => c.Name).Should().Equal("attempts", "delay");

        Schema.Fields(Schema.SourceType("postgres")).Single(f => f.Name == "query").Kind.Should().Be(FieldKind.MultilineText);
        Schema.Fields(Schema.SourceType("postgres")).Single(f => f.Name == "connection").ExpectsSecret.Should().BeTrue();
    }
}
