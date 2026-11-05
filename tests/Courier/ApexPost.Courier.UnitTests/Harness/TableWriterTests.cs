using ApexPost.Courier.Harness;

namespace ApexPost.Courier.UnitTests.Harness;

public sealed class TableWriterTests
{
    [Fact]
    public void Columns_are_padded_to_the_widest_cell_so_separators_line_up()
    {
        using var output = new StringWriter();

        TableWriter.Write(output, ["Type", "Count"], [["Pickup", "2"], ["InTransit", "10"]]);

        var lines = output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["Type      | Count", "----------+------", "Pickup    | 2", "InTransit | 10"], lines);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("Maple Depot", "Maple Depot")]
    [InlineData("Recipient not at home today", "Recipient...")]
    public void Truncate_keeps_short_text_and_shortens_long_text_with_an_ellipsis(string? text, string expected) =>
        Assert.Equal(expected, TableWriter.Truncate(text, 12));
}
