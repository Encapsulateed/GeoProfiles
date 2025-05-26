
const { db } = require('../../Testing/fixtures');
const { convertObjectPropertiesToCamelCase } = require('../../Testing/utils');

async function getProfileFromDb(id) {
    const row = await db
        .select(
            'id',
            'project_id',
            'length_m',
            db.raw('ST_AsText(start_pt) AS start_wkt'),
            db.raw('ST_AsText(end_pt)   AS end_wkt')
        )
        .from('terrain_profiles')
        .where({ id })
        .first();

    return row ? convertObjectPropertiesToCamelCase(row) : null;
}

async function getProfilePointsFromDb(profileId) {
    const rows = await db
        .select('seq', 'dist_m', 'elev_m')
        .from('terrain_profile_points')
        .where({ profile_id: profileId })
        .orderBy('seq');

    return rows.map(convertObjectPropertiesToCamelCase);
}

module.exports = {
    profile: {
        getProfileFromDb,
        getProfilePointsFromDb,
    },
};
