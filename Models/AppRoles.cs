namespace WebApplication1.Models;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Employee = "Employee";
    public const string Customer = "Customer";

    public static readonly string[] All = [Admin, Employee, Customer];
}
