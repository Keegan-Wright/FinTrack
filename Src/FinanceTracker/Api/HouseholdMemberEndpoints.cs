using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.HouseholdMember;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using HouseholdMember = FinanceTracker.Contracts.HouseholdMember.HouseholdMember;

namespace FinanceTracker.Api;

public static class HouseholdMemberEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IHouseholdMemberService"/> at <see cref="ApiRoutes.HouseholdMembers"/>.
        /// </summary>
        public RouteGroupBuilder MapHouseholdMemberEndpoints()
        {
            RouteGroupBuilder members = api.MapGroup(ApiRoutes.HouseholdMembers);

            members.MapGet(string.Empty, (IHouseholdMemberService service, CancellationToken cancellationToken) =>
                service.GetHouseholdMembersAsync(cancellationToken));

            members.MapPost(string.Empty, async Task<Ok<HouseholdMember>> (AddHouseholdMember request,
                        IHouseholdMemberService service, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.AddHouseholdMemberAsync(request, cancellationToken)))
                .Validated<AddHouseholdMember>();

            members.MapPut("{id:guid}/recognition", async Task<Ok<HouseholdMemberRecognitionSaved>> (Guid id,
                        UpdateHouseholdMemberRecognition request, IHouseholdMemberService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.UpdateRecognitionAsync(id, request, cancellationToken)))
                .Validated<UpdateHouseholdMemberRecognition>()
                .NotFoundWhenMissing();

            members.MapDelete("{id:guid}", async Task<Results<NoContent, NotFound>> (Guid id,
                    IHouseholdMemberService service, CancellationToken cancellationToken) =>
                await service.DeleteHouseholdMemberAsync(id, cancellationToken)
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound());

            return api;
        }
    }
}
