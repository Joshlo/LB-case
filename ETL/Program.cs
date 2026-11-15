using System.Text.Json;
using System.Text.Json.Serialization;
using ETL;

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    Converters =
    {
        new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper),
    },
};

var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:18080") };
IApiClient apiClient = new ApiClient(httpClient, jsonOptions);
ICodeGenerator codeGenerator = new CodeGenerator();
IStateStore stateStore = new StateStore();

var etlRunner = new EtlRunner(apiClient, codeGenerator, stateStore);
await etlRunner.Run("../datasource.csv");
 