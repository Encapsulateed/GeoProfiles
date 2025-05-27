/* eslint-disable jest/expect-expect */
const {httpClient} = require('../../../Testing/fixtures');
const customExpect = require('../../../Testing/customExpect');

const testData = {
    ...require('../../../Testing/testData'),
    ...require('../../../Testing/UserTestData'),
    ...require('./../ProjectTestData'),
    ...require('../isolineTestData'),
};

const {prepareUserInDb} = testData.users;
const {
    prepareProjectInDb,
    getProjectFromDb,
} = testData.projects;

const {
    prepareIsolineInDb,
    getIsolinesForProject,
} = testData.isolines;

const {generateAccessToken} = require('../../../Testing/auth');

async function makeRequest(projectId) {
    return await httpClient.get(`api/v1/project/${projectId}`);
}

describe('GET /api/v1/project/:id', () => {
    let owner;
    let stranger;
    let project;
    let isolines;

    beforeAll(async () => {
        [owner, stranger] = await Promise.all([
            prepareUserInDb({
                username: `owner_${testData.random.alphaNumeric(6)}`,
                email: `${testData.random.alphaNumeric(5)}@example.com`,
                passwordHash: testData.random.alphaNumeric(60),
            }),
            prepareUserInDb({
                username: `str_${testData.random.alphaNumeric(6)}`,
                email: `${testData.random.alphaNumeric(5)}@example.com`,
                passwordHash: testData.random.alphaNumeric(60),
            }),
        ]);

        project = await prepareProjectInDb(
            {userId: owner.id, name: 'GetById project'},
        );

        isolines = [
            {level: 0, geomWkt: 'LINESTRING(0 0, 0.001 0.001)'},
            {level: 1, geomWkt: 'LINESTRING(0 0, 0.002 0.002)'},
        ];
        for (const l of isolines) {
            await prepareIsolineInDb(project.id, l);
        }
    });

    beforeEach(() => {
        delete httpClient.defaults.headers['Authorization'];
    });

    describe('happy path', () => {
        it('should return project with all isolines', async () => {
            // Arrange
            const token = await generateAccessToken({
                userId: owner.id,
                user_name: owner.username,
                email: owner.email,
            });
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

            // Act
            const response = await makeRequest(project.id);

            // Assert HTTP 
            expect(response.status).toBe(200);
            expect(response.data).toMatchObject({
                id: project.id,
                name: project.name,
            });
            expect(Array.isArray(response.data.isolines)).toBe(true);
            expect(response.data.isolines.length).toBe(isolines.length);

            for (const iso of response.data.isolines) {
                expect(typeof iso.level).toBe('number');
                expect(typeof iso.geomWkt ?? iso.geom).toBe('string');
            }

            // Assert DB 
            const projectFromDb = await getProjectFromDb(project.id);
            expect(projectFromDb).not.toBeNull();
            expect(projectFromDb.name).toBe(project.name);

            const isolinesFromDb = await getIsolinesForProject(project.id);
            expect(isolinesFromDb.length).toBe(isolines.length);

            // уровни должны идти по возрастанию 0..N-1
            const levels = isolinesFromDb.map((i) => i.level);
            expect(levels).toEqual([...levels].sort((a, b) => a - b));
        });
    });


    describe('unauthorized', () => {
        it('should return 401 if no token provided', async () => {
            const response = await makeRequest(project.id);
            expect(response.status).toBe(401);
        });

        it('should return 401 for invalid token', async () => {
            httpClient.defaults.headers['Authorization'] = 'Bearer invalid.token';
            const response = await makeRequest(project.id);
            expect(response.status).toBe(401);
        });
    });


    describe('not found / forbidden', () => {
        it('should return 404 if project does not exist', async () => {
            const token = await generateAccessToken({userId: owner.id});
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

            const fakeId = testData.random.uuid();
            const response = await makeRequest(fakeId);
            expect(response.status).toBe(404);
        });

        it('should return 404 when project belongs to another user', async () => {
            const token = await generateAccessToken({userId: stranger.id});
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

            const response = await makeRequest(project.id);
            expect(response.status).toBe(404);
        });
    });


    describe('edge cases', () => {
        it('should work when project has no isolines', async () => {
            // Arrange
            const emptyProject = await prepareProjectInDb({
                userId: owner.id,
                name: 'Empty project',
            });

            const token = await generateAccessToken({userId: owner.id});
            httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

            // Act
            const response = await makeRequest(emptyProject.id);

            // Assert
            expect(response.status).toBe(200);
            expect(Array.isArray(response.data.isolines)).toBe(true);
            expect(response.data.isolines.length).toBe(0);
        });
    });
});
