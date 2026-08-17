using InovaSkill.Importer.Api.Auth;
using InovaSkill.Importer.Domain.Entities;

namespace InovaSkill.Importer.Tests.Auth;

public sealed class ApiAccessPolicyTests
{
    [Theory]
    [InlineData(AppUserRoles.Diretor, "/api/routes", "GET", true)]
    [InlineData(AppUserRoles.Vendas, "/api/routes", "GET", true)]
    [InlineData(AppUserRoles.Logistica, "/api/routes", "GET", true)]
    [InlineData(AppUserRoles.Vendas, "/api/customers", "GET", true)]
    [InlineData(AppUserRoles.Logistica, "/api/fiscal-documents", "GET", true)]
    [InlineData(AppUserRoles.Vendas, "/api/production", "GET", false)]
    [InlineData(AppUserRoles.Diretor, "/api/route-imports", "GET", false)]
    [InlineData(AppUserRoles.Admin, "/api/route-imports", "POST", true)]
    [InlineData(AppUserRoles.AdminSystem, "/api/admin/jobs", "GET", true)]
    [InlineData(AppUserRoles.Admin, "/api/admin/knowledge-memories", "GET", true)]
    [InlineData(AppUserRoles.AdminSystem, "/api/admin/knowledge-memories/00000000-0000-0000-0000-000000000001", "DELETE", true)]
    [InlineData(AppUserRoles.Vendas, "/api/admin/knowledge-memories", "GET", false)]
    [InlineData(AppUserRoles.Logistica, "/api/vehicle-types", "PUT", true)]
    [InlineData(AppUserRoles.Vendas, "/api/vehicle-types", "GET", true)]
    [InlineData(AppUserRoles.Vendas, "/api/vehicle-types", "PUT", false)]
    [InlineData(AppUserRoles.Diretor, "/api/vehicle-types", "PUT", false)]
    [InlineData(AppUserRoles.Logistica, "/api/logistics-depot", "GET", true)]
    [InlineData(AppUserRoles.Diretor, "/api/logistics-depot", "GET", true)]
    [InlineData(AppUserRoles.Diretor, "/api/logistics-depot", "PUT", false)]
    [InlineData(AppUserRoles.Logistica, "/api/logistics-depot", "PUT", true)]
    [InlineData(AppUserRoles.Vendas, "/api/logistics-depot", "GET", false)]
    [InlineData(AppUserRoles.Logistica, "/api/osrm/health", "GET", true)]
    [InlineData(AppUserRoles.Vendas, "/api/osrm/health", "GET", false)]
    [InlineData(AppUserRoles.Logistica, "/api/customers/00000000-0000-0000-0000-000000000001/route-assignments", "POST", true)]
    [InlineData(AppUserRoles.Admin, "/api/customers/00000000-0000-0000-0000-000000000001/route-assignments", "POST", true)]
    [InlineData(AppUserRoles.Vendas, "/api/customers/00000000-0000-0000-0000-000000000001/route-assignments", "POST", false)]
    [InlineData(AppUserRoles.Diretor, "/api/customers/00000000-0000-0000-0000-000000000001/route-assignments", "POST", false)]
    public void CanAccess_EnforcesRoleAndOperation(
        string role,
        string path,
        string method,
        bool expected)
    {
        Assert.Equal(expected, ApiAccessPolicy.CanAccess(role, path, method));
    }

    [Fact]
    public void CanAccess_RejectsUnknownAndMissingRoles()
    {
        Assert.False(ApiAccessPolicy.CanAccess(null, "/api/products", "GET"));
        Assert.False(ApiAccessPolicy.CanAccess(AppUserRoles.Gestor, "/api/products", "GET"));
    }

    [Theory]
    [InlineData("/api/login")]
    [InlineData("/api/register")]
    public void CanAccess_KeepsAuthenticationEndpointsPublic(string path)
    {
        Assert.True(ApiAccessPolicy.CanAccess(null, path, "POST"));
    }
}
