create extension if not exists "uuid-ossp";
       
create extension if not exists postgis;
create extension if not exists postgis_topology;
       
create function set_updated_at() returns trigger
    language plpgsql
as
$$
begin
    new.updated_at = now();
return new;
end;
$$;

create table if not exists users (
    id uuid primary key default uuid_generate_v4(),
    username varchar(50) not null unique,
    email varchar(255) not null unique,
    password_hash varchar(255) not null,
    created_at timestamp with time zone not null default now(),
    updated_at timestamp with time zone not null default now()
);

create index ux_users_email on users(email);

create trigger users_updated_at
    before update
    on users
    for each row
    execute procedure set_updated_at();