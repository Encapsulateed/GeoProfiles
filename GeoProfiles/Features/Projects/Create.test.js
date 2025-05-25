const {httpClient} = require('../../Testing/fixtures');
const customExpect = require('../../Testing/customExpect');

const testData = {
    ...require('../../Testing/testData'),
    ...require('../../Testing/UserTestData'),
    ...require('./ProjectTestData'),
    ...require('./isolineTestData'),
};

const {prepareUserInDb} = testData.users;
const {
    getProjectFromDb,
    getIsolinesFromDb,
} = testData.projects;

const {getIsolinesForProject} = testData.isolines;

const {generateAccessToken} = require('../../Testing/auth');

async function makeRequest(body) {
    return await httpClient.post('api/v1/projects', body);
}

describe('POST /api/v1/projects', () => {
    let user;

    beforeAll(async () => {
        user = await prepareUserInDb({
            username: testData.random.alphaNumeric(8),
            email: `${testData.random.alphaNumeric(5)}@example.com`,
            passwordHash: testData.random.alphaNumeric(60),
        });
    });

    beforeEach(() => {
        delete httpClient.defaults.headers['Authorization'];
    });

    describe('happy path', () => {
        it('should create project with isolines and persist all to DB', async () => {
            // Arrange
            const token = await generateAccessToken({
                userId: user.id,
                user_name: user.username,
                email: user.email,
            });
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

            const request = {name: 'My first project'};

            // Act
            const response = await makeRequest(request);

            // ---------- Assert HTTP ----------
            expect(response.status).toBe(201);
            expect(response.data).toMatchObject({name: 'My first project'});
            expect(Array.isArray(response.data.isolines)).toBe(true);
            expect(response.data.isolines.length).toBeGreaterThan(0);

            // каждый изолиния: level (number), geomWkt (string)
            for (const iso of response.data.isolines) {
                expect(typeof iso.level).toBe('number');
                expect(typeof iso.geomWkt ?? iso.geom).toBe('string');
            }

            // ---------- Assert DB ----------
            const projectFromDb = await getProjectFromDb(response.data.id);
            expect(projectFromDb).not.toBeNull();
            expect(projectFromDb.name).toBe('My first project');

            const isolines = await getIsolinesForProject(response.data.id);
            expect(isolines.length).toBe(response.data.isolines.length);

            // уровни идут 0..N-1
            const levels = isolines.map(i => i.level);
            expect(levels).toEqual([...levels].sort((a, b) => a - b));
        });
    });
    
    describe('validation errors', () => {
        it('should return 400 for empty name', async () => {
            // Arrange 
            const token = await generateAccessToken({userId: user.id});
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

            // Act
            const response = await makeRequest({name: ''});

            // Assert
            customExpect.toBeValidationError(response);
        });

        it('should return 400 for name longer than 50 chars', async () => {
            // Arrange 
            const token = await generateAccessToken({userId: user.id});
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

            const longName = testData.random.alpha(51);

            // Act
            const response = await makeRequest({name: longName});

            // Assert
            customExpect.toBeValidationError(response);
        });
    });

    describe('unauthorized', () => {
        it('should return 401 if no token provided', async () => {
            const response = await makeRequest({name: 'NoToken'});
            expect(response.status).toBe(401);
        });

        it('should return 401 for invalid token', async () => {
            httpClient.defaults.headers['Authorization'] = 'Bearer invalid.token';
            const response = await makeRequest({name: 'InvalidToken'});
            expect(response.status).toBe(401);
        });
    });

    describe('not found', () => {
        it('should return 404 if token subject does not exist', async () => {
            // Arrange
            const fakeId = testData.random.uuid();
            const token = await generateAccessToken({userId: fakeId});
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

            // Act
            const response = await makeRequest({name: 'Ghost'});

            // Assert
            expect(response.status).toBe(404);
        });
    });
});
