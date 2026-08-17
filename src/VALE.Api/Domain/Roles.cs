namespace VALE.Api.Domain;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Valet = "Valet";
    public const string Cashier = "Cashier";

    public static readonly string[] All = [Admin, Manager, Valet, Cashier];
    public const string StaffPolicy = "Staff";
}

