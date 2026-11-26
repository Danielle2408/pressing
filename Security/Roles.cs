namespace Pressing.Security;

public static class Roles
{
    public const string Administrator = "Administrateur";
    public const string Operator = "Operateur";
    public const string Customer = "Client";

    public static readonly IReadOnlyList<string> All =
        [Administrator, Operator, Customer];
}
