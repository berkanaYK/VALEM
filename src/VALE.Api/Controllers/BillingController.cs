using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VALE.Api.Services;
using VALE.Contracts;

namespace VALE.Api.Controllers;

[ApiController]
[Route("api/billing")]
[Authorize]
public sealed class BillingController(PremiumEntitlementService entitlements) : ControllerBase
{
    [HttpGet("entitlement")]
    public Task<EntitlementDto> Get(CancellationToken cancellationToken) =>
        entitlements.GetAsync(cancellationToken);

    [HttpPost("google-play/verify")]
    [EnableRateLimiting("billing")]
    public Task<PurchaseVerificationDto> Verify(
        VerifyGooglePlayPurchaseRequest request,
        CancellationToken cancellationToken) =>
        entitlements.VerifyAndGrantAsync(request, cancellationToken);
}
