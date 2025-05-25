create table projects
(
    id         uuid primary key     default uuid_generate_v4(),
    user_id    uuid        not null,
    name       text        not null,
    bbox       geometry(Polygon, 4326) not null,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create index ix_projects_user_id on projects(user_id);
create index gist_projects_bbox on projects using gist(bbox);

create trigger projects_updated_at
    before update
    on projects
    for each row
    execute procedure set_updated_at();

alter table projects
    add constraint fk_projects_users
        foreign key (user_id) references users (id) on delete cascade;
