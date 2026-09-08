using sassClaude.Data;

namespace sassClaude.Services;

public class HttpContextCurrentTenantAccessor : ICurrentTenantAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentTenantAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? EmpresaId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User?.FindFirst(TenantClaimTypes.EmpresaId)?.Value;
            return int.TryParse(value, out var empresaId) ? empresaId : null;
        }
    }
}
