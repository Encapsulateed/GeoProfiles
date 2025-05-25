const {httpClient, db} = require('../../../Testing/fixtures');
const customExpect = require('../../../Testing/customExpect');
const testData = {
    ...require('../../../Testing/testData'),
    ...require('../../../Testing/UserTestData')
};

const {prepareUserInDb} = testData.users;

async function makeRefreshRequest(body) {
    return await httpClient.post('api/v1/auth/refresh', body);
}

describe('POST /api/v1/auth/refresh', () => {
    let user;
    let loginResponse;

    beforeAll(async () => {
        
        const username = testData.random.alphaNumeric(8);
        const email = `${testData.random.alphaNumeric(5)}@example.com`;
        const passwordHash = testData.random.alphaNumeric(60);
        user = await prepareUserInDb({username, email, passwordHash});

        const resp = await httpClient.post('api/v1/auth/login', {email, username, passwordHash});
        loginResponse = resp.data;
    });

    describe('happy path', () => {
        it('should issue new tokens given valid refresh token', async () => {
            // Arrange
            const {refreshToken: oldRefresh} = loginResponse;

            // Act
            const response = await makeRefreshRequest({refreshToken: oldRefresh});

            // Assert HTTP
            expect(response.status).toBe(200);
            expect(response.data).toMatchObject({
                token: expect.any(String),
                refreshToken: expect.any(String)
            });
            expect(response.data.refreshToken).not.toBe(oldRefresh);

            // Assert DB: old token revoked, new saved
            const rows = await db.select().from('refresh_tokens').where({token: response.data.refreshToken});
            expect(rows).toHaveLength(1);
            expect(rows[0].is_revoked).toBe(false);

            const oldRows = await db.select().from('refresh_tokens').where({token: oldRefresh});
            expect(oldRows[0].is_revoked).toBe(true);
        });
    });

    describe('validation errors', () => {
        it('should return 400 when body is empty', async () => {
            // Act
            const response = await makeRefreshRequest({});
            // Assert
            customExpect.toBeValidationError(response);
        });

        it('should return 400 when refreshToken is empty', async () => {
            // Act
            const response = await makeRefreshRequest({refreshToken: ''});
            // Assert
            customExpect.toBeValidationError(response);
        });
    });

    describe('business errors', () => {
        it('should return 400 for non-existing token', async () => {
            // Arrange
            const fake = testData.random.uuid();
            // Act
            const response = await makeRefreshRequest({refreshToken: fake});
            // Assert
            expect(response.status).toBe(400);
            expect(response.data.errorCode).toBe('invalid_refresh_token');
        });

        it('should return 400 for expired token', async () => {
            // Arrange: создаём запись в БД с истекшим сроком
            const expiredToken = testData.random.uuid();
            const past = new Date(Date.now() - 1000 * 60 * 60 * 24).toISOString();
            await db.insert({
                user_id: user.id,
                token: expiredToken,
                expires_at: past,
                is_revoked: false
            }).into('refresh_tokens');

            // Act
            const response = await makeRefreshRequest({refreshToken: expiredToken});

            // Assert
            expect(response.status).toBe(400);
            expect(response.data.errorCode).toBe('invalid_refresh_token');
        });
    });
});
