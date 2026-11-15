using System.Security.Cryptography;

namespace ETL;

public interface ICodeGenerator
{
    string Generate();
}

public class CodeGenerator : ICodeGenerator
{

    public string Generate() => RandomNumberGenerator.GetInt32(100_000, 100_000_000).ToString("D8");
}
