namespace Auth.Models;
public sealed class UserRecord
{
    public long Id { get; set; }
    public string Provider { get; set; } = "";
    public string ProviderSub { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
}