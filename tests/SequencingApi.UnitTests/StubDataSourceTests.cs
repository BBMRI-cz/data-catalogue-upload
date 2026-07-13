using SequencingApi.Infrastructure.DataSource;
using Xunit;

namespace SequencingApi.UnitTests;

public sealed class StubDataSourceTests
{
    [Fact]
    public void ReadRecordsReturnsZeroRecordsAndNoErrors()
    {
        var source = new StubSequencingDataSource();

        var result = source.ReadRecords();

        Assert.False(result.IsError);
        Assert.Equal(0, result.Value.RecordCount);
        Assert.Empty(result.Value.Errors);
    }
}
