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
 * @param {string}         [options.userId]   — sub; по умолчанию uuid
 * @param {string[]}       [options.roles]     — роли для payload.roles; по умолчанию []
 */
async function generateAccessToken(options = {}) {
    const {data: jwksJson} = await httpClient.get(jwksUri);
    const keystore = await jose.JWK.asKeyStore(jwksJson);
    const jwk = keystore.toJSON(true).keys.find(k => k.kid === 'GeoProfiles');
    if (!jwk) throw new Error('JWK с kid=GeoProfiles не найден в JWKS');
    const privatePem = jwkToPem(jwk, {private: true});

    const now = Math.floor(Date.now() / 1000);

    const payload = {
        user_id: options.userId,
        user_name: options.userName,
        email: options.email,
        aud: options.audience ?? ['GeoProfilesClients'],
        iss: options.issuer ?? jwksUri,
        exp: options.expires ?? now + 3600,
        roles: options.roles ?? []
    };

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
        issuer: jwksUri,
        audience: JWT_AUDIENCE,
        algorithms: ['RS256'],
        clockTolerance: 10
    });
}

module.exports = {
    generateAccessToken,
    verifyToken
};
