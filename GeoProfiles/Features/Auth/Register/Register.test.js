const {httpClient} = require('../../../Testing/fixtures');
const customExpect = require('../../../Testing/customExpect');
const testData = {
    ...require('../../../Testing/testData'),
    ...require('../../Users/UserTestData')
};

const {prepareUserInDb, getUserFromDb} = testData.users;

async function makeRequest(request) {
    return await httpClient.post('api/v1/register', request);
}

describe('POST /api/v1/register', () => {
    describe('happy path', () => {
        it('should register a new user and persist to DB', async () => {
            // Arrange
            const username = testData.random.alphaNumeric(8);
            const email = `${testData.random.alphaNumeric(5)}@example.com`;
            const passwordHash = testData.random.alphaNumeric(60);
            const request = {username, email, passwordHash};

            // Act
            const response = await makeRequest(request);

            // Assert HTTP
            console.log(response);

            expect(response.status).toBe(201);
            expect(response.data).toMatchObject({username, email});

            // Assert DB
            const userFromDb = await getUserFromDb(response.data.id);
            expect(userFromDb).not.toBeNull();
            expect(userFromDb).toMatchObject({username, email});
        });
    });

    describe('validation errors', () => {
        it('should return 400 for invalid email', async () => {
            // Arrange
            const username = testData.random.alphaNumeric(8);
            const passwordHash = testData.random.alphaNumeric(60);
            const request = {username, email: 'invalid-email', passwordHash};

            // Act
            const response = await makeRequest(request);

            // Assert
            customExpect.toBeValidationError(response);
        });

        it('should return 400 for empty passwordHash', async () => {
            // Arrange
            const username = testData.random.alphaNumeric(8);
            const email = `${testData.random.alphaNumeric(5)}@example.com`;
            const request = {username, email, passwordHash: ''};

            // Act
            const response = await makeRequest(request);

            // Assert
            customExpect.toBeValidationError(response);
        });
    });

    describe('business logic errors', () => {
        it('should return 400 for existing user', async () => {
            // Arrange
            const username = testData.random.alphaNumeric(8);
            const email = `${testData.random.alphaNumeric(5)}@example.com`;
            const passwordHash = testData.random.alphaNumeric(60);
            const user = await prepareUserInDb({username, email, passwordHash});
            const request = {username, email, passwordHash};

            // Act
            const response = await makeRequest(request);

            // Assert HTTP
            console.log(response);
            
            expect(response.status).toBe(400);
            expect(response.data.message).toBe('User already exists');

            // Assert DB unchanged
            const userInDb = await getUserFromDb(user.id);
            expect(userInDb).toMatchObject({username, email});
        });
    });

});
