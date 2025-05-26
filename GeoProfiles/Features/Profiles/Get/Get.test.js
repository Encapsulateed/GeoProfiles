const {httpClient} = require('../../../Testing/fixtures');
const customExpect = require('../../../Testing/customExpect');
const {generateAccessToken} = require('../../../Testing/auth');

const testData = {
    ...require('../../../Testing/testData'),
    ...require('../../../Testing/UserTestData'),
    ...require('../../Projects/ProjectTestData'),
    ...require('../../Projects/isolineTestData'),
    ...require('./../ProfileTestData'),
};

const {
    randomBboxWkt,
    prepareProjectInDb,
    getProjectFromDb
} = testData.projects;

const {
    getProfileFromDb,
    getProfilePointsFromDb
} = testData.profile;

async function makeProfile(projectId, body) {
    return await httpClient.post(`api/v1/${projectId}/profile`, body);
}

async function makeGet(projectId, profileId) {
    return await httpClient.get(`api/v1/${projectId}/profile/${profileId}`);
}

describe('GET /api/v1/:projectId/profile/:profileId', () => {
    let user, token, project, projectRec;
    let start, end;
    let profileRes, profileId;

    beforeAll(async () => {
        // Arrange: create user + token
        user = await testData.users.prepareUserInDb({
            username: testData.random.alphaNumeric(8),
            email: `${testData.random.alphaNumeric(5)}@example.com`,
            passwordHash: testData.random.alphaNumeric(60),
        });
        token = await generateAccessToken({userId: user.id});

        // Arrange: project with isolines
        const isolines = [
            {level: 0, geomWkt: randomBboxWkt()},
            {level: 1, geomWkt: randomBboxWkt()},
            {level: 2, geomWkt: randomBboxWkt()}
        ];
        project = await prepareProjectInDb({userId: user.id}, isolines);

        // Read bbox to pick start/end inside it
        projectRec = await getProjectFromDb(project.id);
        const coords = projectRec.bboxWkt
            .match(/\(\((.+)\)\)/)[1]
            .split(',')
            .map(pt => pt.trim().split(' ').map(Number));
        const [[lonMin, latMin], , [lonMax, latMax]] = coords;
        const dx = (lonMax - lonMin) * 0.1;
        const dy = (latMax - latMin) * 0.1;
        start = [lonMin + dx, latMin + dy];
        end = [lonMax - dx, latMax - dy];

        // Create a profile
        httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;
        profileRes = await makeProfile(project.id, {start, end});
        profileId = profileRes.data.profileId;
    });

    beforeEach(() => {
        delete httpClient.defaults.headers['Authorization'];
    });

    it('200 returns full profile info with points', async () => {
        // Arrange
        httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

        // Act
        const res = await makeGet(project.id, profileId);

        // Assert HTTP
        expect(res.status).toBe(200);
        expect(res.data.profileId).toBe(profileId);
        expect(Array.isArray(res.data.start)).toBe(true);
        expect(Array.isArray(res.data.end)).toBe(true);
        expect(typeof res.data.length_m).toBe('number');
        expect(typeof res.data.created_at).toBe('string');
        expect(Array.isArray(res.data.points)).toBe(true);
        expect(res.data.points.length).toBeGreaterThan(0);

        // Assert DB consistency
        const profDb = await getProfileFromDb(profileId);
        expect(profDb).not.toBeNull();
        expect(res.data.length_m).toBe(profDb.lengthM);

        const ptsDb = await getProfilePointsFromDb(profileId);
        expect(ptsDb.length).toBe(res.data.points.length);

        // Each point matches
        ptsDb.forEach((dbPt, idx) => {
            const apiPt = res.data.points[idx];
            expect(apiPt.distance).toBeCloseTo(dbPt.distM, 6);
            expect(apiPt.elevation).toBeCloseTo(dbPt.elevM, 6);
        });
    });

    it('401 if no token', async () => {
        // Act
        const res = await makeGet(project.id, profileId);

        // Assert
        expect(res.status).toBe(401);
    });

    it('404 for invalid GUIDs', async () => {
        // Arrange
        httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;

        // invalid projectId
        let res = await httpClient.get('api/v1/not-a-guid/profile/not-a-guid');
        expect(res.status).toBe(404);
    });

    it('404 if project not found or not owned', async () => {
        // Arrange
        httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;
        const fakeProject = testData.random.uuid();

        // Act
        const res = await makeGet(fakeProject, profileId);

        // Assert
        expect(res.status).toBe(404);
    });

    it('404 if profile not found or not in project', async () => {
        // Arrange
        httpClient.defaults.headers['Authorization'] = `Bearer ${token}`;
        const fakeProfile = testData.random.uuid();

        // Act
        const res = await makeGet(project.id, fakeProfile);

        // Assert
        expect(res.status).toBe(404);
    });
});
