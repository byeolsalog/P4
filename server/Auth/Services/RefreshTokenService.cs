using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Auth.Services;
public sealed class RefreshTokenService
{
    private readonly string _conn;
    private readonly int _days;

    public RefreshTokenService(string conn, int days)
    {
        _conn = conn;
        _days = days;
    }

    public static (string plaintext, byte[] hash) Generate()
    {
        // 32바이트 랜덤 → base64/URL-safe 문자열로 클라에 전달
        var bytes = RandomNumberGenerator.GetBytes(32);
        var text = Convert.ToBase64String(bytes);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
        return (text, hash);
    }

    public async Task SaveAsync(long userId, byte[] hash, string? deviceId)
    {
        using var c = new SqlConnection(_conn);
        await c.OpenAsync();
        var cmd = c.CreateCommand();
        cmd.CommandText = @"
INSERT INTO dbo.RefreshTokens(UserId, TokenHash, DeviceId, ExpiresAt)
VALUES(@u, @h, @d, DATEADD(day, @days, SYSUTCDATETIME()));";
        cmd.Parameters.AddWithValue("@u", userId);
        cmd.Parameters.Add("@h", System.Data.SqlDbType.VarBinary, 64).Value = hash;
        cmd.Parameters.AddWithValue("@d", (object?)deviceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@days", _days);
        await cmd.ExecuteNonQueryAsync();
    }

    // 회전/검증 등은 다음 단계에서 추가
}