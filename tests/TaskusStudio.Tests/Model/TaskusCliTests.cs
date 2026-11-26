using TaskusStudio.Model;

namespace TaskusStudio.Tests.Model;

public class TaskusCliTests
{
    [Theory]
    [InlineData("GET", false)]
    [InlineData(" get ", false)]
    [InlineData("POST", true)]
    [InlineData("DELETE", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void SourceMayChangeData_Http(string? method, bool expected)
    {
        TaskusCli.SourceMayChangeData("http", method, null).Should().Be(expected);
    }

    [Theory]
    [InlineData("select 1", false)]
    [InlineData("  WITH x as (select 1) select * from x", false)]
    [InlineData("-- c\nselect 1", false)]
    [InlineData("(select 1)", false)]
    [InlineData("delete from t", true)]
    [InlineData("selection_update()", true)]
    [InlineData("", true)]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void SourceMayChangeData_Sql(string query, bool expected)
    {
        TaskusCli.SourceMayChangeData("postgres", null, query).Should().Be(expected);
        TaskusCli.SourceMayChangeData("odbc", null, query).Should().Be(expected);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SourceMayChangeData_Excel_False()
    {
        TaskusCli.SourceMayChangeData("excel", "POST", "delete from t").Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CoreVersion_IsReferencedTaskusCore()
    {
        TaskusCli.CoreVersion.Should().Be("0.10");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Priority", "High")]
    public void VersionMismatch_SameMajorMinor_IsNull()
    {
        TaskusCli.VersionMismatch("0.10.3").Should().BeNull();
        TaskusCli.VersionMismatch("0.10.0-beta+abc").Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void VersionMismatch_OtherVersion_NamesBoth()
    {
        TaskusCli.VersionMismatch("0.7.1").Should().Contain("0.7.1").And.Contain("0.10.x");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void VersionMismatch_NoVersion_IsMessage()
    {
        TaskusCli.VersionMismatch(null).Should().Contain("no version");
    }

    [Theory]
    [InlineData("0.8.3", "0.8")]
    [InlineData("1.2", "1.2")]
    [InlineData("1.2-rc1", "1.2")]
    [InlineData("1.2.3+sha", "1.2")]
    [InlineData("3", "3")]
    [Trait("Category", "Unit")]
    public void MajorMinor_TakesFirstTwoParts(string version, string expected)
    {
        TaskusCli.MajorMinor(version).Should().Be(expected);
    }
}
