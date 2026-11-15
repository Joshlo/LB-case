using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ETL;

public interface IApiClient
{
    Task<ApiResult<List<LockerModel>?>> GetLockers();
    Task<ApiResult<List<CompartmentModel>?>> GetCompartments(string lockerId);
    Task<ApiResult<CompartmentModel?>> GetCompartment(string lockerId, string compartmentId);

    Task<ApiResult<AllocationResponse>> Allocate(string lockerId, AllocationRequest request);
    Task<ApiResult<CompartmentModel>> Release(string lockerId, string compartmentId, ReleaseRequest model);

    Task<ApiResult<NotificationResponse>> SendNotification(NotificationRequest request);
}


public class ApiClient(HttpClient client, JsonSerializerOptions jsonOptions) : IApiClient
{
    public async Task<ApiResult<List<LockerModel>?>> GetLockers()
        => await this.CreateApiResult<List<LockerModel>?>(await client.GetAsync("/api/v1/lockers"));

    public async Task<ApiResult<List<CompartmentModel>?>> GetCompartments(string lockerId)
        => await this.CreateApiResult<List<CompartmentModel>?>(await client.GetAsync($"/api/v1/lockers/{lockerId}/compartments"));

    public async Task<ApiResult<CompartmentModel?>> GetCompartment(string lockerId, string compartmentId)
        => await this.CreateApiResult<CompartmentModel?>(await client.GetAsync($"/api/v1/lockers/{lockerId}/compartments/{compartmentId}"));

    public async Task<ApiResult<AllocationResponse>> Allocate(string lockerId, AllocationRequest request)
        => await this.CreateApiResult<AllocationResponse>(await client.PostAsJsonAsync($"/api/v1/lockers/{lockerId}/allocations", request, jsonOptions));
    
    public async Task<ApiResult<CompartmentModel>> Release(string lockerId, string compartmentId, ReleaseRequest request)
        => await this.CreateApiResult<CompartmentModel>(await client.PostAsJsonAsync($"/api/v1/lockers/{lockerId}/compartments/{compartmentId}/release", request, jsonOptions));
    
    public async Task<ApiResult<NotificationResponse>> SendNotification(NotificationRequest request)
        => await this.CreateApiResult<NotificationResponse>(await client.PostAsJsonAsync("/api/v1/notifications", request, jsonOptions));

    private async Task<ApiResult<T>> CreateApiResult<T>(HttpResponseMessage response)
    {
        return response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Created)
            ? new ApiResult<T>(response.StatusCode, default(T), await response.Content.ReadFromJsonAsync<ErrorResponse>(jsonOptions))
            : new ApiResult<T>(response.StatusCode, await response.Content.ReadFromJsonAsync<T>(jsonOptions), null);

    }
}
