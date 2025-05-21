// Testing/utils/jwtTestUtils.js
const jwt = require('jsonwebtoken');

// Тестовый секрет. В CI можно переопределять через env JWT_SECRET
const JWT_SECRET   = process.env.JWT_SECRET   || 'S0m3$up3r$3cur3AndL0ngS3cr3tK3y!';
const JWT_ISSUER   = process.env.JWT_ISSUER   || 'GeoProfilesApi';
const JWT_AUDIENCE = process.env.JWT_AUDIENCE || 'GeoProfilesClients';
const JWT_EXPIRES  = process.env.JWT_EXPIRES  || '1m';

/**
 * Генерирует JWT для тестов.
 * @param {{ id: string, username: string, roles?: string[] }} opts
 * @returns {string}
 */
function generateAuthToken({ id, username, roles = [] }) {
    const payload = {
        sub:          id,
        unique_name: username,
        jti:          require('crypto').randomUUID(),
        roles
    };

    return jwt.sign(payload, JWT_SECRET, {
        issuer:    JWT_ISSUER,
        audience:  JWT_AUDIENCE,
        expiresIn: JWT_EXPIRES,
        algorithm: 'HS256'
    });
}

/**
 * Проверяет и декодирует JWT из тестов.
 * @param {string} token
 * @returns {object} payload
 */
function verifyToken(token) {
    return jwt.verify(token, JWT_SECRET, {
        issuer:    JWT_ISSUER,
        audience:  JWT_AUDIENCE,
        algorithms:[ 'HS256' ],
        clockTolerance: 10
    });
}

module.exports = {
    generateAuthToken,
    verifyToken
};
