using TaskusStudio.ViewModels;

namespace TaskusStudio.Tests.ViewModels;

public class FieldTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void EnumChoice_NullOnATextField_KeepsTheText()
    {
        // Every field view has a hidden enum box; when it loses its item it writes null back.
        var doc = TestData.NewDocument();
        var folder = doc.Source.Field("folder")!;
        folder.Text = @"C:\in";

        folder.EnumChoice = null!;

        folder.Text.Should().Be(@"C:\in");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void EnumChoice_Default_ClearsTheValue()
    {
        var doc = TestData.NewDocument();
        var onError = doc.Run.Field("onError")!;
        onError.EnumChoice = "continue";

        onError.EnumChoice = FieldViewModel.DEFAULT_CHOICE;

        onError.Text.Should().BeEmpty();
        doc.YamlText.Should().NotContain("onError");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void SelectedSecret_OnSmtp_WritesTheReferenceInsteadOfTheSection()
    {
        var doc = TestData.NewDocument();
        var smtp = doc.Run.Field("smtp")!;
        smtp.IsEnabled = true;

        smtp.SelectedSecret = "smtp_main";

        smtp.IsEnabled.Should().BeFalse();
        smtp.SelectedSecret.Should().Be("smtp_main");
        doc.YamlText.Should().Contain("smtp: ${secret:smtp_main}");
        doc.Problems.Should().NotContain(p => p.Path.StartsWith("smtp", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void IsEnabled_OnSmtpWithAReference_DropsTheReference()
    {
        var doc = TestData.NewDocument();
        var smtp = doc.Run.Field("smtp")!;
        smtp.SelectedSecret = "smtp_main";

        smtp.IsEnabled = true;

        smtp.Text.Should().BeEmpty();
        doc.YamlText.Should().NotContain("${secret:smtp_main}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectedSecret_OnAConnection_ReplacesTheRawValue()
    {
        var doc = TestData.NewDocument();
        doc.Sink.Type = "postgres";
        var connection = doc.Sink.Field("connection")!;
        connection.Text = "Host=db;Password=x";
        connection.Warning.Should().NotBeNull();

        connection.SelectedSecret = "pg";

        connection.Text.Should().Be("${secret:pg}");
        connection.Warning.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void SinkTemplateFields_CompleteTheMappedFieldNames()
    {
        var doc = TestData.NewDocument();
        doc.Sink.Type = "http";
        doc.AddColumn("Vardas");
        doc.AddColumn("El. paštas");

        var url = doc.Sink.Field("url")!;

        url.IsTemplate.Should().BeTrue();
        url.IsText.Should().BeFalse();
        url.TemplateColumns.Should().Equal("Vardas", "El. paštas");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PlainFields_AreNotTemplates()
    {
        var doc = TestData.NewDocument();
        doc.Sink.Type = "http";

        doc.Sink.Field("timeout")!.IsTemplate.Should().BeFalse();
        doc.Source.Field("folder")!.IsTemplate.Should().BeFalse();
    }
}
