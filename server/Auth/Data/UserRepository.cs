using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Auth.Data
{
    public sealed class UserRepository
    {
        private readonly string _connString;
        public UserRepository(string connString) => _connString = connString;

        /// <summary>
        /// Provider + ProviderSub로 단일 유저 조회 (없으면 null).
        /// </summary>
        public async Task<UserRecord?> GetByProviderAsync(string provider, string sub)
        {
            await using var conn = new SqlConnection(_connString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT TOP 1
    Id, Provider, ProviderSub, DisplayName, Email, CreatedAt, LastLogin,
    ISNULL(Status, 0) AS Status
FROM dbo.Users
WHERE Provider = @p AND ProviderSub = @s;";
            cmd.Parameters.AddWithValue("@p", provider);
            cmd.Parameters.AddWithValue("@s", sub);

            await using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync()) return null;

            return new UserRecord
            {
                Id          = rd.GetInt64(0),
                Provider    = rd.GetString(1),
                ProviderSub = rd.GetString(2),
                DisplayName = rd.IsDBNull(3) ? null : rd.GetString(3),
                Email       = rd.IsDBNull(4) ? null : rd.GetString(4),
                CreatedAt   = rd.GetDateTime(5),
                LastLogin   = rd.IsDBNull(6) ? (DateTime?)null : rd.GetDateTime(6),
                Status      = rd.FieldCount > 7 && !rd.IsDBNull(7) ? rd.GetByte(7) : (byte)0
            };
        }

        /// <summary>
        /// Provider + ProviderSub 기준으로 Upsert 하고 LastLogin 갱신.
        /// DisplayName/Email은 null이면 기존 값을 유지합니다.
        /// </summary>
        public async Task<UserRecord> GetOrCreateAsync(string provider, string sub, string? name, string? email)
        {
            await using var conn = new SqlConnection(_connString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SET NOCOUNT ON;

IF EXISTS (SELECT 1 FROM dbo.Users WHERE Provider = @p AND ProviderSub = @s)
BEGIN
    UPDATE dbo.Users
       SET LastLogin   = SYSUTCDATETIME(),
           DisplayName = COALESCE(@n, DisplayName),
           Email       = COALESCE(@e, Email)
     WHERE Provider = @p AND ProviderSub = @s;
END
ELSE
BEGIN
    INSERT INTO dbo.Users (Provider, ProviderSub, DisplayName, Email, CreatedAt, LastLogin)
    VALUES (@p, @s, @n, @e, SYSUTCDATETIME(), SYSUTCDATETIME());
END

SELECT TOP 1
    Id, Provider, ProviderSub, DisplayName, Email, CreatedAt, LastLogin,
    ISNULL(Status, 0) AS Status
FROM dbo.Users
WHERE Provider = @p AND ProviderSub = @s;";
            cmd.Parameters.AddWithValue("@p", provider);
            cmd.Parameters.AddWithValue("@s", sub);
            cmd.Parameters.AddWithValue("@n", (object?)name ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@e", (object?)email ?? DBNull.Value);

            await using var rd = await cmd.ExecuteReaderAsync();
            if (!await rd.ReadAsync())
                throw new InvalidOperationException("Users upsert failed to return a row.");

            return new UserRecord
            {
                Id          = rd.GetInt64(0),
                Provider    = rd.GetString(1),
                ProviderSub = rd.GetString(2),
                DisplayName = rd.IsDBNull(3) ? null : rd.GetString(3),
                Email       = rd.IsDBNull(4) ? null : rd.GetString(4),
                CreatedAt   = rd.GetDateTime(5),
                LastLogin   = rd.IsDBNull(6) ? (DateTime?)null : rd.GetDateTime(6),
                Status      = rd.FieldCount > 7 && !rd.IsDBNull(7) ? rd.GetByte(7) : (byte)0
            };
        }
    }
}