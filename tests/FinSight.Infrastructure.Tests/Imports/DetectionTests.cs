using System.Text;
using FinSight.Infrastructure.Imports;
using FluentAssertions;

namespace FinSight.Infrastructure.Tests.Imports;

public class DetectionTests
{
    // Built by reflection over every IStatementReader in the Infrastructure assembly (AddInfrastructure needs a
    // database/Redis/Hangfire configuration, which is too heavy for a unit test). Readers must be parameterless.
    private static readonly IReadOnlyList<IStatementReader> AllReaders = typeof(StatementParser).Assembly
        .GetTypes()
        .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IStatementReader).IsAssignableFrom(t))
        .Select(t => (IStatementReader)Activator.CreateInstance(t)!)
        .ToList();

    public static TheoryData<string> FixtureFiles()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Imports", "Fixtures"), "*.csv"))
            data.Add(Path.GetFileName(path));
        return data;
    }

    [Fact]
    public void Readers_AreDiscovered() => AllReaders.Should().NotBeEmpty();

    [Theory]
    [MemberData(nameof(FixtureFiles))]
    public void EachFixture_ClaimedByExactlyOneReader(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Imports", "Fixtures", fileName);
        var result = new StatementParser(AllReaders, []).Parse(new MemoryStream(File.ReadAllBytes(path)), fileName);

        result.IsSuccess.Should().BeTrue(because: "exactly one reader must claim the file: {0}", result.IsFailure ? result.Error.Description : "");
    }
}
