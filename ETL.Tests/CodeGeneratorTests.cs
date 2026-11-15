namespace ETL.Tests;

public class CodeGeneratorTests
{
    private readonly CodeGenerator _sut = new();

    [Fact]
    public void Generate_ReturnsEightDigitCode()
    {
        for (var i = 0; i < 100; i++)
        {
            var code = this._sut.Generate();

            Assert.Equal(8, code.Length);
            Assert.All(code, c => Assert.True(char.IsAsciiDigit(c), $"'{c}' in '{code}' is not a digit"));
        }
    }
}
