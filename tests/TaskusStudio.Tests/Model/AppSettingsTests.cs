using TaskusStudio.Model;

namespace TaskusStudio.Tests.Model;

public class AppSettingsTests
{
    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "High")]
    public void Location_WritableAppFolder_IsUsed()
    {
        using var app = new TempFolder();
        using var user = new TempFolder();

        AppSettings.Location(app.Path, user.Path).Should().Be(Path.Combine(app.Path, AppSettings.FILE_NAME));
        Directory.EnumerateFileSystemEntries(app.Path).Should().BeEmpty("the write probe is removed");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void PanelWidths_SurviveSaveAndLoad()
    {
        using var app = new TempFolder();
        var settings = AppSettings.Load(app.Path, app.Path);
        settings.PanelWidths = [250, 600.5, 300];

        settings.Save();

        AppSettings.Load(app.Path, app.Path).PanelWidths.Should().Equal(250, 600.5, 300);
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "High")]
    public void Location_MissingAppFolder_FallsBackToUserFolder()
    {
        using var user = new TempFolder();
        var app = Path.Combine(user.Path, "no-such-folder");

        AppSettings.Location(app, user.Path).Should().Be(Path.Combine(user.Path, AppSettings.FILE_NAME));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Location_UnwritableAppFolder_FallsBackToUserFolder()
    {
        using var user = new TempFolder();
        var app = Path.Combine(Path.GetPathRoot(user.Path)!, $"no-such-root-{Guid.NewGuid():N}", "app");

        AppSettings.Location(app, user.Path).Should().Be(Path.Combine(user.Path, AppSettings.FILE_NAME));
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "High")]
    public void SaveLoad_RoundTrip()
    {
        using var app = new TempFolder();
        using var user = new TempFolder();
        var settings = AppSettings.Load(app.Path, user.Path);
        settings.TaskusPath = "/opt/taskus/taskus";
        settings.SecretsPath = "/opt/taskus/secrets.yaml";
        settings.Theme = ThemeChoice.Dark;
        settings.AddRecent("/a.yaml");

        settings.Save();
        var loaded = AppSettings.Load(app.Path, user.Path);

        loaded.Should().BeEquivalentTo(settings);
        loaded.FilePath.Should().Be(Path.Combine(app.Path, AppSettings.FILE_NAME));
        File.ReadAllText(loaded.FilePath).Should().Contain("\"theme\": \"dark\"");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Load_BrokenFile_GivesDefaults()
    {
        using var app = new TempFolder();
        File.WriteAllText(Path.Combine(app.Path, AppSettings.FILE_NAME), "{ not json");

        var settings = AppSettings.Load(app.Path, app.Path);

        settings.TaskusPath.Should().BeEmpty();
        settings.RecentFiles.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void AddRecent_DedupesNewestFirstMaxTen()
    {
        var settings = new AppSettings();
        for (var i = 1; i <= 12; i++)
            settings.AddRecent($"/f{i}.yaml");

        settings.AddRecent("/F5.YAML");

        settings.RecentFiles.Should().HaveCount(10);
        settings.RecentFiles[0].Should().Be("/F5.YAML");
        settings.RecentFiles.Should().NotContain("/f5.yaml");
        settings.RecentFiles.Skip(1).Should().Equal("/f12.yaml", "/f11.yaml", "/f10.yaml", "/f9.yaml", "/f8.yaml", "/f7.yaml", "/f6.yaml", "/f4.yaml", "/f3.yaml");
    }
}
