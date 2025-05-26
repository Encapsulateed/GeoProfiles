const {db} = require('../../Testing/fixtures');
const {
    convertObjectPropertiesToSnakeCase,
    convertObjectPropertiesToCamelCase,
} = require('../../Testing/utils');
const testData = require('../../Testing/testData');

/**
 * Генерирует WKT-квадрат 0.002° × 0.002° вокруг случайной точки
 * в диапазоне ±0.05° от (0,0) — достаточно для unit-тестов.
 */
function randomBboxWkt() {
    const dx = (Math.random() * 0.1 - 0.05).toFixed(6);
    const dy = (Math.random() * 0.1 - 0.05).toFixed(6);

    const p1 = `${dx} ${dy}`;
    const p2 = `${(+dx + 0.002).toFixed(6)} ${dy}`;
    const p3 = `${(+dx + 0.002).toFixed(6)} ${(+dy + 0.002).toFixed(6)}`;
    const p4 = `${dx} ${(+dy + 0.002).toFixed(6)}`;

    return `POLYGON((${p1},${p2},${p3},${p4},${p1}))`;
}

/**
 * Создаёт проект + (опционально) изолинии прямо в тестовой БД.
 * Возвращает объект проекта в camelCase-форме.
 *
 * @param {object}  [init]                – поля проекта, которые нужно переопределить
 * @param {Array<{level:number, geomWkt:string}>} [isolines] – список изолиний
 * @returns {Promise<object>}             – созданный проект (camelCase)
 */
async function prepareProjectInDb(init = {}, isolines = []) {
    const project = {
        id: init.id ?? testData.random.uuid(),
        userId: init.userId ?? testData.random.uuid(),
        name: init.name ?? `project_${testData.random.uuid().slice(0, 8)}`,
        bboxWkt: init.bboxWkt ?? randomBboxWkt(),
    };

    const snake = convertObjectPropertiesToSnakeCase(project);
    const bboxWkt = snake.bbox_wkt;
    delete snake.bbox_wkt;

    await db('projects').insert({
        ...snake,
        bbox: db.raw('ST_GeomFromText(?, 4326)', [bboxWkt]),
    });

    if (isolines.length) {
        const rows = isolines.map(({level, geomWkt}) => {
            const camel = {
                id: testData.random.uuid(),
                projectId: project.id,
                level,
                geomWkt,
            };
            const snakeIso = convertObjectPropertiesToSnakeCase(camel);
            const geomWktSn = snakeIso.geom_wkt;
            delete snakeIso.geom_wkt;

            return {
                ...snakeIso,
                geom: db.raw('ST_GeomFromText(?, 4326)', [geomWktSn]),
            };
        });

        await db('isolines').insert(rows);
    }

    return project;
}

async function getProjectFromDb(id) {
    const row = await db
        .select(
            'id',
            'user_id',
            'name',
            db.raw('ST_AsText(bbox) as bbox_wkt'),
            'created_at',
            'updated_at',
        )
        .from('projects')
        .where({id})
        .first();

    if (!row) return null;
    return convertObjectPropertiesToCamelCase(row);
}

async function getProjectListFromDb(ids) {
    const rows = await db
        .select(
            'id',
            'user_id',
            'name',
            db.raw('ST_AsText(bbox) as bbox_wkt'),
            'created_at',
            'updated_at',
        )
        .from('projects')
        .whereIn('id', ids);

    return rows.map(convertObjectPropertiesToCamelCase);
}

async function getIsolinesFromDb(projectId) {
    const rows = await db
        .select(
            'id',
            'project_id',
            'level',
            db.raw('ST_AsText(geom) as geom_wkt'),
        )
        .from('isolines')
        .where({project_id: projectId})
        .orderBy('level');

    return rows.map(convertObjectPropertiesToCamelCase);
}

module.exports = {
    projects: {
        prepareProjectInDb,
        getProjectFromDb,
        getProjectListFromDb,
        getIsolinesFromDb,
        randomBboxWkt
    },
};
