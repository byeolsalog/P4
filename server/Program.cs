using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Google.Protobuf;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

using GamePackets;          // Envelope, ErrorInfo, ErrorCode
using GamePackets.Auth;     // LoginReq/LoginRes

using Auth.Data;            // UserRepository, JwtValidator(정적)
using System.Collections.Generic;

class Program
{
    static async Task Main()
    {
        // 1) 설정
        var cfg = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddEnvironmentVariables()
            .Build();

        int port = cfg.GetValue("Server:Port", 5000);
        string conn =
            cfg.GetConnectionString("AuthDb")
            ?? cfg.GetConnectionString("Default")
            ?? throw new Exception("ConnectionStrings:AuthDb missing");

        // 2) DB 헬스체크
        try
        {
            using var c = new SqlConnection(conn);
            await c.OpenAsync();
            Console.WriteLine("[DB] Connected to AuthDb");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DB] Connection failed: {ex.Message}");
        }

        // 3) 서비스
        var users = new UserRepository(conn);

        // 4) TCP 서버
        var server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine($"[Server] Listen 0.0.0.0:{port}");

        while (true)
        {
            var client = await server.AcceptTcpClientAsync();
            _ = HandleClient(client, users, cfg);
        }
    }

    static string Ts() => DateTime.UtcNow.ToString("o");
    static string Mask(string? s)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        return s.Length > 16 ? $"{s.Substring(0, 6)}...{s[^4..]}" : "***";
    }

    // ===== 중복 요청 멱등 응답 LRU 캐시 (ReqId: ulong) =====
    sealed class ResponseCache
    {
        private readonly Dictionary<ulong, byte[]> _map = new();
        private readonly Queue<ulong> _order = new();
        private readonly int _cap;
        public ResponseCache(int capacity = 256) { _cap = capacity; }
        public bool TryGet(ulong reqId, out byte[] payload) => _map.TryGetValue(reqId, out payload);
        public void Add(ulong reqId, byte[] payload)
        {
            if (_map.ContainsKey(reqId)) return;
            _map[reqId] = payload;
            _order.Enqueue(reqId);
            if (_order.Count > _cap)
            {
                var old = _order.Dequeue();
                _map.Remove(old);
            }
        }
    }

    static async Task HandleClient(TcpClient client, UserRepository users, IConfiguration cfg)
    {
        var remote = (client.Client.RemoteEndPoint?.ToString() ?? "?");
        Console.WriteLine($"[{Ts()}] [ACCEPT] {remote}");
        using var stream = client.GetStream();

        var cache = new ResponseCache(256);

        try
        {
            while (client.Connected)
            {
                var t0 = DateTime.UtcNow;

                var lenBuf = await ReadExactAsync(stream, 4);
                if (lenBuf is null) break;

                uint payloadLen = BinaryPrimitives.ReadUInt32LittleEndian(lenBuf);
                if (payloadLen == 0) continue;

                var payload = await ReadExactAsync(stream, (int)payloadLen);
                if (payload is null) break;

                var env = Envelope.Parser.ParseFrom(payload);

                // ===== 중복 요청(같은 ReqId) → 캐시 재전송 =====
                if (cache.TryGet(env.ReqId, out var cached))
                {
                    await WritePayloadAsync(stream, cached);
                    Console.WriteLine($"[{Ts()}] [DUP]  {remote} ReqId={env.ReqId} → resend cached ({cached.Length}B)");
                    continue;
                }

                switch (env.BodyCase)
                {
                    case Envelope.BodyOneofCase.LoginReq:
                    {
                        var req = env.LoginReq;
                        var token = string.IsNullOrWhiteSpace(req.IdToken) ? "" : req.IdToken;

                        Console.WriteLine($"[{Ts()}] [REQ]  {remote} ReqId={env.ReqId} LoginReq provider={req.Provider} idToken={Mask(token)} len={payloadLen}");

                        var principal = JwtValidator.Validate(token, cfg);
                        if (principal is null)
                        {
                            var errPayload = BuildPayload(new Envelope {
                                ReqId = env.ReqId, Ver = env.Ver,
                                Result = new ErrorInfo { Code = ErrorCode.Unauthorized, Message = "Invalid token" }
                            });
                            await WritePayloadAsync(stream, errPayload);
                            cache.Add(env.ReqId, errPayload);

                            Console.WriteLine($"[{Ts()}] [RES]  {remote} ReqId={env.ReqId} LoginRes code=Unauthorized dur={(DateTime.UtcNow - t0).TotalMilliseconds:F1}ms");
                            break;
                        }

                        string uid =
                            principal.FindFirst("uid")?.Value ??
                            principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ??
                            "";
                        string displayName =
                            principal.FindFirst("name")?.Value ??
                            principal.Identity?.Name ??
                            "Player";
                        string? email =
                            principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ??
                            principal.FindFirst("email")?.Value;
                        string provider =
                            principal.FindFirst("provider")?.Value ??
                            req.Provider ?? "jwt";

                        var user = await users.GetOrCreateAsync(provider, uid, displayName, email);

                        var resEnv = new Envelope
                        {
                            ReqId = env.ReqId,
                            Ver = env.Ver,
                            Result = new ErrorInfo { Code = ErrorCode.Ok, Message = "" },
                            LoginRes = new LoginRes
                            {
                                UserId = $"U-{user.Id}",
                                DisplayName = user.DisplayName ?? ""
                            }
                        };

                        var resPayload = BuildPayload(resEnv);
                        await WritePayloadAsync(stream, resPayload);
                        cache.Add(env.ReqId, resPayload);

                        Console.WriteLine($"[{Ts()}] [RES]  {remote} ReqId={env.ReqId} LoginRes code=Ok userId=U-{user.Id} dur={(DateTime.UtcNow - t0).TotalMilliseconds:F1}ms");
                        break;
                    }

                    default:
                    {
                        Console.WriteLine($"[{Ts()}] [REQ]  {remote} ReqId={env.ReqId} Unhandled={env.BodyCase} len={payloadLen}");

                        var errPayload = BuildPayload(new Envelope {
                            ReqId = env.ReqId,
                            Ver   = env.Ver,
                            Result = new ErrorInfo { Code = ErrorCode.InvalidArgument, Message = $"Unhandled {env.BodyCase}" }
                        });
                        await WritePayloadAsync(stream, errPayload);
                        cache.Add(env.ReqId, errPayload);

                        Console.WriteLine($"[{Ts()}] [RES]  {remote} ReqId={env.ReqId} code=InvalidArgument dur={(DateTime.UtcNow - t0).TotalMilliseconds:F1}ms");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{Ts()}] [ERR]  {remote} {ex}");
        }
        finally
        {
            Console.WriteLine($"[{Ts()}] [CLOSE] {remote}");
            client.Close();
        }
    }

    // ---- 직렬화/전송 헬퍼 ----
    static byte[] BuildPayload(Envelope env)
    {
        using var ms = new MemoryStream();
        env.WriteTo(ms);
        return ms.ToArray();
    }

    static async Task WritePayloadAsync(NetworkStream stream, byte[] payload)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)payload.Length);
        await stream.WriteAsync(header);
        await stream.WriteAsync(payload);
    }

    static async Task<byte[]?> ReadExactAsync(NetworkStream stream, int size)
    {
        var buf = new byte[size];
        int off = 0;
        while (off < size)
        {
            int n = await stream.ReadAsync(buf.AsMemory(off, size - off));
            if (n == 0) return null;
            off += n;
        }
        return buf;
    }
}