const jwt = require('jsonwebtoken');
const jose = require('node-jose');
const jwkToPem = require('jwk-to-pem');
const {httpClient} = require("./mockServer");

const JWT_ISSUER = process.env.JWT_ISSUER || 'GeoProfilesApi';
const JWT_AUDIENCE = process.env.JWT_AUDIENCE || 'GeoProfilesClients';

const baseUrl = process.env.MOCKSERVER_URL ?? 'http://localhost:1080';
const jwksUri = `${baseUrl}/oauth2/jwks`;

/**
 * Генерирует JWT для тестов
 *
 * @param {object} options
 * @param {string|string[]} [options.audience]  — aud; по умолчанию ['GeoProfilesClients']
 * @param {string}         [options.issuer]    — iss; по умолчанию `${baseUrl}/oauth2/jwks`
 * @param {number}         [options.expires]   — время жизни в секундах от теперь; по умолчанию +3600
 * @param {string}         [options.subject]   — sub; по умолчанию uuid
 * @param {string[]}       [options.roles]     — роли для payload.roles; по умолчанию []
 */
async function generateAccessToken(options = {}) {
    
    // 1) Получаем JWKS
    const {data: jwksJson} = await httpClient.get(jwksUri);

    // 2) Загружаем в node-jose и берём приватный ключ
    const keystore = await jose.JWK.asKeyStore(jwksJson);
    // выбираем нужный ключ по kid="GeoProfiles"
    const jwk = keystore.toJSON(true).keys.find(k => k.kid === 'GeoProfiles');
    if (!jwk) throw new Error('JWK с kid=GeoProfiles не найден в JWKS');

    const privatePem = jwkToPem(jwk, {private: true});

    // 3) Строим payload
    const now = Math.floor(Date.now() / 1000);
    const payload = {
        sub: options.subject ?? require('crypto').randomUUID(),
        aud: options.audience ?? ['GeoProfilesClients'],
        iss: options.issuer ?? jwksUri,
        exp: options.expires ?? now + 3600,
        roles: options.roles ?? []
    };

    // 4) Подписываем RS256 и возвращаем токен
    return jwt.sign(payload, privatePem, {
        algorithm: 'RS256',
        keyid: 'GeoProfiles'
    });
}

async function getPublicKey() {

    const {data: jwks} = await httpClient.get(jwksUri);
    const jwk = jwks.keys.find(k => k.kid === 'GeoProfiles');
    if (!jwk) throw new Error('JWK с kid=GeoProfiles не найден');

    cachedPem = jwkToPem(jwk, {private: false});
    return cachedPem;
}

/**
 * Проверяет и декодирует JWT из тестов.
 * @param {string} token
 * @returns {object} payload
 */
async function verifyToken(token) {
    const publicKey = await getPublicKey();
    return jwt.verify(token, publicKey, {
        issuer: JWT_ISSUER,
        audience: JWT_AUDIENCE,
        algorithms: ['RS256'],
        clockTolerance: 10
    });
}

module.exports = {
    generateAccessToken,
    verifyToken
};
