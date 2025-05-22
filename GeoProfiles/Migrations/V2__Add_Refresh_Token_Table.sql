create table refresh_tokens (
    id           uuid      primary key default uuid_generate_v4(),
    user_id      uuid      not null,
    token        varchar(2000) not null unique,
    expires_at   timestamp with time zone not null,
    is_revoked   boolean   not null default false,
    created_at   timestamp with time zone not null default now(),
    updated_at   timestamp with time zone not null default now()
    );

create index if not exists ix_refresh_tokens_user_id on refresh_tokens(user_id);

create trigger refresh_tokens_updated_at
    before update
    on refresh_tokens
    for each row
    execute procedure set_updated_at();

alter table refresh_tokens
    add constraint fk_refresh_tokens_users
        foreign key (user_id) references users(id) on delete cascade;
