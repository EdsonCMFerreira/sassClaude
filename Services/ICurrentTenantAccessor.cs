namespace Saas.Services;

public interface ICurrentTenantAccessor
{
    int? EmpresaId { get; }
}
