const {db} = require("../../Testing/fixtures");
const {
    convertObjectPropertiesToSnakeCase,
    convertObjectPropertiesToCamelCase
} = require("../../Testing/utils");
const testData = require("../../Testing/testData");

async function prepareUserInDb(init) {
    const user = {
        id: init.id ?? testData.random.uuid(),
        username: init.username ?? `user_${testData.random.uuid().slice(0, 8)}`,
        email: init.email ?? `${testData.random.uuid().slice(0, 8)}@example.com`,
        passwordHash: init.passwordHash ?? testData.random.string(60),
    };

    await db
        .insert(convertObjectPropertiesToSnakeCase(user))
        .into("users");

    return user;
}

async function getUserFromDb(id) {
    const rows = await db
        .select()
        .from("users")
        .where({id});

    if (!rows || rows.length === 0) return null;
    return convertObjectPropertiesToCamelCase(rows[0]);
}

const getUserListFromDb = async (ids) =>
    (await db
        .select("*")
        .from("users")
        .whereIn("id", ids))
        .map(convertObjectPropertiesToCamelCase);


module.exports = {
    users: {
        prepareUserInDb,
        getUserFromDb,
        getUserListFromDb,
    }
};
