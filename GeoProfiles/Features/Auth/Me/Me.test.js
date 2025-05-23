const {httpClient} = require('../../../Testing/fixtures');
const testData = {
    ...require('../../../Testing/testData'),
    ...require('../../Users/UserTestData')
};
const {prepareUserInDb} = testData.users;
const {generateAccessToken} = require('../../../Testing/auth');

async function makeMeRequest() {
    return await httpClient.get('api/v1/auth/me');
}

describe('GET /api/v1/auth/me', () => {
    let user;

    beforeAll(async () => {
        const username = testData.random.alphaNumeric(8);
        const email = `${testData.random.alphaNumeric(5)}@example.com`;
        const passwordHash = testData.random.alphaNumeric(60);
        user = await prepareUserInDb({username, email, passwordHash});
    });

    beforeEach(() => {
        delete httpClient.defaults.headers['Authorization'];
    });

    describe('happy path', () => {
        it('should return current user info when valid token provided', async () => {
            // Arrange
            const token = await generateAccessToken({userId: user.id});
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

            // Act
            const response = await makeMeRequest();

            // Assert
            expect(response.status).toBe(200);
            expect(response.data).toMatchObject({
                id: user.id,
                username: user.username,
                email: user.email
            });
        });
    });

    describe('unauthorized', () => {
        it('should return 401 if no token provided', async () => {
            // Act
            const response = await makeMeRequest();

            // Assert
            expect(response.status).toBe(401);
        });

        it('should return 401 for invalid token', async () => {
            // Arrange
            httpClient.defaults.headers['Authorization'] = 'Bearer invalid.token.here';

            // Act
            const response = await makeMeRequest();

            // Assert
            expect(response.status).toBe(401);
        });
    });

    describe('not found', () => {
        it('should return 404 if token subject does not exist', async () => {
            // Arrange: token for random user id
            const fakeId = testData.random.uuid();
            const token = await generateAccessToken({userId: fakeId});
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

            // Act
            const response = await makeMeRequest();

            // Assert
            expect(response.status).toBe(404);
        });
    });
});
