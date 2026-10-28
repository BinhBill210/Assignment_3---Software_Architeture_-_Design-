using Microsoft.AspNetCore.Mvc;
using YourOrderWeDeliver.Core.Coordination;
using YourOrderWeDeliver.Core.Domain;
using YourOrderWeDeliver.Web.Infrastructure;

namespace YourOrderWeDeliver.Web.Pages.Office.Requests;

/// <summary>A1 task T2 subtask 1: office staff see recorded requests, filtered by stage.</summary>
public sealed class IndexModel(DeliveryManagementSystem system) : DmsPageModel(system)
{
    /// <summary>Gets or sets the stage to show, or <c>null</c> for every request.</summary>
    [BindProperty(SupportsGet = true)]
    public RequestStatus? Status { get; set; }

    /// <summary>Gets or sets the full or partial request reference to find.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    /// <summary>Gets the requests to show, newest first.</summary>
    public IReadOnlyList<DeliveryRequest> Requests { get; private set; } = [];

    /// <summary>Gets the number of requests at each stage, for the filter buttons.</summary>
    public IReadOnlyDictionary<RequestStatus, int> Counts { get; private set; } = new Dictionary<RequestStatus, int>();

    /// <summary>Loads the list.</summary>
    public void OnGet()
    {
        Search = Search?.Trim();

        List<DeliveryRequest> matchingRequests = Register.Requests
            .Where(request =>
                string.IsNullOrEmpty(Search)
                || request.Reference.Contains(
                    Search, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Counts = Enum.GetValues<RequestStatus>()
            .ToDictionary(
                status => status,
                status => matchingRequests.Count(request => request.Status == status));

        Requests = matchingRequests
            .Where(request => Status is null || request.Status == Status)
            .ToList();
    }
}
