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
    [Trait("Priority", "High")]
    public void ExcelSourceFolder_FillsProcessedAndFailed_WhileTheyFollowIt()
    {
        var doc = TestData.NewDocument();
        var folder = doc.Source.Field("folder")!;

        folder.Text = "in";
        folder.Text = "inbox";

        doc.Source.Field("processed")!.Text.Should().Be(Path.Join("inbox", "done"));
        doc.Source.Field("failed")!.Text.Should().Be(Path.Join("inbox", "failed"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ExcelSourceFolder_KeepsAProcessedFolderTheUserChose()
    {
        var doc = TestData.NewDocument();
        doc.Source.Field("processed")!.Text = "archive";

        doc.Source.Field("folder")!.Text = "in";

        doc.Source.Field("processed")!.Text.Should().Be("archive");
        doc.Source.Field("failed")!.Text.Should().Be(Path.Join("in", "failed"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void LoadingATaskFile_DoesNotAddProcessedOrFailed()
    {
        var doc = TestData.NewDocument();

        doc.LoadText("source:\n  type: excel\n  folder: in\nsink:\n  type: excel\n  folder: out\n  file: x.xlsx\n", null);

        doc.Source.Field("processed")!.Text.Should().BeEmpty();
        doc.YamlText.Should().NotContain("processed").And.NotContain("failed");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FolderFields_HaveBrowse_OtherTextFieldsDoNot()
    {
        var doc = TestData.NewDocument();

        doc.Source.Field("folder")!.IsFolder.Should().BeTrue();
        doc.Source.Field("processed")!.IsFolder.Should().BeTrue();
        doc.Sink.Field("folder")!.IsFolder.Should().BeTrue();
        doc.Source.Field("pattern")!.IsFolder.Should().BeFalse();
        doc.Sink.Field("file")!.IsFolder.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HttpSourceBody_IsAMultilineTemplate_UrlIsNot()
    {
        var doc = TestData.NewDocument();
        doc.Source.Type = "http";

        doc.Source.Field("body")!.IsMultilineTemplate.Should().BeTrue();
        doc.Source.Field("url")!.IsMultilineTemplate.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TemplateHint_NamesColumnCompletion_OnlyWhenThereAreColumns()
    {
        var doc = TestData.NewDocument();
        doc.Sink.Type = "http";
        var url = doc.Sink.Field("url")!;
        url.TemplateHint.Should().NotContain("row.");

        doc.AddColumn("Vardas");

        url.TemplateHint.Should().Contain("row.");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void HeaderSecret_AppendsAfterAPrefix_AndReplacesAnEarlierOne()
    {
        var doc = TestData.NewDocument();
        doc.Sink.Type = "http";
        var headers = doc.Sink.Field("headers")!;
        headers.AddEntryCommand.Execute(null);
        var entry = headers.Entries[0];
        entry.Key = "Authorization";
        entry.Value = "Bearer ";

        entry.SelectedSecret = "api_token";
        entry.Value.Should().Be("Bearer ${secret:api_token}");

        entry.SelectedSecret = "other_token";
        entry.Value.Should().Be("Bearer ${secret:other_token}");
        doc.YamlText.Should().Contain("Authorization: Bearer ${secret:other_token}");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void HeaderEntries_GetTheSecretNamesOfTheDocument()
    {
        var doc = TestData.NewDocument();
        doc.Sink.Type = "http";
        var headers = doc.Sink.Field("headers")!;
        headers.AddEntryCommand.Execute(null);

        headers.SecretNames = ["a", "b"];

        headers.Entries[0].SecretNames.Should().Equal("a", "b");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Output_StartsWithTheFullStudioVersion()
    {
        var doc = TestData.NewDocument();

        doc.Output[0].Text.Should().StartWith($"Taskus Studio {TaskusStudio.Model.TaskusCli.StudioVersion}");
        TaskusStudio.Model.TaskusCli.StudioVersion.Should().MatchRegex(@"^\d+\.\d+\.\d+");
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
