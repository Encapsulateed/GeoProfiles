const {httpClient} = require('../../../Testing/fixtures');
const customExpect = require('../../../Testing/customExpect');
const testData = require('../../../Testing/testData');
const {users} = require('../../Users/UserTestData');
const {verifyToken} = require('../../../Testing/auth')

async function makeLoginRequest(request) {
    return await httpClient.post('api/v1/auth/login', request);
}

describe('POST /api/v1/auth/login', () => {
    describe('happy path', () => {
        it('should return token for valid credentials', async () => {
            // Arrange
            const username = testData.random.alphaNumeric(8);
            const email = `${testData.random.alphaNumeric(5)}@example.com`;
            const passwordHash = testData.random.alphaNumeric(60);
            const user = await users.prepareUserInDb({username, email, passwordHash});
            const request = {email, passwordHash: passwordHash};

            // Act
            const response = await makeLoginRequest(request);

            // Assert HTTP
            expect(response.status).toBe(200);
            expect(response.data).toMatchObject({
                token: expect.any(String),
                tokenType: 'Bearer',
                expiresIn: expect.any(Number)
            });

            // Assert JWT payload
            const payload = verifyToken(response.data.token);
            expect(payload.sub).toBe(user.id);
            expect(payload.unique_name).toBe(username);
        });
    });

    describe('validation errors', () => {
        it('should return 400 for invalid email format', async () => {
            // Arrange
            const request = {email: 'not-an-email', passwordHash: 'password'};

            // Act
            const response = await makeLoginRequest(request);

            // Assert
            customExpect.toBeValidationError(response);
        });

        it('should return 400 for missing password', async () => {
            // Arrange
            const request = {email: 'test@example.com', passwordHash: ''};

            // Act
            const response = await makeLoginRequest(request);

            // Assert
            customExpect.toBeValidationError(response);
        });
    });

    describe('invalid credentials', () => {
        it('should return 400 for non-existing user', async () => {
            // Arrange
            const request = {email: 'notexists@example.com', passwordHash: 'password'};

            // Act
            const response = await makeLoginRequest(request);

            // Assert
            expect(response.status).toBe(401);
            expect(response.data.errorCode).toBe('invalid_credentials');
        });

        it('should return 401 for wrong password', async () => {
            // Arrange
            const username = testData.random.alphaNumeric(8);
            const email = `${testData.random.alphaNumeric(5)}@example.com`;
            const passwordHash = testData.random.alphaNumeric(60);
            await users.prepareUserInDb({username, email, passwordHash});
            const request = {email, passwordHash: 'wrongPassword'};

            // Act
            const response = await makeLoginRequest(request);

            // Assert
            expect(response.status).toBe(401);
            expect(response.data.errorCode).toBe('invalid_credentials');
        });
    });
});
