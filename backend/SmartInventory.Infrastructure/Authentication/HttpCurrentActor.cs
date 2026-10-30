using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SmartInventory.Application.Common;

namespace SmartInventory.Infrastructure.Authentication;

public sealed class HttpCurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public int? UserId => accessor.HttpContext?.User.Identity?.IsAuthenticated == true &&
        int.TryParse(accessor.HttpContext.User.FindFirstValue("sub"), NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0
        ? id : null;
}
