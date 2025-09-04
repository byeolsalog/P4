import express from "express";
import cors from "cors";
import jwt from "jsonwebtoken";
import admin from "firebase-admin";
import dotenv from "dotenv";
import crypto from "node:crypto";
import sql from "mssql";
import path from "node:path";
import { fileURLToPath } from "node:url";

dotenv.config({ path: "login.env" }); // env 먼저 로드

// ---------- Firebase Admin 초기화 ----------
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const keyPath = path.join(__dirname, "serviceAccountKey.json");
admin.initializeApp({ credential: admin.credential.cert(keyPath) });

// ---------- DB 풀 (MSSQL) ----------
const poolPromise = sql.connect(process.env.AUTHDB_CONNSTR);

// ---------- Express ----------
const app = express();
const port = Number(process.env.PORT || 3000);
app.use(cors());
app.use(express.json());

// ---------- 로그 유틸 ----------
const ts = () => new Date().toISOString();
const mask = (v) => {
  if (!v) return v;
  if (typeof v !== "string") return v;
  return v.length > 16 ? `${v.slice(0, 6)}...${v.slice(-4)}` : "***";
};
const maskObj = (obj) => {
  if (!obj || typeof obj !== "object") return obj;
  const clone = JSON.parse(JSON.stringify(obj));
  if (clone.idToken) clone.idToken = mask(clone.idToken);
  if (clone.accessJwt) clone.accessJwt = mask(clone.accessJwt);
  if (clone.refreshToken) clone.refreshToken = "***";
  return clone;
};

// 요청/응답 로깅(라우트보다 위)
app.use((req, res, next) => {
  const start = process.hrtime.bigint();
  console.log(`[${ts()}] [IN ] ${req.method} ${req.originalUrl} ip=${req.ip} body=${JSON.stringify(maskObj(req.body))}`);
  const _json = res.json.bind(res);
  res.json = (data) => {
    const dur = Number(process.hrtime.bigint() - start) / 1e6;
    console.log(`[${ts()}] [OUT] ${req.method} ${req.originalUrl} status=${res.statusCode} dur=${dur.toFixed(1)}ms res=${JSON.stringify(maskObj(data))}`);
    return _json(data);
  };
  next();
});

// ---------- 리프레시 토큰 유틸 ----------
function genRefresh() {
  const bytes = crypto.randomBytes(32);
  const plaintext = bytes.toString("base64");
  const hash = crypto.createHash("sha256").update(plaintext, "utf8").digest(); // Buffer
  return { plaintext, hash };
}

async function saveRefreshToken(userId, hash, deviceId) {
  const pool = await poolPromise;
  await pool
    .request()
    .input("u", sql.BigInt, userId)
    .input("h", sql.VarBinary(sql.MAX), hash)
    .input("d", sql.NVarChar(64), deviceId || null)
    .input("days", sql.Int, Number(process.env.REFRESH_DAYS || 30))
    .query(`
      INSERT INTO dbo.RefreshTokens(UserId, TokenHash, DeviceId, ExpiresAt)
      VALUES(@u, @h, @d, DATEADD(day, @days, SYSUTCDATETIME()))
    `);
}

async function getRefreshByHash(hash) {
  const pool = await poolPromise;
  const r = await pool
    .request()
    .input("h", sql.VarBinary(sql.MAX), hash)
    .query(`
      SELECT TOP 1 Id, UserId, ExpiresAt, UsedAt, RevokedAt
      FROM dbo.RefreshTokens
      WHERE TokenHash=@h
      ORDER BY Id DESC
    `);
  return r.recordset[0];
}

async function markRefreshUsed(hash) {
  const pool = await poolPromise;
  await pool.request().input("h", sql.VarBinary(sql.MAX), hash).query(`
    UPDATE dbo.RefreshTokens SET UsedAt = SYSUTCDATETIME() WHERE TokenHash=@h
  `);
}

async function revokeRefresh(hash) {
  const pool = await poolPromise;
  await pool.request().input("h", sql.VarBinary(sql.MAX), hash).query(`
    UPDATE dbo.RefreshTokens SET RevokedAt = SYSUTCDATETIME() WHERE TokenHash=@h
  `);
}

// ---------- Users 유틸(서버와 동일 키: Provider + ProviderSub) ----------
async function getOrCreateUserId(provider, providerSub, displayName, email) {
  const pool = await poolPromise;
  const r = await pool
    .request()
    .input("prov", sql.NVarChar(32), provider)
    .input("sub", sql.NVarChar(128), providerSub)
    .input("name", sql.NVarChar(64), displayName || null)
    .input("email", sql.NVarChar(128), email || null)
    .query(`
      MERGE dbo.Users AS t
      USING (SELECT @prov AS Provider, @sub AS ProviderSub) AS s
      ON (t.Provider = s.Provider AND t.ProviderSub = s.ProviderSub)
      WHEN MATCHED THEN 
        UPDATE SET DisplayName = COALESCE(@name, t.DisplayName),
                   Email       = COALESCE(@email, t.Email)
      WHEN NOT MATCHED THEN 
        INSERT(Provider, ProviderSub, DisplayName, Email)
        VALUES(@prov, @sub, @name, @email)
      OUTPUT inserted.Id AS UserId;
    `);
  return r.recordset[0].UserId;
}

async function getUserById(userId) {
  const pool = await poolPromise;
  const r = await pool
    .request()
    .input("id", sql.BigInt, userId)
    .query(`
      SELECT TOP 1 Provider, ProviderSub, DisplayName, Email
      FROM dbo.Users WHERE Id=@id
    `);
  return r.recordset[0];
}

// ---------- JWT 발급 ----------
function signAccessJwt(claims) {
  const key = process.env.JWT_KEY;
  const iss = process.env.JWT_ISSUER;
  const aud = process.env.JWT_AUDIENCE;
  if (!key || !iss || !aud) throw new Error("JWT env missing");
  return jwt.sign(claims, key, { issuer: iss, audience: aud, expiresIn: "1h" });
}

// ---------- 라우트 ----------

// 헬스체크
app.get("/health", async (_req, res) => {
  try {
    await (await poolPromise).connect; // 이미 연결되어 있으면 no-op
    res.json({ ok: true });
  } catch {
    res.status(500).json({ ok: false });
  }
});

// 로그인 (guest / firebase)
app.post("/login", async (req, res) => {
  const { provider, idToken } = req.body;
  try {
    let prov = provider;
    let sub;           // ProviderSub
    let displayName;   // 표시명
    let email = null;

    if (prov === "guest") {
      sub = `guest_${Date.now()}`;
      displayName = "Guest";
    } else {
      if (!idToken) return res.status(400).json({ ok: false, error: "idToken is required" });
      const decoded = await admin.auth().verifyIdToken(idToken);
      prov = prov || "firebase";
      sub = decoded.uid;
      displayName = decoded.name || "Player";
      email = decoded.email || null;
    }

    // Users upsert → numeric UserId 확보(RefreshTokens 용)
    const userId = await getOrCreateUserId(prov, sub, displayName, email);

    // access jwt (게임서버는 provider + uid(providerSub)로 조회)
    const accessJwt = signAccessJwt({ uid: sub, displayName, provider: prov });

    // refresh 생성/저장
    const deviceId = req.headers["x-device-id"] || null;
    const { plaintext: refreshToken, hash } = genRefresh();
    await saveRefreshToken(userId, hash, deviceId);

    return res.json({
      ok: true,
      uid: sub,
      displayName,
      accessJwt,
      refreshToken, // 평문은 클라에만 전달
      gameHost: process.env.GAME_HOST,
      gamePort: Number(process.env.GAME_PORT || 5000)
    });
  } catch (e) {
    console.error("[/login] error:", e);
    return res.status(401).json({ ok: false, error: "Invalid token" });
  }
});

// 리프레시(회전) : 새 access + 새 refresh
app.post("/refresh", async (req, res) => {
  try {
    const { refreshToken } = req.body;
    if (!refreshToken) return res.status(400).json({ ok: false, error: "refreshToken required" });

    const hash = crypto.createHash("sha256").update(refreshToken, "utf8").digest();
    const row = await getRefreshByHash(hash);
    if (!row) return res.status(401).json({ ok: false, error: "invalid refresh" });

    const now = new Date();
    if (row.RevokedAt || row.UsedAt || new Date(row.ExpiresAt) < now)
      return res.status(401).json({ ok: false, error: "expired/revoked" });

    // 이전 토큰 사용 처리(재사용 방지)
    await markRefreshUsed(hash);

    // 유저 정보 조회 → 게임서버가 기대하는 클레임으로 access 재발급
    const user = await getUserById(row.UserId);
    if (!user) return res.status(401).json({ ok: false, error: "user not found" });

    const accessJwt = signAccessJwt({
      uid: user.ProviderSub,           // 게임서버는 provider+uid 로 조회
      displayName: user.DisplayName || "Player",
      provider: user.Provider
    });

    // 새 refresh 발급/저장(회전)
    const next = genRefresh();
    await saveRefreshToken(row.UserId, next.hash, null);

    return res.json({ ok: true, accessJwt, refreshToken: next.plaintext });
  } catch (e) {
    console.error("[/refresh] error:", e);
    return res.status(500).json({ ok: false, error: "server error" });
  }
});

// 로그아웃(리프레시 폐기)
app.post("/logout", async (req, res) => {
  try {
    const { refreshToken } = req.body;
    if (!refreshToken) return res.status(400).json({ ok: false, error: "refreshToken required" });
    const hash = crypto.createHash("sha256").update(refreshToken, "utf8").digest();
    await revokeRefresh(hash);
    return res.json({ ok: true });
  } catch (e) {
    console.error("[/logout] error:", e);
    return res.status(500).json({ ok: false, error: "server error" });
  }
});

app.listen(port, () => console.log(`Webserver running on http://localhost:${port}`));